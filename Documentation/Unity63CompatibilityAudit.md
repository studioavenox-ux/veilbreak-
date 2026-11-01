# Unity 6.3 LTS compatibility audit

**Target editor:** Unity **6000.3.26f1** (Unity 6.3 LTS)  
**Project:** Existing VEILBREAK repository; URP; Android-first; landscape  
**Audit status:** source/configuration audit complete. The exact Editor, its Android modules, and Android hardware are not available in this source sandbox, so package resolution, C# compilation, scene generation, Unity tests, Player build, and device behavior remain **unverified**. This document is not a pass claim for those gates.

## Changes made

- `ProjectSettings/ProjectVersion.txt` now pins `6000.3.26f1`.
- `Packages/manifest.json` now pins `com.unity.render-pipelines.universal` to **17.3.0**, the URP 17.3 line associated with Unity 6000.3. The previous 17.0.3 pin belonged to the Unity 6.0 generation. uGUI 2.0.0, Test Framework 1.4.5, and the built-in module package references are retained: this audit found no API use requiring speculative upgrades to them.
- `VeilArenaGenerator` now sets `PlayerSettings.Android.appCategory = "game"`. Unity 6.3 documents this setting as the way to preserve fixed orientation behavior on Android 16 large-screen devices. The generator already sets Landscape Left and disables portrait autorotation. It does not use the obsolete `androidIsGame` API.
- Current setup/build guidance now names the exact Editor version and the supplied Windows executable path.
- No generated scene, URP asset, `ProjectSettings.asset`, package lock, or replacement project was fabricated. Unity will generate/import these in the existing project on the Windows development PC. Existing assets, scripts, metadata GUIDs, scenes and gameplay are otherwise untouched.

## Compatibility findings by area

| Area | Inspected use | Finding for 6000.3.26f1 | Action / local check |
|---|---|---|---|
| Core engine APIs | `MonoBehaviour`, `GameObject`, transforms, coroutines, `CharacterController`, `Camera`, `Screen`, `Application`, serialization and events | No use found of a Unity 6.3-removed or newly deprecated API in project C# source. These are stable core APIs. Static review cannot replace compilation. | Compile in the exact Editor; review the first Console error if any. |
| URP | Runtime `Shader.Find("Universal Render Pipeline/Lit")`; Editor creation/configuration of `UniversalRenderPipelineAsset` and `UniversalRendererData`; render pipeline quality assignments | Project references common URP APIs, not ScriptableRenderer feature or RenderGraph extension APIs. URP 17.0.3 is not the 6000.3 package line. Pin 17.3.0 and let Package Manager resolve its dependencies. | Confirm Package Manager resolves URP 17.3.0 without errors, generator creates assets, and Android shaders/materials render. No URP API binary compile was possible here. |
| Input | Built-in `UnityEngine.Input` polling (`GetKey`, `GetAxisRaw`, mouse) and `StandaloneInputModule`; supplied `InputManager.asset` axes; generator enables the legacy input backend | Legacy Input Manager remains intentionally required; the new Input System package is not installed and source is not migrated. Unity 6.3 can still use this setup. A switch to Input System-only would break desktop polling and UI event dispatch. | Preserve **Both** or **Input Manager (Old)** in Player settings; generator selects legacy input. Accept/restart if the Editor prompts after import, then regenerate and test desktop/touch. |
| uGUI / UI | `UnityEngine.UI` Canvas, `Image`, `Button`, `Slider`, `Text`, EventSystem and pointer interfaces; `Screen.safeArea` | Uses the separate uGUI package and long-standing APIs. No UI Toolkit, Accessibility, deprecated `AccessibilityNode.selected`, or removed UI API is present. `com.unity.ugui` remains 2.0.0. | Run EditMode and PlayMode UI/input tests, then test multi-touch, cutouts and narrow/large landscape screens on device. |
| 3D physics | `Physics.RaycastAll`, `Linecast`, `SphereCast`, `CapsuleCast`, `CheckCapsule`, `ComputePenetration`, `SyncTransforms`, `QueryTriggerInteraction`; `CharacterController` | These are classic 3D PhysX APIs; Unity 6.3's new low-level Box2D v3 work concerns 2D and does not replace this code. No `Physics.autoSyncTransforms` reference is present. | Run physics/shift/combat/navigation tests in both Unity test platforms; manually validate collisions and frame cost. |
| Editor scripting | `MenuItem`, `AssetDatabase`, `SerializedObject`, `EditorSceneManager`, `EditorBuildSettings`, `BuildPipeline`, `NamedBuildTarget`, `BuildReport` | No known 6.3 removal found. The generator's serialized settings-field names are Unity internals and therefore need exact-version runtime validation even when the public APIs compile. This repository deliberately has no fabricated scene/settings serialization. | In the target Editor, import, inspect Console, generate twice, check scene/build profile and stable generated asset GUIDs; exercise documented recovery cases. |
| Android | Landscape Left, portrait autorotation disabled, Android API 26 minimum, ARM64, IL2CPP, Build Profiles, development APK builder | Unity 6.3 introduces Android 16 large-screen handling: fixed orientation may be ignored unless the app is categorized as a game. Set the documented `PlayerSettings.Android.appCategory` to `game`; avoid obsolete `androidIsGame`. No custom Gradle templates or Android manifests exist in this checkout. | Install Android Build Support, SDK/NDK and OpenJDK through Hub for **6000.3.26f1**; switch to Android; build and install APK; validate orientation, app resume, permissions and devices. |
| Tests | Test Framework attributes and `UnityEngine.TestTools` in existing EditMode/PlayMode assemblies | `com.unity.test-framework` remains pinned at 1.4.5. Test source only; not executed by this audit. No test API in use was identified as 6.3-removed. | First resolve packages, then run both suites in the exact Editor and archive XML/log results. |

