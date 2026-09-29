# Phase 5: Flow, Friction and Ribbon Trails

Plan: [index.md](index.md)
Tasks: 19-22
Depends on: Phase 4 / Tasks 12-16 (settings, lifecycle, emitter, batch wiring), Phase 1 / Task 2 (unit conversions for friction/flow)

`<pkg>` means `Packages/com.virtualmaestro.raindropeffect`.

## Objective

Port the two trail families onto the same renderer with bounded reusable ribbon storage, no coroutines, no per-drop materials, a baked friction field sampled with explicit coordinate rules, and deterministic seeded motion. After this phase all four families run through one CPU update path and one draw path.

## Current-Code Evidence

| Path | Symbols / lines | Why it matters |
|------|-----------------|----------------|
| `Assets/RainDropEffect2/Scripts/Common/DropTrail.cs` | `UpdateTrail` L145-203: expire points by `Time.time - timeCreated ≥ lifeTime`; bootstrap 2–3 points; append when `sqrDistance > vertexDistance` (squared vs. unsquared quirk); direction quaternions rotated −90° about forward; sharp turns subdivided into `angle/angleDivisions` slerped points; new head inserted at index 0. `UpdateMesh` L206-263: per-frame `new Vector3[n*2]`, `new Vector2[n*2]`, `new int[(n-1)*6]`; width `max(widthMultiplier*widthCurve(progress)*0.5, 0.001)`; verts at `±w` along the point's local Y; UV.x = progress (Stretch), UV.y = 0/1; triangles `[i*2-2, i*2-1, i*2, i*2+1, i*2, i*2-1]` | Ribbon construction to replicate without allocation; winding and UV contract. |
| `Assets/RainDropEffect2/Scripts/RainBehaviours/FlowRain/FlowRainController.cs` | `InitializeDrawer` L244-276 (per-spawn `new Material`, `angleDivisions 20`, `vertexDistance` formula); `UpdateTransform` L279-323 (`Wait` coroutine with 0.01 s steps and `rndMax=(int)(100/fluctuationRate)`; `posXDt += 0.01*Smooth*dt`; `x = Slerp(cur, cur + downward*rnd1, posXDt).x`; `y = startY - downward.y*0.5*t²*accel - InitialVelocity*t`; `z = 0.001`; wind `+= progress*wind`); `UpdateShader` L326-384 (per-type visibility early-out) | Flow motion, fluctuation cadence and the allocation sites removed here. |
| `Assets/RainDropEffect2/Scripts/RainBehaviours/FrictionFlowRain/FrictionFlowRainController.cs` | `InitializeDrawer` L254-289 (material reused per slot); `PickRandomWeightedElement` L306-320 (argmax, uniform tie-break only when all equal); `GetNextPositionWithFriction` L323-375 (`iter = clamp(150*dt,2,5)`, `resol = clamp(2*8,2,5)` → 6 lateral samples, `step = downValue/iter*3/8`, samples at `(int)(W*u), (int)(H*-v)` → vertical flip via wrap, score `1 - gray`, dictionary keyed by world position); `UpdateTransform` L411-425 (`downValue = 0.5*t²*accel*0.1 + v0*t*0.01`) | Friction sampling semantics, tie rule, coordinate flip, and scaling to reproduce exactly. |
| `<pkg>/Runtime/Textures/Rain/friction_map.tga` (+`.meta`: `isReadable 1`, `sRGBTexture 1`, 512×512, no mips) | readable source | Bake input; readability removed after bake. |
| `ProjectSettings/ProjectSettings.asset` `m_ActiveColorSpace: 0` | Gamma | `GetPixel(...).grayscale` in Gamma space equals `0.299r+0.587g+0.114b` on raw bytes; bake uses the same weights. |
| `<pkg>/Runtime/Simulation/EmitterState.cs`, `LayerRuntime.cs`, `RainBatchMesh.cs`, `RainTickContext.cs` | Phase 4 | Reused unchanged. |

## Files to Change

