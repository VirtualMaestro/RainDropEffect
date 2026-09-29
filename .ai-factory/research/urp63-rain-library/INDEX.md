<!-- aif:research-mode:ultra -->
# Research Index: URP 6.3 rain-effect library migration

Topic: Restructure and optimize Rain Drop Effect 2 for Unity 6.3+ URP
Slug: urp63-rain-library
Updated: 2026-09-09 19:55 Europe/Berlin
Status: active

## Purpose

Preserve the inspected source evidence, detailed migration phase design, renderer/package decisions and qualification gates for a reusable Web/iOS/Android rain-effect library. This is the persisted result of `$aif-explore ultra`; it contains analysis, not project implementation or an executable task checklist.

## Artifact Index

| Artifact | Purpose | Why included | Status |
|---|---|---|---|
| [RESEARCH.md](RESEARCH.md) | Active Summary, evidence, feature contract, proposed structure, rendering/simulation design, optimization policy, budgets, eight phases and release gates | Required durable research and input for `$aif-plan` | active |
| [ADR-0001-urp-package-boundary.md](ADR-0001-urp-package-boundary.md) | One URP-only UPM package, host/sample separation and migration/version policy | Package/assembly/serialization boundaries are costly to reverse; Assets-only and multiple-package alternatives are credible | proposed |
| [ADR-0002-portable-rain-rendering.md](ADR-0002-portable-rain-rendering.md) | CPU simulation, reusable raster batches and stable scene-color source | Backend choice changes portability, overlap semantics, resource costs and all family integration; direct/field/compute alternatives differ materially | proposed |
| [C4-COMPONENT-rendering.md](C4-COMPONENT-rendering.md) | Camera ownership, profiles, simulation, geometry and URP rendering interactions | More than three components currently mix lifetime, data and rendering responsibilities; the proposed boundary needs an explicit view | proposed |
| [DEPENDENCY-GRAPH.md](DEPENDENCY-GRAPH.md) | Current source/serialized dependencies, proposed package boundary and critical migration ordering | Four effect families share rendering helpers while prefab/type identities and demo/editor consumers converge on conversion | active |

No C4 Context or Container file is included: this migration stays inside one Unity runtime/library integration and does not establish multiple deployable applications or external systems. No speculative ADR or empty supporting artifact was generated.

## Reading order

1. Read RESEARCH Active Summary and §8 for the overall direction and detailed phase sequence.
2. Read RESEARCH §1–§7 for source evidence, feature coverage, implementation constraints and proposed budgets.
3. Read the package ADR, then the rendering ADR for alternatives and decision gates.
4. Read the component view and dependency graph for resource ownership and safe migration boundaries.
5. Resolve RESEARCH §9 assumptions when creating the executable plan; all scope-affecting supporting conclusions are represented in Active Summary.

## Traceability

| ID | Requirement / decision / risk | Evidence | Owning analysis |
|---|---|---|---|
| REQ-01 / DEC-01 | Standalone package; opt-in examples and host-only tooling | E01/E10–E14; U06/U07/U13 | RESEARCH §3; ADR-0001 |
| REQ-02 / DEC-02 | Render Graph-only refraction with correct scene source | E04/E08; U01–U05/U14 | RESEARCH §4; ADR-0002; component view |
| REQ-03 | Preserve four families and rain/frozen/blood/splash intent | E02/E03/E09/E12 | RESEARCH §2/§5/§8 |
| REQ-04 / NFR-01–04 | Bounded storage, no warmed library GC, no inactive render cost | E04–E07; proposed §7 budgets | RESEARCH §5–§7; dependency graph |
| REQ-05 | Portable Web/iOS/Android baseline; optional same-backend PC | User platform request; U11/U12 | RESEARCH §6; ADR-0002 |
| REQ-06 / RISK-03 | GUID, settings, shader and API conversion | E02/E03/E08/E12/E14 | RESEARCH §3/§8 P6; ADR-0001; dependency graph |
| REQ-07 | Clean-install, real-device and version release qualification | E01/E12; no tests/profiles yet | RESEARCH §7/§8 P7 |
| RISK-01 | Stable-source overlap/shared-blur visual change | E08 and proposed renderer | RESEARCH §4; ADR-0002 |
| RISK-02 | Backbuffer, transparents, MSAA, render scale and camera stacks | URP resource/injection contracts U01–U05 | RESEARCH §4/§8 P1; component view |
| RISK-04 | Friction semantics, timing and allocations | E05–E07/E15 | RESEARCH §5/§8 P4 |
| RISK-05 | Browser/mobile bandwidth, format and thermal limits | U08/U10–U12; devices unspecified | RESEARCH §6–§7 |

## Handoff

Use `$aif-plan ultra URP 6.3 rain library migration` on the existing `URP6.3` branch. Preserve master and unrelated changes. Convert the research phases into actionable implementation tasks only in plan mode/workflow. Working assumptions about XR, world-space glass, API compatibility, publisher identity and devices remain explicit; no unanswered question is treated as user approval.
