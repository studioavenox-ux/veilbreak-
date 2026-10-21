# VEILBREAK
**AVENOX Games — Change Reality. Control the Fight.**

A Unity 6 / URP foundation for a single-player, landscape Android third-person action game. The first encounter, **The Veil Arena**, is a compact ruined courtyard with one mechanical Veil Guardian. Shift between Living and Broken versions of the same space to open routes and expose the guardian's core.

This is native Unity gameplay, not a web app. All character, enemy, and environment art is clearly identified procedural primitive placeholder geometry inspired by the four supplied concept boards. There are no imported models, rigs, animations, or pretend finished assets.

## Requirements
- **Unity 6000.0.38f1** (Unity 6.0), installed through Unity Hub.
- Android Build Support, Android SDK/NDK Tools, and OpenJDK for Android builds.
- Packages declared in `Packages/manifest.json`: **URP 17.0.3**, **Unity UI 2.0.0**, **Test Framework 1.4.5**, and built-in modules. No paid packages or runtime internet services.
- The Editor needs internet on first import to resolve Unity packages. The built game is offline.

## Open and run
1. Clone this repository and add its **root** to Unity Hub as an existing project. Do not create another project inside it.
2. Open with the version above and wait for package import/compilation.
3. Choose **VEILBREAK → Generate Veil Arena**. Save other work when prompted.
4. The utility configures URP, landscape Android defaults, the legacy input backend, environment layer 8, shader preservation, and the build scene list. If Unity requests an input-backend restart, allow it, then run the command again.
5. It saves **`Assets/VEILBREAK/Scenes/VeilArena.unity`** using Unity Editor APIs. This source checkout intentionally does **not** contain a hand-written scene or serialized URP assets. The generated scene has one `VeilbreakBootstrap` entry point; the arena is constructed **when entering Play Mode**, not in the edit-time Scene view.
6. Open that scene, enter **Play**, and select **Start Encounter** on the title overlay.

The generator replaces that scene, registers it once as the first enabled build scene, and preserves unrelated build-scene entries. Re-running it reuses the generated URP assets and repairs a missing renderer reference, but overwrites edits to the generated scene. Exit Play Mode before generating; the menu is disabled while playing. Runtime geometry changes do not persist when Play Mode ends. For tuning, edit the small serialized defaults in the relevant component or the bootstrap construction values, not the generated runtime objects.

Unity creates absent default project settings on first import (for example GraphicsSettings, QualitySettings, TagManager and PlayerSettings). These are not fabricated in this checkout. The generator applies the prototype defaults through Editor APIs and checks that required settings/shaders exist. Review and retain the Editor-generated essential configuration and package lock after local validation; do not commit Library/Temp/Logs. If layer 8 already has a different name, generation stops rather than overwriting it.

## Controls
| Action | Landscape touch | Editor / desktop |
|---|---|---|
| Move / strafe / back up | Bottom-left joystick | WASD, camera-relative |
| Rotate camera | Drag unused right-side area | Mouse movement |
| Melee | ATTACK | Left mouse, not over UI |
| Dodge | DODGE | Space |
| Change reality | SHIFT | Q |
| Pause / resume | Top-right II / Resume | Escape |

The joystick and camera each own a pointer ID; separate action buttons accept simultaneous fingers. Right-side camera dragging is behind buttons, so a button press is not also a camera gesture. Native UI scales to the screen and is inset by `Screen.safeArea`. Camera sensitivity is adjustable on the title/pause/result overlay. Mouse look is not locked, so UI remains reachable.