| Path | Action | Required change |
|------|--------|-----------------|
| `<pkg>/Runtime/Simulation/TrailBuffer.cs` | create | Bounded per-trail point ring with spacing/subdivision/expiry and ribbon emission (Task 19). |
| `<pkg>/Runtime/Simulation/FlowLayerRuntime.cs` | replace stub | Flow family (Task 20). |
| `<pkg>/Runtime/Data/FrictionField.cs` | modify | Sampling API (Task 21). |
| `<pkg>/Editor/Baking/FrictionFieldBaker.cs` | create | Bakes `FrictionField` assets from readable textures (Task 21). |
| `<pkg>/Runtime/Data/FrictionField_friction_map.asset` | create | Baked field for the shipped map (Task 21). |
| `<pkg>/Runtime/Simulation/FrictionLayerRuntime.cs` | replace stub | Friction family (Task 21). |
| `<pkg>/Tests/Editor/TrailBufferTests.cs`, `FlowLayerRuntimeTests.cs`, `FrictionFieldTests.cs`, `FrictionLayerRuntimeTests.cs`; `<pkg>/Tests/Runtime/TrailAllocationTests.cs`, `DenseTrailBenchmarkTests.cs` | create | Task 22. |

## Task 19: Implement the bounded trail buffer and ribbon emission

### Intent

One reusable structure owns all trail points for a layer: fixed capacity per trail, no per-frame arrays, deterministic point expiry, and ribbon geometry identical in layout to `DropTrail.UpdateMesh` but written straight into `RainBatchMesh`.

### Implementation Steps

1. `<pkg>/Runtime/Simulation/TrailBuffer.cs`:
   ```csharp
   internal sealed class TrailBuffer {
     public struct Point { public Vector2 Pos; public float BornAt; public Vector2 Dir; }   // Dir = unit direction of travel at this point (for width offset)
     readonly Point[] points; readonly int trailCount, maxPoints; readonly int[] head, count;   // ring per trail: index = trail*maxPoints + ((head + i) % maxPoints)
     public TrailBuffer(int trailCount, int maxPoints) { ... allocate once ... }
     public int Count(int t) => count[t];
     public void Clear(int t) { head[t] = 0; count[t] = 0; }
     public ref Point Get(int t, int i);            // i = 0 oldest … count-1 newest
     /// Adds 'pos' as the newest point if far enough from the current newest (spacing), inserting up to 'maxSubdivisions' intermediate points on sharp turns; drops oldest when full.
     public void Push(int t, Vector2 pos, float now, float spacing, int maxSubdivisions = 4) {
       if (count[t] == 0) { Add(t, pos, now, Vector2.down); Add(t, pos, now, Vector2.down); return; }   // bootstrap like legacy (two coincident points)
       ref var last = ref Get(t, count[t] - 1);
       Vector2 delta = pos - last.Pos; float dist = delta.magnitude;
       if (dist < spacing) return;
       Vector2 dir = delta / dist;
       if (count[t] >= 2) { Vector2 prevDir = last.Dir; float cos = Mathf.Clamp(Vector2.Dot(prevDir, dir), -1f, 1f); float angle = Mathf.Acos(cos);
         if (angle > 0.35f) { int sub = Mathf.Min(maxSubdivisions, (int)(angle / 0.35f)); for (int k = 1; k <= sub; k++) { float f = k / (float)(sub + 1); Vector2 d = Vector2.Lerp(prevDir, dir, f).normalized; Add(t, last.Pos + d * (dist * f), now, d); } } }
       Add(t, pos, now, dir);
     }
     void Add(int t, Vector2 pos, float now, Vector2 dir) { if (count[t] == maxPoints) { head[t] = (head[t] + 1) % maxPoints; count[t]--; } int idx = t * maxPoints + (head[t] + count[t]) % maxPoints; points[idx] = new Point { Pos = pos, BornAt = now, Dir = dir }; count[t]++; }
     /// Removes oldest points older than lifetime.
     public void Expire(int t, float now, float lifetime) { while (count[t] > 0 && now - Get(t, 0).BornAt >= lifetime) { head[t] = (head[t] + 1) % maxPoints; count[t]--; } }
     /// Emits a ribbon for trail t. Returns false when fewer than 2 points.
     public bool EmitRibbon(int t, RainBatchMesh mesh, float widthMul, AnimationCurve widthCurve, float opacity, Color32 tint, Vector4 prm) {
       int n = count[t]; if (n < 2) return false;
       if (mesh.VertexCount + n * 2 > 65535) return false;
       ushort baseV = (ushort)mesh.VertexCount;
       for (int i = 0; i < n; i++) { ref var p = ref Get(t, i); float progress = i / (float)(n - 1);   // 0 oldest … 1 newest
         float w = Mathf.Max(widthMul * widthCurve.Evaluate(1f - progress) * 0.5f, 0.001f);   // legacy progress runs newest(0)→oldest(1); mirror so curve semantics match
         Vector2 side = new Vector2(-p.Dir.y, p.Dir.x) * w;
         ref var a = ref mesh.AddVertex(); a.Position = new Vector3(p.Pos.x + side.x, p.Pos.y + side.y, opacity); a.UV = new Vector2(1f - progress, 0f); a.Tint = tint; a.Params = prm;
         ref var b = ref mesh.AddVertex(); b.Position = new Vector3(p.Pos.x - side.x, p.Pos.y - side.y, opacity); b.UV = new Vector2(1f - progress, 1f); b.Tint = tint; b.Params = prm; }
       for (int i = 1; i < n; i++) { ushort v = (ushort)(baseV + i * 2); mesh.AddIndex((ushort)(v - 2)); mesh.AddIndex((ushort)(v - 1)); mesh.AddIndex(v); mesh.AddIndex((ushort)(v + 1)); mesh.AddIndex(v); mesh.AddIndex((ushort)(v - 1)); }
       return true;
     }
   }
   ```
   Legacy ordering note: legacy inserts the newest point at index 0 and `progress = i/(n-1)` from newest to oldest; UV.x = progress; the ring stores oldest first, so `1 − progress` is used for both width and UV to keep the same visual mapping.
