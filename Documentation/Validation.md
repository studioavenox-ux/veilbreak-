# Validation record

Implementation environment: Linux source sandbox, **2026-10-21**.

## Performed
- Inspected all initial repository files: README and Unity `.gitignore` only; no existing project/scripts/scenes.
- Confirmed working branch `arena/d61c4a65-veilbreak` and initially clean status.
- Reviewed each supplied image: protagonist, UI flows, enemy designs, and arena/map references. No binary concept files were copied into the game or misrepresented as models.
- Added Unity 6 version pin, package manifest, legacy input axes, runtime/editor/test assembly boundaries and stable `.meta` GUIDs.
- Parsed all C# source files with Tree-sitter C#; no syntax errors detected.
- Checked JSON manifests/assembly definitions, linker XML, file/class names and unique metadata GUIDs using `Tools/validate_source.py`.
- Reviewed initialization order: Health/CharacterControllers are created before actor components; dependencies wired before initialization; the HUD subscribes after core setup and reads current state explicitly.
- Reviewed state flow: non-playing states freeze simulation; outcomes trigger once via health death event; restart clears pending melee, dodge, shift cooldown, guardian timers, visuals, camera, health and state without spawning replacements.
- Reviewed native input routing: joystick/look pointer ownership, action buttons as separate raycast targets, mobile-only look raycast surface to avoid blocking desktop LMB on empty screen, safe-area adaptation, state-transition input clearing.
- Reviewed genuine reality effects: gates/platforms switch entire objects including colliders; capsule penetration tests inspect incoming inactive geometry; support checks reject unsupported shifts; core armor/damage and palette change together.
- Cross-checked URP asset creation and configurable rendering-property signatures against Unity-Technologies/Graphics `6000.0/staging` source through GitHub; replaced an internal-only property setter with serialized Editor configuration. This is API source review, **not compilation**.
- Checked whitespace errors and repository contents for unintended generated assets/caches.

## Added but NOT executed
- EditMode: health lifecycle; inactive destination geometry rejection; support rejection/acceptance; reactive collider presence.
- PlayMode: full bootstrap and title/pause/outcome transitions; repeated reset; cooldown behavior; exposed-core multiplier.
- Manual Editor/mobile acceptance checklist in `Development.md`.

## Unavailable / not claimed
No Unity executable, C# compiler with Unity reference assemblies, Android toolchain or physical device was available. Therefore **Unity compilation, Unity scene generation, Unity Test Runner, Play Mode, Android build/install, real multi-touch, visual quality, combat balance, and performance are unverified**. Scene and URP assets must be generated with the documented Editor menu before playing/building. There is no fabricated scene YAML, build artifact or performance result.

The source implements the requested encounter paths, but milestone acceptance in a running game remains pending Unity import, the included tests, manual playthrough, and Android device checks. No push or commit was needed to preserve the working-branch source changes.

## Post-merge repair audit — 2026-10-21
The sections above record PR #1's initial source-only validation, not a successful Unity run. A follow-up audit on `arena/b0e1f861-veilbreak` found and repaired the unresolved UI assembly name, camera-pointer cleanup, warning-text priority and generator failure/recovery paths. See [PostMergeAudit.md](PostMergeAudit.md) for exact evidence, comparison with the 78-file attachment, changed behavior and remaining checks.

Current executed results: Tree-sitter syntax parsing for 21 C# files; JSON/XML/reference-name/metadata checks with 43 unique GUIDs; four Python validator regressions; whitespace checks. Unity compilation, scene generation, Unity tests, Play Mode and Android/device checks remain **not executed**. Test sources were expanded; their presence is not a passing Unity Test Runner result.

## Candidate-reliability pass — 2026-10-21
Baseline on `arena/aa8151b8-veilbreak`: clean, `806935a`, confirmed identical to latest main with PRs #1–#3 merged. Read the current runtime/art/UI/Editor/tests and essential configuration; retained the existing project and procedural scene workflow.

Executed: Tree-sitter syntax parsing for **27 C# files**, JSON/XML/assembly names and **49 unique metadata GUIDs**, all **4 Python source-validator regressions**, and whitespace checks. Unity BuildPipeline API names were cross-checked against Unity 6000.0 source; this is not compilation. No Unity scene/settings/cache or build binary was generated here.

Prepared, **not executed**: **29 Unity test methods** (14 EditMode, 15 PlayMode), including Broken-only stagger accumulation and queued Recovery-entry stagger without an extra hit, inactive destination PhysX activation and platform-edge support, corner/terrace boss navigation, camera cover-bias clamping, timing/cancel/immunity, UI/pointer cleanup and pressed-feedback regressions. C# compilation, scene generation, Play Mode, Unity tests, APK build/install and physical-device/performance checks remain blocked. See [ReleaseCandidate.md](ReleaseCandidate.md) for evidence-based repairs, exact run/build commands and mandatory acceptance gates. This source is not a certified release candidate.

