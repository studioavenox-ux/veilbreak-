# Post-merge source audit and repair — 2026-10-21

## Baseline and method
- Clean starting checkout on `arena/b0e1f861-veilbreak`, based on `c1d1b91` (PR #1's merge into main).
- PR #1 is confirmed MERGED through GitHub. Reviewed its changed-file list and the current runtime, Editor, test, package, metadata and input files.
- Parsed the attached `veilbreak_audit_bundle.txt` into 78 named files and compared each with the checkout: **78/78 match after stripping surrounding whitespace**, none missing or different. The attachment is a source snapshot, not a Unity build or an independent executed test report. No older files were copied over the repository.
- Kept the single-project, code-first design. Saving a scene with one bootstrap, then constructing the arena on Play, is intentional. An absent generated `.unity` file is not itself a defect.

## Confirmed defects and focused repairs
| Evidence in original source | Repair |
| --- | --- |
| Runtime asmdef references `Unity.ugui`; Unity's 6000.0 / Unity UI 2.0.0 source declares its assembly as `UnityEngine.UI`. The unresolved assembly prevents compilation. | Use `UnityEngine.UI`; tests exercising UI explicitly reference it too. Add known-package assembly reference validation and a negative regression for the old typo. |
| `GameInput.Clear` resets only the joystick and command buffer, while `TouchLookArea` keeps its pointer until pointer-up or disable. Focus loss calls Clear but does not necessarily disable the UI or deliver pointer-up. | Wire look area into the input source and reset ownership on Clear. Add synthetic-pointer EditMode regression. |
| HUD messages last 2.6 s, and attack warning text is only updated once that message expires, longer than the 1.3 s windup. | Telegraph warnings have priority over startup/shift messages, including immediate message callbacks; pending messages can return after the warning. Add PlayMode regression. |
| Generator ignores `SaveScene`'s boolean and always registers/logs success. It is also callable while playing. | Check save success before updating build scenes; disable menu during Play/transition and reject direct batch calls then. |
| Generator only creates a renderer when pipeline is missing. Existing pipeline with deleted renderer is never repaired; surviving renderer is not reused when pipeline is recreated. | Load/reuse both assets independently and fill a missing renderer slot without replacing the pipeline. Guard asset-type conflicts. |
| Generator replaces every build entry, leaves the last quality level selected, and indexes settings arrays without validating availability. | Preserve unrelated build entries, register arena once at index zero, restore active quality, validate required settings/properties/shader and reject conflicting layer 8. Configure vSync for each quality level rather than just the last. |
| Source checker flags any local Library/Logs directory as an error, even if ignored, making it unusable after normal Editor import. | Reject tracked caches rather than legitimate ignored local folders. Add positive and negative Python regressions. |

## Foundation and gameplay reviewed; not rewritten
- Version remains Unity 6000.0.38f1. Package pins remain URP 17.0.3, Unity UI 2.0.0 and Test Framework 1.4.5. No speculative package upgrades.
- Legacy mouse axes and StandaloneInputModule navigation axes exist. The generator selects the legacy backend. Desktop look's transparent surface is already non-raycast, so blank-screen LMB isn't swallowed; retained that existing fix.
- URP creation uses Editor-only APIs, assigns global and per-quality pipeline, includes runtime Lit shader, and sets landscape Android / ARM64 / IL2CPP / API 26. Generator menu remains **VEILBREAK → Generate Veil Arena**. No fake scene/settings YAML was added.
- Bootstrap creates player health/controllers before initialization, explicitly wires actor/camera/reality/HUD references, and builds all art from available primitives. All existing GUIDs were preserved; new test script has a unique meta.
- Movement, gravity, camera pitch/obstruction checks, melee windup/one-hit cooldown, dodge, health events, both guardian attacks, recovery/stagger/defeat, and result transitions have meaningful source implementations. No new confirmed defect in those paths justified a rewrite.
- Reality objects switch whole GameObjects including colliders. Current safety validation checks incoming inactive geometry and nearby shared/destination support; no teleport is performed. Existing limitation: support validation is for this flat-floor/box-platform arena, not arbitrary destination terrain.
- Restart reuses instances and resets health/positions, melee window, dodge/shift timers, reality geometry, guardian state, camera and input. Event subscriptions are not added on each restart. No demonstrated subscription duplication was found.
- Direct steering around cover, primitive animation, basic camera framing, no audio and unmeasured mobile performance remain known prototype limitations, not newly invented regressions.

## Verification actually executed
- Tree-sitter C# syntax parse: **21 files passed** (includes one new EditMode fixture).
- JSON/XML, class/file names, known assembly reference names, GUID metadata: **passed**, 43 unique GUIDs.
- Python source-validator regressions: **4 passed**. These cover current sources, the original assembly typo, permitted ignored caches, and rejected tracked caches. They do not compile or run Unity code.
- `git diff --check`: passed.
- Unity's uGUI `6000.0` package source confirms version 2.0.0 and assembly name `UnityEngine.UI`; URP `6000.0/staging` source confirms the serialized renderer/settings field names and Editor creation API used. Source/API inspection is not binary compilation or exact installed-package resolution.

## Not executed / still required
Unity Editor, C# compiler with Unity references, Android SDK/NDK and hardware are unavailable in this sandbox. **No C# compilation, Editor generation, Play Mode, Unity EditMode/PlayMode tests, Android build/install, real multi-touch, or performance test has been run.** Missing execution is a verification gap, not proof of an additional source defect.

Open with the pinned Unity version, allow package import, generate the scene, run both Unity test suites and the manual acceptance list in `Development.md`. In particular, test generation twice, renderer-loss recovery, warning priority, camera focus loss, collision rejection, melee/dodge timing, both attacks, win/lose/restart and Android safe-area/multi-touch. Test any Android Build Profile scene-list overrides explicitly. Do not consider the runtime milestone accepted until these checks actually pass.