2. Degenerate handling: `dir` is only computed when `dist ≥ spacing > 0`; `spacing` is clamped to `≥ 0.001` by callers; `Dir` never NaN. Width minimum `0.001` prevents zero-area quads (legacy).
3. Time source: `now` is the layer's accumulated simulation time (sum of clamped `dt`), not `Time.time`, so pauses and time scale behave consistently and tests are deterministic.

### Required Interfaces and Contracts

- Capacity per layer: `trails × maxPoints` points, allocated once; `Push` never allocates.
- Ribbon vertex layout: pairs `(left, right)` per point, indices as legacy; 16-bit safe by construction because `2·trails·maxPoints ≤ 2·32·96 = 6144`.
- `EmitRibbon` writes `opacity` into `Position.z` and per-trail `Params/Tint` (no per-point variation; legacy applied material values per trail).

### Error Handling and Logging

- Exceeding 65535 vertices returns `false` (cannot happen with caps); no exceptions in the frame loop.

### Tests

- Task 22 `TrailBufferTests`.

### Acceptance Criteria

- Pushing 1000 positions along a line with spacing 0.01 into a 64-point trail keeps `Count == 64` and the oldest point advances.
- A 90° turn inserts at most 4 intermediate points.
- `EmitRibbon` on a 3-point trail yields 6 vertices and 12 indices with the legacy winding.

### Verification

- `TrailBufferTests` pass (Task 22).

## Task 20: Implement the Flow layer

### Intent

Port flowing trails (rain streaks) without coroutines or per-drop materials: closed-form vertical motion, exponential fluctuation timer, lerped lateral jitter, wind, and ribbon emission through `TrailBuffer`.

### Implementation Steps