## Encounter rules
- Player: 100 health, 5 m/s movement, gravity, grounded collision, .27 s dodge with damage immunity and .85 s cooldown.
- Melee: 3.4 m center-to-center range, 75° half-angle, .14 s windup, .55 s cooldown, one damage check per swing, blocked by environment line of sight. Face the guardian before attacking.
- Living Veil: warm light, cyan energy, left gate/platform active, guardian core armored (0.65× base damage).
- Broken Veil: dark violet atmosphere, right gate/platform active, visibly exposed core (2× base damage). The full level footprint and player position are preserved.
- SHIFT: 1 s cooldown after success. A penetration check against incoming solid geometry and a support check reject unsafe changes with a HUD message. Losing a low step is safe because the permanent floor remains beneath it.
- Guardian: 540 health. Alternates a 4.2 m ground slam and a 10 m forward energy lane. Orange geometry and HUD text warn for 1.3 s before damage resolves once. Dodge away or sideways, then attack in the 1.6 s recovery. Broken-core hits accumulate toward a recovery stagger.
- Environmental cover blocks direct attack damage. No complex navigation system is present; lure the guardian back toward the clear center if it is caught on cover.
- Death of either combatant produces a real result state. Pause and results freeze simulation. Retry/replay restarts the existing instances and resets positions, health, reality objects, cooldowns, attack windows, camera view, and game flags. Return to Title is a same-scene menu equivalent.

See [arena layout](Documentation/VeilArena.md), [shift rules](Documentation/RealityRules.md), [guardian behavior](Documentation/Guardian.md), and [development/testing](Documentation/Development.md).

## Android build
1. Generate the scene first. Open **File → Build Profiles**, add/switch to **Android**, and include `VeilArena` (the generator enables it).
2. Defaults: package `com.avenoxgames.veilbreak`, landscape-left, minimum Android API 26, ARM64, IL2CPP. Keep target API automatic or select the SDK required for your distribution. Confirm the identifier and signing policy before publishing.
3. Use a Development Build for initial checks. Choose **Build** or **Build And Run** to your own ignored `Builds/Android/` directory. Device debugging/USB authorization must be configured locally.
4. Test safe areas, simultaneous touch, thermal behavior, frame time, and memory on physical hardware. Profile before adjusting target frame rate, render scale, or shadows.

URP defaults are modest: HDR off, 2× MSAA, one directional light, no additional-light shadows, 1024 main shadow map, 30 m shadow distance. No post-processing, particles, remote services, or per-attack spawning. The runtime uses 60 FPS as its cap; 30 FPS low-end / 60 FPS capable-device performance are **targets, not measured results**. Set the bootstrap target or lower render scale for device-specific trials.

## Implemented source vs. future work
**Implemented in source:** bootstrap and Editor scene generator; placeholder arena/actors; CharacterController movement and dodge; separate orbit/follow/collision camera; timed directional melee; health; shared-coordinate reality manager and reactive collision; unsafe-shift rejection; guardian finite-state machine and two telegraphed attacks; native touch HUD, health and cooldown display; title, pause, victory, defeat, and restart; Edit/Play Mode test suites.

**Not yet verified here:** Unity import/compilation, scene generation inside Unity, Play Mode behavior, Android build/install, real multi-touch, and performance. Unity Editor and Android hardware were unavailable in the implementation environment. Source syntax/static checks passed; this is **not** a claim that a Unity build passed. See [validation record](Documentation/Validation.md) and the [post-merge audit](Documentation/PostMergeAudit.md).

**Known prototype limitations:** primitive art without skeletal locomotion/attack animation, simple camera obstruction retraction rather than a full occlusion solver, direct approach steering rather than NavMesh pathfinding, basic UI and instantaneous visual shift, no sound/haptics, no saved settings, no jump or platforming progression. The boss is scaled to approximately 5 m for readability in this compact arena, not the concept's 8–10 m. Runtime-built materials keep the URP Lit shader always included, which may increase initial shader build time/size; replace with authored materials/variant collection when art stabilizes.

**Planned, not implemented:** production characters/rigs/VFX/audio, authored environment prefabs, device optimization and art pass. Other enemy concepts, campaigns, accounts, shops, multiplayer, inventories, and progression remain out of scope.