## Unity 6.3 LTS compatibility pass — 2026-11-01

Target selected by the developer: **Unity 6000.3.26f1**. The earlier references to Unity 6000.0.38f1 in this validation history record the prior baseline; they are not current setup guidance. The current compatibility findings and local acceptance steps are in [Unity63CompatibilityAudit.md](Unity63CompatibilityAudit.md).

Source-only changes: updated the editor version pin, aligned URP to 17.3.0, changed the stale Test Framework pin from 1.4.5 to the Unity 6.3 core-package line 1.6.0, set Android App Category to `game` for Android 16 large-screen landscape behavior, and updated setup/test/build instructions to the supplied Windows Editor path. No Unity-generated scene/settings/package lock was fabricated. Existing gameplay/assets/tests were preserved.

Checks run in this environment: `python3 Tools/validate_source.py` (27 C# sources, JSON/XML and metadata check; its C# parser skipped because Tree-sitter is not installed), `python3 -m unittest discover -s Tools -v` (4 validator tests passed), and whitespace/configuration inspection. **Unity 6000.3.26f1 compilation/import, package resolution, scene generation, EditMode/PlayMode tests, Android build/install, and device runtime were not run.** The local Editor acceptance gates remain outstanding; do not interpret source checks as Unity compilation or runtime verification.

## Headless gameplay simulation — 2026-11-17

Branch `arena/67e6bcaa-veilbreak`. Since no Unity Editor is available here, a **headless simulation harness** was built to execute the real gameplay scripts outside Unity and assert their behaviour. It is deliberately honest about what it proves: **this is simulation-verified, not Unity-verified.** The mock reproduces the Unity APIs the scripts call (transforms, colliders, `CharacterController`, physics casts, input, `Time`, uGUI, lifecycle messages) and runs the 17 unmodified runtime scripts; it is not the Unity engine, so rendering, Physics engine internals (PhysX), MonoBehaviour ordering guarantees, serialization, `Time.timeScale` pause fidelity and device performance stay unverified.

Executed and passing (`bash Tools/HeadlessSim/build_sim.sh`, then run `Tools/.toolchain/out/sim/app/veilsim`):

- `engine self-test: OK` — 20 invariant checks on the mock itself (vector math, `Euler`/`LookRotation` basis, parented `TransformPoint`/`InverseTransformPoint`, rotated/scaled box raycasts, floor raycast, a capsule falls 2 m and rests at `y≈0` grounded with no horizontal drift, a wall stops forward motion).
- `RESULT SIMULATION PASSED - 84 checks passed, 0 failed` across 19 cases: title freeze/immunity, restart state, melee windup + Living Veil ×0.65 damage, dodge immunity/cancel/cooldown, Broken Veil ×2 damage and core exposure, shift cooldown, rejection inside forming stone (state/position/cooldown intact), rejection with no support, slam telegraph → single hit → alternation to the energy strike, cover raised mid-windup blocking the slam, dodging the slam, stagger only from Broken core hits and only in Recovery, stagger threshold → `Stagger` + meter reset, guardian routing around Living Veil cover instead of stalling, win/lose/restart, pause freeze, repeated restarts (no duplicate death subscriptions), no damage to dead/paused encounters, and falling out of the arena.

Harness bugs found and fixed along the way (each one had been silently invalidating results, so the fixes are recorded rather than the misleading original failures): a stale-object leak between cases, inverted free/blocked search bounds in the collider sweep, `CharacterController` colliding with itself, `TransformPoint` applying world scale before rotation, missing `Physics` refresh before queries, `ComputePenetration` ignoring the caller's capsule transform, colliders created outside the scene root being invisible to physics, a box raycast that ignored non-uniform scale, and missing skin-width/step-offset handling in the movement resolution (an actor resting 5 mm inside the floor could not slide horizontally and could not climb a 0.4 m terrace).

The harness caught one genuine scene-layout defect while being written: this repository's earlier diagnosis had the Broken Veil cover at `x=0` instead of its real `x=7` position in `PrototypeSceneArt.Courtyard`. The scene art remains the source of truth; the harness copies it.

Not verified by this harness: Unity compilation/import of the scripts, scene generation, EditMode/PlayMode Test Runner results, APK build/install, rendering, real touch input, PhysX character-controller fidelity, performance. Those gates in the sections above remain outstanding.

## Shared arena layout — 2026-11-17

Branch `arena/67e6bcaa-veilbreak`. Removed the duplicated arena geometry that had let the
simulation and the real scene drift apart (a live example: the Broken Veil cover was at `x=0` in
the harness and `x=7` in `PrototypeSceneArt`).

**One definition.** `Assets/VEILBREAK/Scripts/Core/ArenaLayout.cs` now holds every shape gameplay
can collide with: floor, four boundary stones, eight piers (column + plinth), four arch piers,
the reliquary pedestal, and the four reality-reactive groups (Living/Broken route cover and
Living/Broken terrace) with their `VeilState`. `PrototypeSceneArt` builds its colliders from it,
`VeilbreakBootstrap` takes its spawn points and guardian collider dimensions from it, and
`Tools/HeadlessSim` builds its courtyard from the same data. Decorative-only geometry (flagstones,
coping blocks, masonry drums, capitals, foliage, crystals, actor art) stays hand-authored in
`PrototypeSceneArt`, deliberately not unified. `ArenaBox` positions are arena space everywhere,
including pier boxes, so an `ArenaBox` always means the same thing.

**Checks proving both consumers use it.**

| Layer | Check |
| --- | --- |
| Sim data | `EverySolidShapeIsWellFormedAndOnTheSolidLayer` — 34 shapes, all positive extents, layer 8 |
| Sim scene | sim case `Simulated courtyard is built from the shared ArenaLayout definition` — matches every layout shape by name/size/world position, rejects unexplained colliders, verifies reactive group names, order, state and child counts, verifies actor spawn/collider values, and asserts the Living cover actually blocks a capsule |
| Unity scene | PlayMode `BootstrapProducesExactlyTheLayoutGeometry` — same contract against a real bootstrapped arena |
| EditMode data | `ArenaLayoutTests` (5 tests): realities covered, spawn heights above the floor, cover/terrace placement, rotation semantics |

Adding or removing a solid shape is a gameplay change and is meant to fail
`EverySolidShapeIsWellFormedAndOnTheSolidLayer`'s explicit count until updated.

**Executed results.**

- `bash Tools/HeadlessSim/build_sim.sh` → `engine self-test: OK`, `RESULT SIMULATION PASSED - 138 checks passed, 0 failed` (the previous 84, unchanged, plus 54 layout-contract checks), `EDITMODE RESULT executed=17 failed=0 skipped=2`.
- `bash Tools/OfflineCompile/check_offline.sh all` → `OFFLINE TYPECHECK PASSED (mode: all)`; Runtime 21 sources, Editor 2, Tests 7, all 0 errors.
- `Tools/validate_source.py` → `PASS: 30 C# files checked; JSON/XML valid; 52 unique Unity asset GUIDs.` with C# syntax parsed.
- `python -m unittest discover -s Tools` → 4 validator regressions OK.

**The harness now executes real EditMode tests.** `Tools/HeadlessSim/EditModeRunner.cs` reflects
over the project's actual `Assets/VEILBREAK/Tests/Editor` sources and runs the `[Test]` methods
whose signatures only need plain data, against an asserting NUnit shim
(`Tools/HeadlessSim/NUnitShim.cs` — the typecheck stubs have empty bodies and would pass
silently). 17 execute, 2 are skipped (they need a `UnityTearDown` frame and are covered by the
PlayMode suite instead). This is not the Unity Test Runner; it is an additional honest signal.

Mock-fidelity bugs this work exposed and fixed (each had been silently hiding behaviour):

1. `Component.name` was a separate field, so components reported their own name instead of their
   GameObject's. Unity proxies it; the harness now does too.
2. `RectTransform.rect` was hard-coded to 100×100, which masked the joystick's zero-size
   divide-by-zero guard. It now follows `sizeDelta` like Unity.
3. Physics queries treated a *disabled collider* as absent. Unity's queries only skip colliders on
   *inactive GameObjects*, which is exactly what the shift validator depends on for incoming
   geometry.
4. `RayBox` applied world scale twice (scaled extents *and* a scale-divided ray), so any solid box
   with a non-unit scale was hit at the wrong distance. Now pinned by an engine self-test that
   asserts a Y-scaled slab's true world face.

All four were found by running the real tests, not by inspection. The engine self-test is now
23 invariants.

## Unity validation — BLOCKED in this environment

The pinned Editor `D:\UnityEditors\6000.3.26f1\Editor\Unity.exe` does not exist here: the sandbox
is Linux (`Linux e2b.local 6.1.158+ x86_64`) with no `/mnt/d`, no Unity binary, no Android
SDK/NDK and no device. Therefore **Unity import/compile, scene generation, EditMode and PlayMode
Test Runner runs, the encounter playthrough in Play Mode, and the Android APK build have not been
executed here and are not claimed.** No logs, test counts or `BuildReport` output are fabricated.

`Tools/UnityValidation/Run-UnityGates.ps1` runs all six gates on the Windows machine and writes
real artefacts to `Documentation/UnityValidation/<timestamp>/` (`summary.tsv`, logs, NUnit XML,
both generated scenes, the APK). The script itself has not been executed either — there is no
PowerShell here — so treat it as an unrun harness until a run directory exists:

```powershell
cd <repo>
powershell -ExecutionPolicy Bypass -File Tools\UnityValidation\Run-UnityGates.ps1
```
