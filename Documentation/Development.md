# Development and test instructions

## Architecture
- `Core/VeilbreakBootstrap`: single scene entry point, explicit construction/reference wiring. All scene content is parented under it; no DontDestroyOnLoad or static singleton.
- `Core/PrimitiveFactory`: small primitive/material factory with material cleanup.
- `Core/GameInput`: aggregates touch commands and legacy desktop input. Commands are consumed once and cleared at state transitions.
- `Player/PlayerController`: camera-relative CharacterController movement, gravity, dodge and damage immunity.
- `Camera/ThirdPersonCameraController`: yaw/pitch, sensitivity, follow and environment spherecast retraction.
- `Combat/HealthComponent`, `PlayerCombat`: health events and one hit evaluation per timed directional swing.
- `Reality/`: central state, reusable presence component, and collision/support validator.
- `Enemies/GuardianAI`: finite-state combat behavior and core multiplier.
- `Core/ArenaGameManager`: title/play/pause/results, freeze policy, and reset orchestration.
- `UI/`: real Unity Canvas, health fills, controls, safe area, native event handlers and overlays.
- `Editor/VeilArenaGenerator`: scene, URP assets and project defaults generated through Editor APIs.

The runtime also contains `EncounterAudio` (original, generated reusable feedback clips) and the bounded flat-courtyard `ArenaNavigator`. `Editor/AndroidBuild` builds an explicit single-scene development APK and checks BuildReport status.

The source separates runtime, Editor and test assemblies. Key tuning defaults have `[SerializeField]`; runtime reference wiring uses explicit properties. Both actors are reused on restart. Event subscriptions are initialized once and removed on destruction. Runtime placeholder art is built once per scene, not per attack or restart.

The project uses the built-in legacy Input Manager, not the new Input System package. `ProjectSettings/InputManager.asset` defines mouse axes and StandaloneInputModule axes. Generator selects legacy handling. Do not change to Input System-only without migrating both runtime polling and the EventSystem module.

## Source-only check (no Unity)
```
python3 -m venv .venv
.venv/bin/pip install tree-sitter tree-sitter-c-sharp
.venv/bin/python Tools/validate_source.py
.venv/bin/python -m unittest discover -s Tools -p 'test_*.py' -v
```
The parser packages are developer-only and are not game dependencies. Without them the checker explicitly skips C# syntax parsing. It also checks JSON/XML, script/class names, known local/package assembly references and stable unique meta GUIDs. Package assembly names are explicitly mapped (for example UnityEngine.UI, not the package ID). Ignored local Unity caches are allowed; tracked cache files are rejected. Four Python regressions cover the validator itself, not the game. It does not typecheck Unity APIs or execute physics/gameplay. Unity will generate its own machine-specific settings and package lock on first open; review and retain essential project/package configuration after validating in the Editor.

## Unity tests
Generate the scene/configuration first, exit Play Mode, then open **Window → General → Test Runner**:
- **EditMode → Run All:** camera-pointer ownership and input clearing, health clamp/reset/death events, destination penetration rejection against inactive geometry, shared-floor support vs unsupported shift, reactive presence/collider activation.
- **PlayMode → Run All:** shift position/object presence and warning-text priority, runtime construction, title/play/pause/win/lose/title transitions, repeated same-instance reset, shift cooldown reset and enforcement, and higher exposed-core damage.

For batch testing use your local Unity executable (platform-specific path):
```
Unity -batchmode -projectPath "$PWD" -executeMethod Veilbreak.Editor.VeilArenaGenerator.GenerateBatch -quit -logFile Logs/generate.log
Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile Logs/editmode.log
Unity -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults Logs/playmode.xml -logFile Logs/playmode.log
```
Create `Logs/` first. Do not use `-quit` with `-runTests`; allow the test runner to terminate after completion. First-run package import or an input-backend change may require a separate Editor launch/restart. A valid Unity license and installed packages/modules are required. No CI build or test success is asserted by the presence of these commands.

