# ADR-0001: One URP-only package with optional examples

Status: proposed
Date: 2026-09-09
Research: [INDEX.md](INDEX.md)
Decision ID: DEC-01

## Context

The requested product is a reusable rain-effect library targeting Unity 6.3+, Web, iOS and Android. The existing repository is a Unity development/demo project without an installable package boundary. Runtime and demo folders are partly separate already, but Runtime/Misc includes a gameplay HP controller used by BloodRain.prefab. Five demo scenes, sample audio/meshes and 42 host NuGet DLLs must not become library dependencies. Evidence E01/E10/E11/E12/E13 in [RESEARCH.md](RESEARCH.md) establishes these distinctions; it does not establish a general runtime-to-demo code cycle.

The current context documents intentionally preserve the legacy architecture. The user's new explicit refactoring request permits a new boundary and a migration strategy, while GUID and serialized-data integrity remain requirements.

## Decision

Propose one URP-only UPM package with Runtime and Editor assemblies, opt-in samples, focused test assemblies and documentation. Keep the existing project as the development/validation host. The package depends on the editor-compatible URP/Core packages, not on MCP, NuGetForUnity, examples or host build settings. Do not split a general rain-core package from a URP adapter for a product with only one requested renderer.

Use Unity 6.3 as the minimum series, first validate editor 6000.3.23f1, and resolve the exact URP 17.3-family patch during setup. No Built-in/HDRP backend or old-version conditional implementation is retained. Future Unity/URP support is claimed only after explicit tests, not inferred from a minimum-version field.

Runtime keeps rain/blood/frozen/splash visuals and required preset textures. Samples own HP/damage logic, audio, prototype geometry, example GUI and FPS display. Mobile quality becomes settings over the same renderer. XR/world-space scope and old-API compatibility are still working assumptions documented in RESEARCH §9, not accepted removals. The default proposal is a major API with one-time migration, no permanent legacy runtime.

Publisher ID and namespace are unresolved; select them before converting serialized types. Preserve asset GUIDs where identity is retained, and deliberately remap new identities. Maintain original MIT attribution. Use either a manually authored distribution with Samples~ and matching paths or Unity 6.3's native source/export sample convention; verify the exported consumer experience.

## Alternatives considered

| Option | Benefits | Costs / risks | Disposition |
|---|---|---|---|
| Reorganize only Assets/RainDropEffect2 | Smallest immediate move | Project settings/tooling remain easy to accidentally distribute; weak clean-install contract | Insufficient for the requested library boundary |
| One URP-only UPM package | Clear consumer boundary, explicit dependencies and optional samples | Asmdef/type identity and asset references need migration | Preferred |
| Core + URP + samples as separate packages | Useful for independent renderer/platform release cycles | Multiple package/version/dependency contracts for one requested backend | Revisit only if another actual backend needs independent distribution |
| Permanent Built-in/URP dual support | Old consumers retain rendering path | Contradicts removal of obsolete renderer/version code and doubles qualification | Rejected for this task |

## Consequences

The package can be installed and built in an empty URP project before importing any demo. Runtime/Editor asmdefs have a concrete dependency-enforcement purpose. Host tooling can remain useful without leaking into the shipped package. A migration guide and actual reference-conversion checks are essential; moving folders alone is not sufficient.

No package publication, code migration or asset deletion has occurred. This ADR records the preferred design for planning. Revisit if Asset Store delivery rules, publisher identity, external API consumers or XR requirements materially change distribution scope.

## Evidence

- [RESEARCH.md](RESEARCH.md), source register E01/E10–E14, requirements REQ-01/05/06 and phases P0/P2/P6/P7.
- [Unity 6.3 package layout](https://docs.unity3d.com/6000.3/Documentation/Manual/cus-layout.html).
- [Unity 6.3 sample export convention](https://docs.unity3d.com/6000.3/Documentation/Manual/cus-samples.html).
- [Unity 6.3 package metadata](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-manifestPkg.html).
