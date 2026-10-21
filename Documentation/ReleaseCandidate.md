# VEILBREAK: The Veil Arena — candidate acceptance gates

## Current status
This branch is a **source-level development candidate**, not a verified release. Baseline is PR #3's merge, `806935a`, identical to GitHub main at audit time. No Unity Editor, C# compiler with Unity reference assemblies, SDK/NDK or Android device is available in the source sandbox. Do not mark the game complete until the blocked gates below are executed successfully.

## Source audit: scope and evidence
**Confirmed bugs/risks repaired:**
- Pending melee was not cancelled by dodge. Dodge also applied a full-frame displacement even when less duration remained. Damage immunity/remaining time now derive from the same scaled clock; movement only integrates the active portion.
- Destination collision safety validated only the player, allowing state-specific cover to form on the Guardian. Both actor capsules are now protected; disabled colliders are ignored.
- Direct-only boss approach could remain blocked against cover and start a windup with no sight. A cached, bounded 31×31 flat-courtyard route search now seeks an in-range visible position. No NavMesh package or additional enemies.
- Camera world-position interpolation could pass through untested corner geometry even though the final ray was clear. Distance-only smoothing stays on the checked ray.
- Desktop UI hit filtering depended on the previous EventSystem pointer update. Current-position raycasting prevents a fresh UI click from also becoming a melee command.
- Health configuration allowed NaN/infinity to corrupt health; non-finite values are now handled explicitly.
- Focus loss could clear controls without pausing; repeated explicit initialization could duplicate subscriptions. Focus loss pauses an initialized encounter; manager/boss initialization is idempotent.

**Missing features implemented:** button press ownership/color feedback, dodge cooldown UI, bounded cover routing, original synthesized feedback sounds and a repeatable Android development APK builder.

**Quality improvements:** width-aware scaling for narrow landscape, safe-area zero-dimension guard, stable camera framing and explicit reality-colored background; preserve the previously merged low-poly art rather than replacing it again.

**Not established by source inspection:** actual package resolution/compilation, physics query correctness, camera feel, pathfinding/frame-time cost, combat balance, audio output and real multi-touch. Added tests are not proof these work until executed.

## Beginner run sequence
1. Install Unity **6000.0.38f1** through Hub with Android Build Support, SDK/NDK Tools and OpenJDK.
2. Hub → Add existing project → select this repository root. Open it and wait for package imports. Resolve Console errors before proceeding.
3. Outside Play Mode choose **VEILBREAK → Generate Veil Arena**. Allow any requested legacy-input-backend Editor restart, then generate again. Generation overwrites the generated scene, not the source. It creates actual URP assets and registers the scene via Editor APIs.
4. Open `Assets/VEILBREAK/Scenes/VeilArena.unity`. Press Play, then **Start Encounter**. The courtyard, actors and native UI are built at runtime.
5. WASD moves relative to camera; mouse rotates; LMB attacks; Space dodges/cancels melee; Q shifts; Escape pauses. Mobile uses the visible left stick, right drag area and action buttons.
6. Read the warning and dodge out of the disc or sideways out of the lane. Shift to Broken to expose the core; attack during recovery. Check loss/retry and victory/replay/return-to-title.
7. Window → General → Test Runner: run **EditMode**, then **PlayMode**, including `EncounterReliabilityTests` and `SceneArtTests`. Save XML/log results and note exact Unity version.

## Repeatable Android development build
First switch the active build target to Android in File → Build Profiles (wait for reimport). Then use **VEILBREAK → Build Android Development APK**. It generates/configures the scene, builds only VeilArena explicitly, restores the previous bundle flag, and throws if the build fails. Output: `Builds/Android/VEILBREAK-development.apk` (ignored). Requirements: ARM64 / IL2CPP / minimum API 26; target SDK automatic as installed. Default debug signing is for internal testing only. No signing keys are stored in the project.

Equivalent separate batch invocations, replacing `Unity` with your installed executable:
```
mkdir -p Logs
Unity -batchmode -projectPath "$PWD" -buildTarget Android -executeMethod Veilbreak.Editor.VeilArenaGenerator.GenerateBatch -quit -logFile Logs/generate.log
Unity -batchmode -projectPath "$PWD" -runTests -testPlatform EditMode -testResults Logs/editmode.xml -logFile Logs/editmode.log
Unity -batchmode -projectPath "$PWD" -runTests -testPlatform PlayMode -testResults Logs/playmode.xml -logFile Logs/playmode.log
Unity -batchmode -projectPath "$PWD" -buildTarget Android -executeMethod Veilbreak.Editor.AndroidBuild.BuildDevelopment -quit -logFile Logs/android-build.log
```
Do not use `-quit` on test invocations. First-run input/backend/package changes may require reopening Unity before tests/build. Inspect every exit code, Test Runner failure count and log. A successful build command does not certify tests or runtime behavior.

For a connected authorized test device:
```
adb install -r Builds/Android/VEILBREAK-development.apk
adb shell am force-stop com.avenoxgames.veilbreak
adb shell monkey -p com.avenoxgames.veilbreak -c android.intent.category.LAUNCHER 1
adb logcat -s Unity AndroidRuntime
```
These commands have **not** been run here. Use local Android/Unity tools, not a website preview.

## Required acceptance evidence
| Gate | Source sandbox result | Required local evidence |
| --- | --- | --- |
| Source/configuration checks | Passed; see Validation.md | Rerun after importing/generated config |
| Correct Editor/package import and C# compile | BLOCKED | No compilation errors; resolved package lock/version |
| Repeatable generation | BLOCKED | Generate twice; same scene/asset GUIDs, one bootstrap, valid URP/build scenes |
| Automated Unity tests | BLOCKED | 9 EditMode + 12 PlayMode test methods, zero failures, saved reports |
| Full game loop | BLOCKED | Launch → start → move/look/attack/dodge/shift → both attacks → win/loss → 3 retries/replays/title cycles; no Console errors |
| Shift edge cases | BLOCKED | Test forming cover/platforms near both actors, pillars, boundaries; same player position, rejection/no cooldown if blocked |
| Boss routing / damage | BLOCKED | Pursue around both cover variants; no repeated per-frame damage or wall hits; readable windup and vulnerable recovery |
| UI/camera/audio | BLOCKED | Current-position UI click isolation, pressed/cooldown feedback, no corner clipping, synthetic sounds stop on pause and reset |
| Android APK | BLOCKED | BuildReport success and output artifact generated locally |
| Physical device | BLOCKED | Offline launch; multi-touch movement+look+attack; cutouts/aspect ratios; focus loss/resume; full encounter; no AndroidRuntime exceptions |
| Performance | BLOCKED | Sustained CPU/GPU frame time, memory and thermal measurements on representative supported devices; 30/60 FPS targets are not measured results |

## Boundaries
Procedural art remains unrigged. Navigation assumes this fixed flat courtyard with static state geometry; clearance rebuilding/search may need device tuning. Sounds are simple original tones, no production music/voice/haptics or saved audio settings. No jump/open world/progression/inventory/network services are added. The scene/pipeline settings must still be generated by Unity; no unverified scene YAML or fake build artifact is committed.