1. Replace `<pkg>/Runtime/Simulation/FlowLayerRuntime.cs` stub:
   ```csharp
   internal sealed class FlowLayerRuntime : LayerRuntime {
     struct Trail { public bool Active; public float Age, Lifetime, Accel, FluctRate, NextFluctAt, LateralTarget, LateralT, WidthMul; public Vector2 Start, Pos, WindOffset; }
     readonly FlowLayerSettings s; readonly Trail[] trails; readonly TrailBuffer buffer; EmitterState emitter; int activeCount; float simTime; readonly int maxPoints;
     public FlowLayerRuntime(FlowLayerSettings s, RainShaderSet sh, RainQuality q) : base(s, sh, q, 2 * Cap(s,q) * Pts(s,q), 6 * Cap(s,q) * (Pts(s,q) - 1)) { this.s = s; trails = new Trail[Cap(s,q)]; maxPoints = Pts(s,q); buffer = new TrailBuffer(trails.Length, maxPoints); }
     static int Cap(FlowLayerSettings s, RainQuality q) => Mathf.Clamp(Mathf.Min(s.MaxCount, RainQualityCaps.MaxTrails(q)), 1, 32);
     static int Pts(FlowLayerSettings s, RainQuality q) => Mathf.Clamp(Mathf.Min(s.MaxPoints, RainQualityCaps.MaxPoints(q)), 4, 96);
     // IsPlaying/IsEmitting/Play/Stop/Clear as in SimpleLayerRuntime; Clear also buffer.Clear(t) for all t and simTime = 0.
     public override void Tick(in RainTickContext ctx, ref RainRandom rng) {
       simTime += ctx.Dt;
       int spawn = emitter.Tick(ctx.Dt, trails.Length - activeCount, s, ref rng);
       for (int k = 0; k < spawn; k++) Spawn(ctx, ref rng);
       Vector2 down = ctx.Down; Vector2 side = new Vector2(-down.y, down.x);   // perpendicular (lateral axis)
       for (int i = 0; i < trails.Length; i++) { ref var t = ref trails[i]; if (!t.Active) continue;
         t.Age += ctx.Dt; float p = t.Lifetime > 0f ? t.Age / t.Lifetime : 1f;
         if (p >= 1f) { t.Active = false; activeCount--; buffer.Clear(i); continue; }
         if (t.Age >= t.NextFluctAt) { t.LateralTarget = rng.Range(-s.LateralAmplitude, s.LateralAmplitude); t.LateralT = 0f; t.NextFluctAt = t.Age + NextFluctDelay(t.FluctRate, ref rng); }
         t.LateralT = Mathf.Min(1f, t.LateralT + 0.01f * s.LateralSmooth * ctx.Dt);
         float lateral = Mathf.Lerp(0f, t.LateralTarget, t.LateralT);
         float fall = 0.5f * t.Accel * t.Age * t.Age + s.InitialVelocity * t.Age;
         t.WindOffset += ctx.Wind * (p * ctx.Dt);                                 // cumulative wind, same rule as Simple/Friction (matrix tolerance (g))
         t.Pos = t.Start + down * fall + side * lateral + t.WindOffset;
         buffer.Push(i, t.Pos, simTime, Mathf.Max(0.001f, s.PointSpacing));
         buffer.Expire(i, simTime, t.Lifetime);
       }
       if (emitter.Current == EmitterState.Phase.Draining && activeCount == 0) emitter.Clear();
     }
     static float NextFluctDelay(float rate, ref RainRandom rng) { if (rate <= 0f) return float.PositiveInfinity; float u = Mathf.Clamp(rng.NextFloat(), 1e-6f, 1f - 1e-6f); return Mathf.Max(0.01f, -Mathf.Log(1f - u) / rate); }   // exponential with mean 1/rate; matches legacy geometric 0.01 s roll
     void Spawn(in RainTickContext ctx, ref RainRandom rng) { /* first inactive slot: Age 0, Lifetime = rng.Range(LifetimeRange), Accel = rng.Range(AccelerationRange), FluctRate = rng.Range(FluctuationRateRange), NextFluctAt = 0.01f, LateralTarget 0, LateralT 0, WindOffset = zero, WidthMul = rng.Range(WidthRange), Start = Pos = (rng.Range(-aspect,aspect), rng.Range(-1,1) + 2*SpawnOffsetY); buffer.Clear(slot); activeCount++ */ }
     public override void Emit(in RainTickContext ctx) {
       Mesh.Begin(); Visible = false;
       if (ctx.Intensity <= 0f || activeCount == 0) { Mesh.End(); return; }
       for (int i = 0; i < trails.Length; i++) { ref var t = ref trails[i]; if (!t.Active) continue; float p = t.Age / t.Lifetime; float a = s.AlphaOverLifetime.Evaluate(p) * ctx.Intensity; if (a <= 0f) continue;
         var tint = s.OverlayColor; tint.a *= a;
         var prm = new Vector4(s.Distortion * s.DistortionOverLifetime.Evaluate(p) * ctx.Intensity, s.Relief * s.ReliefOverLifetime.Evaluate(p) * ctx.Intensity, s.Blur * s.BlurOverLifetime.Evaluate(p) * ctx.Intensity, s.Darkness);
         if (buffer.EmitRibbon(i, Mesh, t.WidthMul, s.TrailWidth, a, tint, prm)) Visible = true; }
       Mesh.End();
     }
   }
   ```