## Manual acceptance checklist (not executed here)
1. Generate, open VeilArena and Play: title visible, no console errors; Start shows exactly one player and guardian.
2. WASD front/back/strafe relative to camera; mouse look pitch clamped; inspect walls/arches for camera clipping. LMB on empty screen attacks; UI clicks do not attack.
3. Attack just outside range, inside range, facing away, behind cover; verify one health reduction per swing and sword feedback.
4. Space dodges and respects cooldown. Time a dodge through each attack and verify immunity; stand still and verify damage/HUD feedback.
5. Q toggles palette, armor plate, gates and low platforms without teleporting. Repeated Q respects cooldown. Enter the destination gate's currently empty footprint, shift, and verify rejection/message; move clear and retry.
6. Stand on each low platform, change state, settle onto shared floor safely. No invisible remaining collider. Inspect all routes and courtyard bounds.
7. Guardian observes/approaches/telegraphs, holds aim through warning, hits once, then recovers. Both attacks occur; exposed core takes greater damage.
8. Lose; retry three times. Win; replay three times. Check original actor positions/health/state, Living geometry, cleared cooldowns/attack windows, no extra bosses, HUDs or EventSystems.
9. Pause during a windup: timer and movement freeze. Resume continues. Restart cancels pending hit. Return to Title, start again.
10. Device: joystick + attack + look simultaneously with multiple fingers; no pointer steals, stuck input or accidental camera drag from action buttons. Test finger leaving screen and app focus changes. Check narrow landscape, ultrawide, cutout and safe-area devices.
11. Build an offline Android development player; inspect graphics and shader preservation. Profile on target physical hardware for frame times, thermals, memory, GPU cost and sustained performance.

## Remaining engineering verification
Compilation/import and on-device behavior must be established using Unity, not this source checker. If Unity reports an API/package issue, capture the first compiler error and resolve it before generating/building. Tune attack readability, encounter balance, camera framing and actual touch ergonomics in Play Mode; procedural layout proportions are initial values, not a tested level-design result.

## Generator repair checks (Unity required; not executed in the source sandbox)
1. Generate twice outside Play Mode. Verify one bootstrap in the scene, one VeilArena build entry in position zero, and one pipeline/renderer asset at each documented path. Existing unrelated build entries should remain.
2. Record the scene/pipeline/renderer GUIDs and the active quality level before regenerating. GUIDs and active quality selection should remain stable. Both assets should be assigned for every quality level.
3. In a disposable clone, delete only `VeilMobileRenderer.asset` via Unity and regenerate. Confirm the renderer is recreated, pipeline renderer slot zero repaired and the scene still plays. Likewise delete only the pipeline and regenerate: the surviving renderer must be reused.
4. Enter Play Mode: Generate Veil Arena must be disabled. Try a conflicting layer-8 name in a disposable clone: generation should fail clearly without renaming the layer or replacing the scene.
5. Ensure generation reports success only after the scene actually saves. Check that the Android Build Profile uses the global scene list, or manually add VeilArena to a profile with a scene-list override.
6. Start combat and shift during a telegraph: the boss warning should remain readable rather than being replaced by the shift/tutorial message. Temporarily lose app focus while dragging the camera, then try a new finger after returning/resuming; input must not remain owned by the vanished finger.

## New reliability regressions (Unity execution pending)
EditMode adds finite-health input handling, Guardian-size destination occupancy, disabled collider filtering, platform-edge/incoming-support shift validation, sideways and corner/terrace boss routing, camera cover-bias clamping, Guardian Broken-only stagger accumulation/reset, zero-width joystick safety, and button/look pointer cleanup. PlayMode adds melee windup/one-hit/cooldown, dodge cancel/immunity, Guardian and player overlap rejection, Broken-only stagger in and outside Recovery without an extra hit, HUD health/reality/unpause hint preservation, restart cancellation and pursuit around Living cover. The full suite now has **29 Unity test methods** (14 EditMode, 15 PlayMode); none were executed in the source sandbox. Existing art/lifecycle/shift tests are retained.

See `ReleaseCandidate.md` for the Android development-build command, stage-by-stage acceptance gates and the required release evidence. The build utility does not substitute for Test Runner or physical-device testing.
