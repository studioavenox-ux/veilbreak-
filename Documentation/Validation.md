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