2. Legacy `posXDt` ramp (`0.01·Smooth·dt`) is retained as `LateralT`; the legacy `Vector3.Slerp(...).x` is replaced by a linear lerp along the lateral axis (documented tolerance (d) in the matrix).
3. Wind: legacy Flow applied an instantaneous `progress × GlobalWind` offset (position recomputed from `startPos` each frame, `FlowRainController.cs:322`), while Simple/Friction accumulated it. The plan uses one cumulative rule everywhere (`WindOffset += Wind × p × dt`); all shipped presets have zero wind, so this is matrix tolerance (g).

### Required Interfaces and Contracts

- Trail lifetime is independent of frame rate and render count: points expire by `simTime − BornAt ≥ Lifetime`.
- Per-trail values (`WidthMul`, `Accel`, `FluctRate`) are rolled once at spawn from the seeded RNG; fluctuation timing consumes the same RNG so two identical seeds yield identical trails.
- Spawn/stop/clear semantics are identical to Simple (shared `EmitterState`).

### Error Handling and Logging

- `FluctuationRateRange` containing 0 → no fluctuation (infinite delay). `PointSpacing` below 0.001 clamped. No logging in the tick.

### Tests

- Task 22 `FlowLayerRuntimeTests`.

### Acceptance Criteria

- `Rain2`-like profile (flow layer, 10 trails) shows falling streaks with lateral jitter, streaks fade by curve, no allocation after warm-up, trail count never exceeds the cap.
- Same seed → identical trail positions across two runtimes; 30/60/120 Hz runs agree on trail head position within 0.5% of view height at `t = 1 s`.

### Verification

- Frame Debugger: one `DrawMesh` per flow layer; vertex count = `2 × Σ points`.
- `FlowLayerRuntimeTests` pass (Task 22).

## Task 21: Bake the friction field and implement the Friction layer

### Intent

Replace CPU `GetPixel` on a readable sRGB texture, dictionary/LINQ candidate selection, and scratch transforms with a compact baked scalar grid, explicit viewport coordinates, and a direct argmax scan that reproduces legacy path selection (including its maximum-selection semantics and tie rule).

### Implementation Steps

1. Complete `<pkg>/Runtime/Data/FrictionField.cs`:
   ```csharp
   public sealed class FrictionField : ScriptableObject {
     public int Width, Height; [HideInInspector] public byte[] Values;   // row-major, row 0 = bottom, value = 255 × (1 − grayscale) already flipped to viewport orientation
     public string SourceTexturePath; public string SourceGuid;
     /// u,v in viewport [0,1]; outside → clamped to edge. Returns score 0..1 (higher attracts flow).
     public float Sample(float u, float v) { if (Values == null || Width <= 0 || Height <= 0) return 0f; int x = Mathf.Clamp((int)(u * Width), 0, Width - 1); int y = Mathf.Clamp((int)(v * Height), 0, Height - 1); return Values[y * Width + x] * (1f / 255f); }
   }
   ```