## Deprecated, removed, or changed API scan

Reviewed all C# source for Unity APIs around Input, physics, UI, Editor, rendering and Android. No project use was found of the Unity 6.3 upgrade-guide removals/deprecations relevant to the codebase:

- Experimental `AdditionalBakedProbes` (removed) and `CustomBake` (obsolete): not used.
- `PlayerSettings.Android.androidIsGame` (obsolete/replaced by App Category): not used; the generator now sets `appCategory`.
- `Physics.autoSyncTransforms`: not used; code calls `Physics.SyncTransforms` explicitly where required.
- Netcode for GameObjects `NetworkTransform.Update` changes: no Netcode package or multiplayer code.
- Accessibility `AccessibilityNode.selected` -> `invoked`: no Accessibility API use.
- Legacy URP custom renderer / RenderGraph extension APIs: none used by VEILBREAK.
- Custom Gradle templates and attributes: none present.

The 6000.3.26f1 release notes' reported URP fixes concern Blitter/Shader Graph and mobile reflection-probe rendering. VEILBREAK does not call Blitter, use Shader Graph, or define reflection probes, so these fixes require no source migration. They do not prove the project compiles.

## Project settings and package lock

Only `ProjectVersion.txt`, `InputManager.asset`, and `Packages/manifest.json` are tracked in the project configuration folder. The generator configures missing Unity settings through Editor APIs, and the repository has no `packages-lock.json`. Do not fabricate lock/settings YAML or replace defaults wholesale. Open the existing repository with the specified Editor; retain the package lock and necessary generated settings only after checking their changes in Unity. Do not commit `Library`, `Temp`, `Logs`, or local Android build outputs.

## Exact Windows validation procedure

Editor executable supplied by the developer:

```powershell
$Unity = 'D:\UnityEditors\6000.3.26f1\Editor\Unity.exe'
$Project = (Get-Location).Path
New-Item -ItemType Directory -Force Logs | Out-Null
& $Unity -projectPath $Project
```

In the opened Editor: wait for Package Manager/import/compilation; ensure the Console has no errors; run **VEILBREAK → Generate Veil Arena** outside Play Mode; open `Assets/VEILBREAK/Scenes/VeilArena.unity`; run EditMode and PlayMode test suites; inspect Player settings (legacy input, Android app category Game, landscape); then build to Android. Record the exact version, package resolution, test counts and build result in the audit log. Do not report a successful Unity run until it is actually performed on that machine.

The command-line equivalents and APK/device acceptance gates are in [ReleaseCandidate.md](ReleaseCandidate.md). The exact Editor executable must be used for all future project validation unless a verified incompatibility is documented and reviewed. Do not create a Unity starter project or blank SampleScene.

## References

- [Unity 6.3 URP requirements and Editor compatibility](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/requirements.html)
- [URP 17.3 changelog](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.3/changelog/CHANGELOG.html)
- [Unity 6.3 upgrade guide](https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuideUnity63.html)
- [Unity 6.3 Android large-screen behavior and App Category](https://docs.unity3d.com/6000.3/Documentation/Manual/android-large-screen-and-foldable-support.html)
- [PlayerSettings.Android.appCategory scripting API](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PlayerSettings.Android-appCategory.html)
- [Unity 6000.3.26f1 release notes](https://unity.com/releases/editor/whats-new/6000.3.26f1)
