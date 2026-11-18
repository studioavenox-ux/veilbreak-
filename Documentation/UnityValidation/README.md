# Unity validation evidence

This folder is where real Unity test-runner output and build reports are stored, one
subdirectory per run (`<yyyyMMdd-HHmmss>/`), produced by:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\UnityValidation\Run-UnityGates.ps1
```

## Current status: BLOCKED — no evidence yet

No subdirectory exists, because the gates have never run. The development sandbox used for
the recent work is **Linux with no Unity Editor, no Android SDK/NDK and no device**; the pinned
Editor (`D:\UnityEditors\6000.3.26f1\Editor\Unity.exe`) exists only on the Windows development
machine. Nothing in this repository may be described as Unity-verified, Play-Mode-verified,
Android-verified or device-verified until a run directory appears here containing:

| Gate | Artefact this folder must contain |
| --- | --- |
| Import + compile | `import.log` with zero `error CS####` |
| Generate Veil Arena (twice) | `VeilArena-pass1.unity`, `VeilArena-pass2.unity` + matching SHA-256 in `summary.tsv` |
| EditMode tests | `EditMode-results.xml` with `failed="0"` |
| PlayMode tests | `PlayMode-results.xml` with `failed="0"` |
| Encounter playthrough | the named play-mode cases present in `PlayMode-results.xml` |
| Android APK | `android-build.log` with the `Android development APK built:` summary line, plus the APK size |

`summary.tsv` records every gate as `PASS`, `FAIL` or `BLOCKED` with a one-line reason.

## What the sandbox *can* verify, and where it is recorded

These are separate, weaker classes of evidence and are labelled as such everywhere:

- **Offline typecheck** — `Tools/OfflineCompile/check_offline.sh all` compiles UnityEngine/
  UGUI from Unity 6000.3 C# source and typechecks the runtime, Editor and test assemblies at
  C# 10. Proves API/signature/language compatibility, not import or runtime behaviour.
- **Headless simulation** — `Tools/HeadlessSim/build_sim.sh` runs the real runtime scripts
  against a mock engine, plus an engine self-test and the project's data-only EditMode tests.
  Proves gameplay logic, not Unity or device behaviour.
- **Source validation** — `Tools/validate_source.py` checks C# syntax (tree-sitter), JSON/XML
  and Unity `.meta` GUID uniqueness and presence.

See [../Validation.md](../Validation.md) for the full history of executed checks.