2. `<pkg>/Editor/Baking/FrictionFieldBaker.cs`: menu `Rain Drop Effect/Bake Friction Field` (on a selected `Texture2D`) and `public static FrictionField Bake(Texture2D source, string outputAssetPath)`:
   - read pixels using the same temporary-importer technique as `CoverageMaskBaker` (readable, Default, uncompressed, keep `sRGBTexture` as is; the raw bytes are what legacy `GetPixel` returned in this Gamma project);
   - `gray = (0.299·r + 0.587·g + 0.114·b) / 255` per pixel (Unity `Color.grayscale` weights);
   - legacy sampled row `(int)(H·(−v))` which, through `Repeat` wrapping, maps viewport `v` to texture row `H − v·H` counted from the bottom, i.e. the texture appears vertically flipped relative to the viewport. Bake therefore writes `Values[y·W + x] = 255 × (1 − gray(x, H − 1 − y))` so that runtime `Sample(u, v)` with `v` measured bottom-up reproduces the legacy sample. Record this in `docs/migration-matrix.md` §Unit conversion (friction row).
   - `SourceTexturePath`, `SourceGuid` filled for traceability; asset saved at `outputAssetPath` (`<pkg>/Runtime/Data/FrictionField_<textureName>.asset`).
   - after baking the shipped map, set `friction_map.tga` importer `isReadable = false`, `mipmapEnabled = false` (runtime never samples it).
3. Bake `<pkg>/Runtime/Data/FrictionField_friction_map.asset` from `<pkg>/Runtime/Textures/Rain/friction_map.tga` (512×512 → 262 144 bytes).
4. Replace `<pkg>/Runtime/Simulation/FrictionLayerRuntime.cs` stub, structurally identical to `FlowLayerRuntime` (same `Trail` struct minus fluctuation fields, same emitter/buffer/emit) with this `Tick` body for active trails:
   ```csharp
   t.Age += dt; p = Age/Lifetime; expire as Flow;
   // Legacy advanced by the TOTAL kinematic displacement every frame (0.05·a·t² + 0.01·v0·t from the current position),
   // i.e. a speed of 3·a·t² + 0.6·v0·t units/s at 60 Hz. Settings already hold Accel = 3·a·k and InitialVelocity = 0.6·v0·k (matrix).
   float speed = t.Accel * t.Age * t.Age + s.InitialVelocity * t.Age;               // normalized units per second
   float step = speed * ctx.Dt;                                                    // displacement this tick along the friction-guided path
   if (step > 0f) t.Pos = FrictionStep(t.Pos, step, ctx, ref rng);
   t.Pos += ctx.Wind * (p * ctx.Dt);   // cumulative wind (matrix tolerance (g))
   buffer.Push(i, t.Pos, simTime, spacing); buffer.Expire(i, simTime, t.Lifetime);
   ```
   and:
   ```csharp
   Vector2 FrictionStep(Vector2 pos, float downValue, in RainTickContext ctx, ref RainRandom rng) {
     int iter = Mathf.Clamp(Mathf.RoundToInt(s.ScanRate * ctx.Dt), 2, 5);            // legacy clamp(150·dt, 2, 5)
     int lateral = Mathf.Clamp(s.LateralSamples, 2, 8);                               // legacy resol = 5 → 6 samples (0..5)
     Vector2 down = ctx.Down; Vector2 side = new Vector2(-down.y, down.x);
     float stepLen = downValue / iter; float lateralStep = stepLen * s.LateralStepFactor;
     Vector2 cur = pos;
     for (int k = 0; k < iter; k++) {
       cur += down * stepLen;
       float best = float.NegativeInfinity; int bestCount = 0; Vector2 bestPos = cur;
       for (int j = 0; j <= lateral; j++) {
         float ww = j * lateralStep - (lateral * 0.5f) * lateralStep;
         Vector2 cand = cur + side * ww;
         float u = (cand.x / ctx.Aspect) * 0.5f + 0.5f; float v = cand.y * 0.5f + 0.5f;   // normalized units → viewport
         float score = s.Field != null ? s.Field.Sample(u, v) : 0f;
         if (score > best + 1e-6f) { best = score; bestPos = cand; bestCount = 1; }
         else if (Mathf.Abs(score - best) <= 1e-6f) { bestCount++; if (rng.Range(0, bestCount) == 0) bestPos = cand; }   // reservoir tie-break: uniform among maxima
       }
       cur = bestPos;
     }
     return cur;
   }
   ```
   Legacy semantics note: legacy picked the maximum score; only when every candidate was equal did it shuffle. The reservoir rule above is uniform among all maxima, a documented superset (matrix tolerance (c)); it is deterministic under the seeded RNG. Missing `Field` → all scores 0 → straight fall along gravity with deterministic tie-breaking.
5. Viewport mapping: `u = x/(2·aspect) + 0.5`, `v = y/2 + 0.5` (normalized units → `[0,1]`), clamped inside `Sample`; candidates outside the view still get edge values (legacy would have wrapped or thrown; the matrix documents "clamp").

### Required Interfaces and Contracts

- `FrictionField.Sample(u, v)`: viewport coordinates, bottom-left origin, clamped, returns `[0,1]`, higher = preferred.
- Bake is deterministic and idempotent; the asset is shipped in `Runtime/Data`; no readable GPU texture at runtime.
- `FrictionLayerSettings.Field == null` is valid (straight fall) and logs once at rebuild: `RainLog.WarnOnce("nofield:" + name, ...)`.

### Error Handling and Logging

- Bake: missing/invalid texture → `RainLog.Error`; importer settings restored in `finally`.
- Runtime: null field warning once (above); no per-tick logs.

### Tests

- Task 22 `FrictionFieldTests`, `FrictionLayerRuntimeTests`.

### Acceptance Criteria

- `FrictionField_friction_map.asset` exists (`Width == 512`, `Height == 512`, `Values.Length == 262144`).
- `friction_map.tga` importer is no longer readable.
- `Rain1`-like profile (friction layer with the baked field) shows trails steering toward dark map regions, deterministic under a fixed seed.

### Verification

- Inspector on the asset shows `Width/Height 512`; `ls Packages/com.virtualmaestro.raindropeffect/Runtime/Data` → contains the `.asset`.
- `grep -A1 "isReadable" Packages/com.virtualmaestro.raindropeffect/Runtime/Textures/Rain/friction_map.tga.meta` → `isReadable: 0`.

## Task 22: Trail, flow, friction tests and the dense-trail benchmark

### Intent

Prove determinism, boundedness, no allocation, and frame-rate independence for the trail families, and record a CPU cost figure for the densest supported configuration to feed Phase 6's optimization decision.

### Implementation Steps

1. `<pkg>/Tests/Editor/TrailBufferTests.cs`: `PushBootstrapsTwoPoints`; `PushRespectsSpacing` (positions closer than spacing are ignored); `RingDropsOldestWhenFull` (capacity 8, push 20 → `Count == 8`, `Get(t,0).Pos` is the 13th pushed); `ExpireRemovesOldPoints`; `SharpTurnSubdividesAtMostFour`; `EmitRibbonLayout` (3 points → 6 vertices, 12 indices, indices equal `[0,1,2,3,2,1, 2,3,4,5,4,3]`); `EmitRibbonNoNaN` (random walks of 500 steps, all vertices finite); `NoAllocationOnPush` (`Is.Not.AllocatingGCMemory()` around 100 pushes).
2. `<pkg>/Tests/Editor/FlowLayerRuntimeTests.cs`: `DeterministicWithSeed`; `TrailCountNeverExceedsCap` (`MaxCount 100`, `Quality Low` → cap 8); `FrameRateIndependence` (three runtimes seeded identically ticked at 1/30, 1/60, 1/120 for 1 s with `FluctuationRateRange (0,0)` to remove randomness ordering effects; head positions within 0.005); `StopDrainsToIdle`; `ClearEmptiesBuffers` (all `buffer.Count == 0`); `FluctuationMeanMatchesRate` (statistical: 2000 samples of `NextFluctDelay(5)` mean within 10% of 0.2 s).
3. `<pkg>/Tests/Editor/FrictionFieldTests.cs`: `SampleClampsOutsideRange`; `SampleUsesFlippedRows` (construct a 2×2 field manually with `Values = {0, 0, 255, 255}` and assert `Sample(0.25, 0.75) == 1f`, `Sample(0.25, 0.25) == 0f`); `BakeInMemoryUsesGrayscaleWeights` (expose pure `Bake(Color32[] pixels, int w, int h)`; pixel `(255,0,0)` → `255 × (1 − 0.299)` = 179 ± 1); `BakeFlipsRows` (top row of input appears at `y = 0` of output).
4. `<pkg>/Tests/Editor/FrictionLayerRuntimeTests.cs`: `StraightFallWithoutField` (Field null → trail x unchanged after 1 s with `Wind 0`); `FallSpeedMatchesLegacyEquivalent` (Field null, `AccelerationRange (0.1,0.1)`, `InitialVelocity 0.5`: after 1 s in 1/60 steps the head fell by `∫(0.1·t² + 0.5·t)dt = 0.0333 + 0.25 = 0.283 ± 0.01`); `MovesTowardHigherScore` (field 8×8 with a bright column at `u ∈ [0.75,0.875]` and the trail started at `u = 0.5`: after 2 s the head `u` is ≥ 0.6); `EqualScoresTieBreakDeterministic` (uniform field: two seeded runtimes identical); `BorderClampDoesNotThrow` (start at `x = aspect`, run 2 s); `IterClamp` (`ScanRate 150`, `dt 0.001` → 2 iterations; `dt 1` → 5; expose `internal static int IterFor(float rate, float dt)`).
5. `<pkg>/Tests/Runtime/TrailAllocationTests.cs` (PlayMode): profile with one flow layer (32 trails, 96 points, High) and one friction layer (32/96, baked field): warm up 30 frames, then `Assert.That(() => effect.TickForTest(), Is.Not.AllocatingGCMemory())` and same for `EmitForTest()`.
6. `<pkg>/Tests/Runtime/DenseTrailBenchmarkTests.cs` (PlayMode, informational): same dense profile plus 256 simple drops; measure `Stopwatch` over 300 `TickForTest()+EmitForTest()` calls after warm-up; write `Logs/benchmark-cpu.json` `{ "meanTickMs": x, "p95TickMs": y, "machine": SystemInfo.processorType }` and `TestContext.WriteLine`. Assert only that `p95TickMs < 5.0` (sanity, not the device budget); Task 25 records real budgets.
7. Run EditMode and PlayMode suites; fix regressions.

### Required Interfaces and Contracts

- Internal hooks: `FrictionLayerRuntime.IterFor`, `FrictionFieldBaker.Bake(Color32[], int, int)`, `TrailBuffer` internal API, `RainEffect.TickForTest/EmitForTest` (Phase 4).

### Error Handling and Logging

- Benchmark test logs its numbers with `TestContext.WriteLine`; no `Debug.Log` spam.

### Tests

This task is the test set. Commands as in Task 18.

### Acceptance Criteria

- All new tests pass; allocation tests pass for the dense profile.
- `Logs/benchmark-cpu.json` exists with `p95TickMs` recorded.

### Verification

- `grep -c 'result="Failed"' Logs/editmode.xml Logs/playmode.xml` → `0`.
- `cat Logs/benchmark-cpu.json` → shows numbers.

## Phase Risks and Mitigations

- Risk: closed-form wind/lateral formulas visibly differ from the legacy per-frame accumulation.
  Mitigation: constants live in profile fields; Task 27's visual comparison tunes `LateralAmplitude`/`Wind` for migrated presets and documents the change.
- Risk: friction visual parity depends on the row-flip assumption.
  Mitigation: `SampleUsesFlippedRows`/`BakeFlipsRows` tests pin the convention; Task 27 compares `Rain1`/`Rain5`/`Rain6` streak paths against baseline captures and flips the bake if paths mirror.
- Risk: `Acos` per push on sharp turns costs CPU at 32×96.
  Mitigation: benchmark records it; a dot-product threshold replaces `Acos` only if `p95TickMs` exceeds the Phase 6 budget.

## Phase Completion Checklist

- Every Task N in this phase satisfies its acceptance criteria.
- Required verification commands pass.
- `index.md` task checkboxes are updated immediately after verified completion.
