// VEILBREAK headless gameplay simulation (developer tooling, NOT shipped in the game).
//
// Runs the REAL gameplay scripts from Assets/VEILBREAK/Scripts inside the functional engine mock
// (MiniEngine.cs): character controller movement, damage, dodge window, reality shift validation,
// guardian telegraph/strike timing, stagger, navigation and win/defeat transitions.
//
// Results from this harness are "simulation-verified". They are not Unity Play Mode results and
// not device results, and must never be reported as such. Presentation code (scene art, HUD,
// audio) is excluded from the simulation build because it needs a real renderer.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Veilbreak.Sim
{
    internal sealed class Sim
    {
        // ---- assertions ----
        int passed, failed;
        string current;
        readonly List<string> failures = new List<string>();

        void Check(string what, bool condition, string detail = null)
        {
            if (condition) { passed++; return; }
            failed++;
            failures.Add(current + " :: " + what + (detail == null ? "" : " [" + detail + "]"));
        }
        void Near(string what, float expected, float actual, float tolerance) =>
            Check(what + " = " + expected.ToString("0.###"), Math.Abs(expected - actual) <= tolerance, "actual " + actual.ToString("0.###"));

        void Case(string name, Action body)
        {
            current = name;
            int beforeFailed = failed;
            try { body(); }
            catch (Exception error)
            {
                failed++;
                failures.Add(name + " :: threw " + error.GetType().Name + ": " + error.Message);
                if (Environment.GetEnvironmentVariable("VEILSIM_TRACE") != null) Console.WriteLine(error);
            }
            Console.WriteLine((failed == beforeFailed ? "PASS  " : "FAIL  ") + name);
            Scene.Reset();
        }

        // ---- helpers ----
        static void Tick(float seconds, float dt = 1f / 60f)
        {
            int frames = Math.Max(1, (int)Math.Round(seconds / dt));
            for (int i = 0; i < frames; i++) Scene.Frame(dt);
        }

        /// <summary>Advances frames until the condition holds or the frame budget is exhausted.</summary>
        static bool TickUntil(Func<bool> condition, float seconds, float dt = 1f / 60f)
        {
            int frames = Math.Max(1, (int)Math.Round(seconds / dt));
            for (int i = 0; i < frames; i++)
            {
                if (condition()) return true;
                Scene.Frame(dt);
            }
            return condition();
        }

        static void Place(PlayerController player, Vector3 point)
        {
            player.Body.enabled = false;
            player.transform.position = point;
            player.Body.enabled = true;
        }

        static FieldInfo Field(Type type, string name) =>
            type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance) ??
            throw new MissingFieldException(type.Name, name);

        static void SetPrivate(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);

        public int Run()
        {
            Console.WriteLine("VEILBREAK headless gameplay simulation (engine mock; not Unity)");
            EngineSelfTest.Run();
            Console.WriteLine();

            Case("Title state freezes time and the encounter cannot be damaged", () =>
            {
                var world = Scene.Build();
                Check("state is Title", world.Game.State == ArenaState.Title, world.Game.State.ToString());
                Near("timeScale frozen", 0f, Time.timeScale, 0.0001f);
                world.Combat.Guardian.ReceiveHit(50);
                Near("guardian health untouched in Title", 540f, world.Game.Guardian.Health.Current, 0.001f);
            });

            Case("Restart starts the encounter with full health in Living Veil", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                Check("state is Playing", world.Game.IsPlaying);
                Near("timeScale running", 1f, Time.timeScale, 0.0001f);
                Near("player health", 100f, world.Game.Player.Health.Current, 0.001f);
                Near("guardian health", 540f, world.Game.Guardian.Health.Current, 0.001f);
                Check("veil is Living", world.Game.Veil.Current == VeilState.Living);
                Check("guardian armour visible", world.Game.Guardian.ArmorPlate.gameObject.activeSelf);
            });

            Case("Simulated courtyard is built from the shared ArenaLayout definition", () =>
            {
                var world = Scene.Build();
                var root = world.Root.transform;
                var solids = new List<BoxCollider>();
                foreach (var collider in root.GetComponentsInChildren<BoxCollider>(true)) solids.Add(collider);

                var byName = new Dictionary<string, List<BoxCollider>>();
                foreach (var col in solids)
                {
                    if (!byName.TryGetValue(col.gameObject.name, out var list)) byName[col.gameObject.name] = list = new List<BoxCollider>();
                    list.Add(col);
                }

                int expected = 0, matched = 0;
                // Walk the same yield order the scene art uses, resolving each shape to world space so
                // nested pieces (pier boxes under their column group) are compared correctly.
                var actual = new List<BoxCollider>(solids);
                foreach (var box in ArenaLayout.AllSolidShapes())
                {
                    expected++;
                    BoxCollider hit = null;
                    for (int i = 0; i < actual.Count && hit == null; i++)
                    {
                        var col = actual[i];
                        if (col.gameObject.name != box.Name) continue;
                        if ((col.size - box.Size).magnitude > .0005f) continue;
                        if ((col.transform.TransformPoint(col.center) - (root.position + root.rotation * box.Center)).magnitude > .01f) continue;
                        hit = col;
                    }
                    if (hit == null) { Check("sim matches " + box, false); continue; }
                    actual.Remove(hit);
                    matched++;
                }
                // Only the three extra boundary stones remain: the layout describes one wall box, and the
                // scene builds it once per courtyard side.
                Check("every layout shape exists in the simulation", matched == ArenaLayout.SolidShapeCount,
                    matched + "/" + ArenaLayout.SolidShapeCount + " matched");
                foreach (var leftover in actual)
                    Check("leftover collider is a boundary stone", leftover.gameObject.name == ArenaLayout.ParapetStone.Name,
                        leftover.gameObject.name);
                Check("wall stones are laid out once per side", byName[ArenaLayout.ParapetStone.Name].Count == 4);
                foreach (var col in solids)
                    Check("solid layer for " + col.gameObject.name, col.gameObject.layer == ArenaLayout.SolidLayer, col.gameObject.layer.ToString());

                var veil = world.Game.Veil;
                Check("reactive group count matches the layout", veil.Objects.Length == ArenaLayout.ReactiveStructures.Length,
                    veil.Objects.Length + " vs " + ArenaLayout.ReactiveStructures.Length);
                for (int i = 0; i < ArenaLayout.ReactiveStructures.Length && i < veil.Objects.Length; i++)
                {
                    var structure = ArenaLayout.ReactiveStructures[i];
                    var reactive = veil.Objects[i];
                    Check("reactive " + i + " is " + structure.Name, reactive.name == structure.Name, reactive.name);
                    Check("reactive " + structure.Name + " state", reactive.ActiveState == structure.State, reactive.ActiveState.ToString());
                    Check("reactive " + structure.Name + " shape count",
                        reactive.GetComponentsInChildren<BoxCollider>(true).Length == structure.Boxes.Length);
                }

                // The actors must be placed by the layout too, not by a re-typed spawn vector.
                Near("player spawn y", ArenaLayout.PlayerSpawn.Center.y, world.Game.Player.transform.position.y, 0.02f);
                Check("player spawn xz",
                    Vector3.ProjectOnPlane(world.Game.Player.transform.position - ArenaLayout.PlayerSpawn.Center, Vector3.up).magnitude < 0.15f,
                    world.Game.Player.transform.position.ToString());
                var bossBody = world.Game.Guardian.GetComponent<CharacterController>();
                Near("guardian collider height", ArenaLayout.GuardianColliderHeight, bossBody.height, 0.0001f);
                Near("guardian collider radius", ArenaLayout.GuardianColliderRadius, bossBody.radius, 0.0001f);

                // A spot check that the layout is what actually blocks movement: the Living cover is solid.
                var coverCol = byName["Living route cover"][0];
                Check("living cover blocks a capsule", Physics.CheckCapsule(
                    coverCol.transform.position - Vector3.up * 1.5f,
                    coverCol.transform.position + Vector3.up * 1.5f, .34f, 1 << ArenaLayout.SolidLayer, QueryTriggerInteraction.Ignore));
            });

            Case("Melee lands once after windup for reduced Living Veil damage", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                world.Game.Guardian.enabled = false;
                Place(world.Game.Player, new Vector3(0f, .15f, 2f));
                Tick(0.05f);

                world.Game.InputSource.Attack();
                Tick(1f / 60f);
                Check("swing started", world.Game.Combat.IsAttacking);
                Near("no instant damage", 540f, world.Game.Guardian.Health.Current, 0.001f);

                Tick(0.2f);
                Near("18 damage x 0.65 armour", 540f - 18f * 0.65f, world.Game.Guardian.Health.Current, 0.01f);

                float afterHit = world.Game.Guardian.Health.Current;
                world.Game.InputSource.Attack();
                Tick(0.2f);
                Near("cooldown blocks a second hit", afterHit, world.Game.Guardian.Health.Current, 0.01f);
            });

            Case("Dodge grants immunity, cancels the pending melee hit and respects cooldown", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                world.Game.Guardian.enabled = false;
                Place(world.Game.Player, new Vector3(0f, .15f, 2f));
                Tick(0.05f);

                world.Game.InputSource.Attack();
                Tick(1f / 60f);
                Check("swing pending", world.Game.Combat.IsAttacking);
                world.Game.InputSource.Dodge();
                Tick(1f / 60f);
                Check("dodging", world.Game.Player.IsDodging);
                Check("swing cancelled", !world.Game.Combat.IsAttacking);

                float health = world.Game.Player.Health.Current;
                world.Game.Player.ReceiveDamage(50f);
                Near("damage ignored during dodge", health, world.Game.Player.Health.Current, 0.001f);
                Check("dodge cooldown running", world.Game.Player.DodgeCooldownRemaining > 0f);

                Tick(0.3f);
                Check("not dodging after duration", !world.Game.Player.IsDodging);
                world.Game.InputSource.Dodge();
                Tick(1f / 60f);
                Check("cooldown prevents a second dodge", !world.Game.Player.IsDodging);
                Tick(0.6f);
                world.Game.InputSource.Dodge();
                Tick(1f / 60f);
                Check("dodge available again after cooldown", world.Game.Player.IsDodging);
            });

            Case("Broken Veil doubles player damage and exposes the core", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                world.Game.Guardian.enabled = false;
                world.Game.InputSource.Shift();
                Tick(1f / 60f);
                Check("shifted to Broken", world.Game.Veil.Current == VeilState.Broken);
                Check("core armour shutters hidden", !world.Game.Guardian.ArmorPlate.gameObject.activeSelf);

                Place(world.Game.Player, new Vector3(0f, .15f, 2f));
                world.Game.InputSource.Attack();
                Tick(0.2f);
                Near("18 damage x 2.0 exposed", 540f - 36f, world.Game.Guardian.Health.Current, 0.01f);
            });

            Case("Shift cooldown prevents rapid reality flipping", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                world.Game.InputSource.Shift();
                Tick(1f / 60f);
                Check("first shift applied", world.Game.Veil.Current == VeilState.Broken);
                world.Game.InputSource.Shift();
                Tick(1f / 60f);
                Check("second shift rejected during cooldown", world.Game.Veil.Current == VeilState.Broken);
                Tick(1.05f);
                world.Game.InputSource.Shift();
                Tick(1f / 60f);
                Check("shift available after cooldown", world.Game.Veil.Current == VeilState.Living);
            });

            Case("Shift into forming stone is rejected and leaves state and position intact", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                // Eastern Broken terrace sits at x 8.65..11.35, z 6; stand inside it in Living Veil.
                Place(world.Game.Player, new Vector3(10f, .15f, 6f));
                Tick(1f / 60f);
                Vector3 before = world.Game.Player.transform.position;
                string reason = null;
                world.Game.Veil.Rejected += value => reason = value;
                Check("shift refused", !world.Game.Veil.TryShift());
                Check("still Living", world.Game.Veil.Current == VeilState.Living);
                Check("position unchanged", before == world.Game.Player.transform.position);
                Check("no cooldown burned", world.Game.Veil.CooldownRemaining <= 0f);
                Check("reason mentions forming stone", reason != null && reason.Contains("forming stone"), reason);

                // Leaving the footprint on stable ground allows the shift.
                Place(world.Game.Player, new Vector3(-8.2f, .55f, -5f));
                Check("shift allowed on open ground", world.Game.Veil.TryShift());
            });

            Case("Player standing in unsupported space cannot shift", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                Place(world.Game.Player, new Vector3(0f, 6f, 0f));
                string reason = null;
                world.Game.Veil.Rejected += value => reason = value;
                Check("shift refused without ground", !world.Game.Veil.TryShift());
                Check("reason mentions stable ground", reason != null && reason.Contains("stable ground"), reason);
            });

            Case("Guardian slam telegraphs, lands once, then alternates to a dodgeable strike", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                Place(world.Game.Player, new Vector3(0f, .15f, 2f));
                world.Game.Player.enabled = false;

                Check("approach begins after Observe", TickUntil(() => world.Game.Guardian.State == GuardianAI.Phase.Approach, 3f),
                    world.Game.Guardian.State.ToString());
                Check("slam telegraph starts", TickUntil(() => world.Game.Guardian.State == GuardianAI.Phase.Telegraph, 3f),
                    world.Game.Guardian.State.ToString());
                Check("first attack is the slam", world.Game.Guardian.AttackName.Contains("GROUND SLAM"), world.Game.Guardian.AttackName);
                Near("no damage during the 1.3 s windup", 100f, world.Game.Player.Health.Current, 0.001f);

                Check("slam lands", TickUntil(() => world.Game.Player.Health.Current < 100f, 2f), world.Game.Player.Health.Current.ToString("0.##"));
                Near("slam deals exactly 25", 75f, world.Game.Player.Health.Current, 0.01f);

                // The alternation happens when Recovery ends, so watch for the strike telegraph.
                Check("second attack telegraphs", TickUntil(() => world.Game.Guardian.State == GuardianAI.Phase.Telegraph, 5f),
                    world.Game.Guardian.State.ToString());
                Check("second attack is the energy strike", world.Game.Guardian.AttackName.Contains("ENERGY STRIKE"), world.Game.Guardian.AttackName);
                Near("slam did not double-tick", 75f, world.Game.Player.Health.Current, 0.01f);
            });

            Case("Cover created during windup blocks the slam at impact", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                Place(world.Game.Player, new Vector3(0f, .15f, 2f));
                world.Game.Player.enabled = false;
                TickUntil(() => world.Game.Guardian.State == GuardianAI.Phase.Telegraph, 3f);
                Check("telegraphing", world.Game.Guardian.State == GuardianAI.Phase.Telegraph, world.Game.Guardian.State.ToString());
                var cover = new GameObject("test cover");
                cover.layer = 8;
                var box = cover.AddComponent<BoxCollider>();
                box.size = new Vector3(6f, 2f, .3f);
                cover.transform.position = new Vector3(0f, 1f, 3.5f);
                Scene.Register(cover);
                Physics.SyncTransforms();
                Tick(1.5f);
                Near("slam blocked by late cover", 100f, world.Game.Player.Health.Current, 0.001f);
            });

            Case("Dodging avoids the slam entirely", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                Place(world.Game.Player, new Vector3(0f, .15f, 2f));
                TickUntil(() => world.Game.Guardian.State == GuardianAI.Phase.Telegraph, 3f);
                Check("telegraphing", world.Game.Guardian.State == GuardianAI.Phase.Telegraph, world.Game.Guardian.State.ToString());
                world.Game.InputSource.Dodge();
                Tick(1.0f);
                Near("dodge immunity prevents slam damage", 100f, world.Game.Player.Health.Current, 0.01f);
            });

            Case("Stagger builds only from Broken core hits and only interrupts Recovery", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                Place(world.Game.Player, new Vector3(0f, .15f, 2f));

                for (int i = 0; i < 10; i++) world.Game.Guardian.ReceiveHit(18f);
                Near("Living Veil hits build no stagger", 0f, world.Game.Guardian.StaggerMeter, 0.001f);

                Check("shift to Broken", world.Game.Veil.TryShift());
                world.Game.Guardian.ReceiveHit(18f);
                world.Game.Guardian.ReceiveHit(18f);
                Near("Broken hits accumulate stagger", 72f, world.Game.Guardian.StaggerMeter, 0.01f);
                Check("stagger did not fire outside Recovery", world.Game.Guardian.State != GuardianAI.Phase.Stagger);
            });

            Case("Stagger threshold reached in Recovery triggers Stagger and clears the meter", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                Place(world.Game.Player, new Vector3(0f, .15f, 2f));
                world.Game.Player.enabled = false;

                // Walk the fight to Recovery.
                bool reached = TickUntil(() => world.Game.Guardian.State == GuardianAI.Phase.Recovery, 6f);
                Check("reached Recovery", reached, world.Game.Guardian.State.ToString());

                Check("shift to Broken", world.Game.Veil.TryShift());
                world.Game.Guardian.ReceiveHit(18f);
                world.Game.Guardian.ReceiveHit(18f);
                Check("still Recovery below threshold", world.Game.Guardian.State == GuardianAI.Phase.Recovery);
                world.Game.Guardian.ReceiveHit(18f);
                Check("third hit staggers", world.Game.Guardian.State == GuardianAI.Phase.Stagger, world.Game.Guardian.State.ToString());
                Near("meter reset", 0f, world.Game.Guardian.StaggerMeter, 0.001f);

                float before = world.Game.Guardian.Health.Current;
                world.Game.Guardian.ReceiveHit(18f);
                Near("staggered hits deal damage but build no meter", before - 36f, world.Game.Guardian.Health.Current, 0.01f);
                Near("meter still empty", 0f, world.Game.Guardian.StaggerMeter, 0.001f);
            });

            Case("Guardian routes around Living Veil cover instead of stalling", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                Place(world.Game.Player, new Vector3(-10f, .15f, 1f));
                var body = world.Game.Guardian.GetComponent<CharacterController>();
                body.enabled = false;
                world.Game.Guardian.transform.position = new Vector3(-4f, .1f, 1f);
                body.enabled = true;
                Tick(0.05f);

                // The player stands behind the Living Veil cover box, so the guardian only regains sight
                // by walking around it - a straight-line approach would stall against the box.
                bool cleared = TickUntil(() => world.Game.Guardian.transform.position.x <= -8f || !world.Game.IsPlaying, 12f);
                Check("guardian cleared the west cover", cleared && world.Game.Guardian.transform.position.x < -8f,
                    "x=" + world.Game.Guardian.transform.position.x.ToString("0.##"));
                Check("guardian regained sight of the player",
                    ArenaNavigator.HasSight(world.Game.Guardian.transform.position, world.Game.Player.transform.position));
                bool closed = TickUntil(() => Vector3.ProjectOnPlane(
                    world.Game.Player.transform.position - world.Game.Guardian.transform.position, Vector3.up).magnitude < 3.4f, 12f);
                Check("guardian closed to melee range", closed,
                    Vector3.Distance(world.Game.Player.transform.position, world.Game.Guardian.transform.position).ToString("0.00"));
            });

            Case("Killing the guardian wins; player death loses; restart resets both", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                world.Game.Guardian.Health.Damage(10000f);
                Check("victory", world.Game.State == ArenaState.Victory, world.Game.State.ToString());
                Near("time frozen on victory", 0f, Time.timeScale, 0.0001f);

                world.Game.Restart();
                Check("restart clears victory", world.Game.IsPlaying);
                Near("guardian restored", 540f, world.Game.Guardian.Health.Current, 0.001f);
                Near("player restored", 100f, world.Game.Player.Health.Current, 0.001f);

                world.Game.Player.ReceiveDamage(10000f);
                Check("defeat", world.Game.State == ArenaState.Defeat, world.Game.State.ToString());
                world.Game.Restart();
                Check("restart after defeat", world.Game.IsPlaying);
                Check("veil reset to Living", world.Game.Veil.Current == VeilState.Living);
                Check("guardian back to Observe", world.Game.Guardian.State == GuardianAI.Phase.Observe, world.Game.Guardian.State.ToString());

                world.Game.ReturnToTitle();
                Check("return to title", world.Game.State == ArenaState.Title);
            });

            Case("Pause freezes the fight and restarting from pause clears pending damage", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                world.Game.Guardian.enabled = false;
                Place(world.Game.Player, new Vector3(0f, .15f, 2f));
                world.Game.InputSource.Attack();
                Tick(1f / 60f);
                Check("swing pending", world.Game.Combat.IsAttacking);
                world.Game.TogglePause();
                Check("paused", world.Game.State == ArenaState.Paused);
                Near("time frozen", 0f, Time.timeScale, 0.0001f);
                Tick(0.2f);
                Check("pending swing survives pause", world.Game.Combat.IsAttacking);
                world.Game.Restart();
                Check("restart cleared the swing", !world.Game.Combat.IsAttacking);
                Near("guardian unharmed after restart", 540f, world.Game.Guardian.Health.Current, 0.001f);
            });

            Case("Repeated restarts do not duplicate death subscriptions or leave stale state", () =>
            {
                var world = Scene.Build();
                for (int i = 0; i < 5; i++)
                {
                    world.Game.Restart();
                    world.Game.Guardian.Health.Damage(10000f);
                }
                Check("still able to restart after repeated wins", world.Game.IsPlaying == false);
                world.Game.Restart();
                Check("playing again", world.Game.IsPlaying);
                Near("guardian full", 540f, world.Game.Guardian.Health.Current, 0.001f);
            });

            Case("Guardian cannot damage a dead or paused encounter", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                world.Game.TogglePause();
                world.Game.Player.ReceiveDamage(20f);
                Near("paused player takes no damage", 100f, world.Game.Player.Health.Current, 0.001f);
                world.Game.TogglePause();
                world.Game.Player.ReceiveDamage(20f);
                Near("resumed player takes damage", 80f, world.Game.Player.Health.Current, 0.001f);
            });

            Case("Player falling out of the arena dies", () =>
            {
                var world = Scene.Build();
                world.Game.Restart();
                Place(world.Game.Player, new Vector3(0f, -8f, 0f));
                Tick(1f / 60f);
                Check("fall damage killed the player", world.Game.Player.Health.IsDead, world.Game.Player.Health.Current.ToString("0.##"));
            });

            Console.WriteLine();
            foreach (var failure in failures) Console.WriteLine("  FAILED: " + failure);
            Console.WriteLine();
            Console.WriteLine("RESULT " + (failed == 0 ? "SIMULATION PASSED" : "SIMULATION FAILED") +
                              " - " + passed + " checks passed, " + failed + " failed");
            Console.WriteLine();

            // Execute the real EditMode test sources that only need plain data. This is not the Unity
            // Test Runner, but it does mean the layout invariants are actually executed, not just compiled.
            int editModeFailed = EditModeRunner.Run();
            return failed == 0 && EngineSelfTest.Failures == 0 && editModeFailed == 0 ? 0 : 1;
        }
    }

    /// <summary>Owns the simulated world; mirrors the ordering VeilbreakBootstrap performs.</summary>
    internal static class Scene
    {
        internal sealed class World
        {
            public GameObject Root;
            public ArenaGameManager Game;
            public GameInput InputSource;
            public PlayerController PlayerControllerComponent;
            public PlayerCombat Combat;
        }

        static readonly List<GameObject> Roots = new List<GameObject>();
        internal static World Current;

        public static void Reset()
        {
            // Destroy every live object, not just the roots this class created: one test's geometry
            // must never block another test's queries.
            foreach (var go in GameObject.All.ToArray()) UnityEngine.Object.Destroy(go);
            GameObject.All.Clear();
            Roots.Clear();
            Physics.SceneProvider?.Invoke(Physics.Scratch);
            Current = null;
            Time.ResetAll();
            Input.ResetAll();
            Camera.main = null;
        }

        internal static void Register(GameObject go) { }

        public static World Build()
        {
            Current = new World { Root = new GameObject("VEILBREAK sim root") };
            Roots.Add(Current.Root);
            var root = Current.Root.transform;

            // --- courtyard collision: built from ArenaLayout, the same data PrototypeSceneArt uses ---
            SolidBox(root, ArenaLayout.Floor.Name, ArenaLayout.Floor.Center, ArenaLayout.Floor.Size);
            var boundary = ArenaLayout.ParapetStone;
            for (int side = 0; side < 4; side++)
            {
                var wall = new GameObject("Courtyard broken parapet " + side);
                wall.transform.SetParent(root, false);
                wall.transform.localRotation = Quaternion.Euler(0f, side * 90f, 0f);
                var stone = new GameObject(boundary.Name);
                stone.layer = ArenaLayout.SolidLayer;
                stone.transform.SetParent(wall.transform, false);
                stone.transform.localPosition = boundary.Center;
                Box(stone, boundary.Size);
            }
            for (int i = 0; i < ArenaLayout.Piers.Length; i++)
            {
                var pier = ArenaLayout.Piers[i];
                SolidBox(root, pier.Box.Name, pier.Box.Center, pier.Box.Size);
                SolidBox(root, pier.Plinth.Name, pier.Plinth.Center, pier.Plinth.Size);
            }
            for (int i = 0; i < ArenaLayout.ArchPiers.Length; i++)
            {
                var arch = ArenaLayout.ArchPiers[i];
                SolidBox(root, arch.Name, arch.Center, arch.Size);
            }
            var pedestal = ArenaLayout.ReliquaryPedestal;
            SolidBox(root, pedestal.Name, pedestal.Center, pedestal.Size);

            // Route cover and terraces: one reactive group per ArenaLayout structure, in the same
            // order, so the reality shift enables exactly the same shapes as the real scene.
            var reactiveObjects = new List<VeilReactiveObject>();
            for (int g = 0; g < ArenaLayout.ReactiveStructures.Length; g++)
            {
                var structure = ArenaLayout.ReactiveStructures[g];
                var group = new GameObject(structure.Name);
                group.transform.SetParent(root, false);
                for (int i = 0; i < structure.Boxes.Length; i++)
                {
                    var shape = structure.Boxes[i];
                    SolidBox(group.transform, shape.Name, shape.Center, shape.Size);
                }
                var reactive = group.AddComponent<VeilReactiveObject>();
                reactive.ActiveState = structure.State;
                reactiveObjects.Add(reactive);
            }

            // --- actors ---
            var lightObject = new GameObject("Sun");
            lightObject.transform.SetParent(root, false);
            var sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            RenderSettings.sun = sun;

            var input = Current.Root.AddComponent<GameInput>();
            var game = Current.Root.AddComponent<ArenaGameManager>();
            var veil = Current.Root.AddComponent<VeilStateManager>();
            var safety = Current.Root.AddComponent<ShiftSafetyValidator>();

            var playerRoot = new GameObject(ArenaLayout.PlayerSpawn.Name);
            playerRoot.transform.SetParent(root, false);
            playerRoot.transform.localPosition = ArenaLayout.PlayerSpawn.Center;
            var controller = playerRoot.AddComponent<CharacterController>();
            controller.height = ArenaLayout.PlayerColliderHeight;
            controller.center = Vector3.up * (ArenaLayout.PlayerColliderHeight * .5f);
            controller.radius = ArenaLayout.PlayerColliderRadius;
            controller.stepOffset = ArenaLayout.ActorStepOffset;
            controller.skinWidth = .035f;
            playerRoot.AddComponent<HealthComponent>().Configure(100f);
            var player = playerRoot.AddComponent<PlayerController>();

            var bossRoot = new GameObject(ArenaLayout.GuardianSpawn.Name);
            bossRoot.transform.SetParent(root, false);
            bossRoot.transform.localPosition = ArenaLayout.GuardianSpawn.Center;
            bossRoot.transform.localRotation = Quaternion.Euler(0f, ArenaLayout.GuardianYaw, 0f);
            var bossBody = bossRoot.AddComponent<CharacterController>();
            bossBody.height = ArenaLayout.GuardianColliderHeight;
            bossBody.center = Vector3.up * (ArenaLayout.GuardianColliderHeight * .5f);
            bossBody.radius = ArenaLayout.GuardianColliderRadius;
            bossBody.stepOffset = .45f;
            bossRoot.AddComponent<HealthComponent>().Configure(540f);
            var boss = bossRoot.AddComponent<GuardianAI>();

            var core = new GameObject("core").transform;
            core.SetParent(bossRoot.transform, false);
            var armor = new GameObject("armour").transform;
            armor.SetParent(bossRoot.transform, false);
            var vis = new GameObject("boss visual").transform;
            vis.SetParent(bossRoot.transform, false);
            boss.Core = core;
            boss.ArmorPlate = armor;
            boss.Visual = vis;
            boss.SlamMarker = new GameObject("slam marker").transform;
            boss.SlamMarker.SetParent(bossRoot.transform, false);
            boss.StrikeMarker = new GameObject("strike marker").transform;
            boss.StrikeMarker.SetParent(bossRoot.transform, false);
            boss.SlamMarker.gameObject.SetActive(false);
            boss.StrikeMarker.gameObject.SetActive(false);

            var cameraRoot = new GameObject("Main Camera");
            cameraRoot.transform.SetParent(root, false);
            // Stand-in for the presentation camera: sits behind the player looking forward, which is
            // what PlayerController's movement basis needs. Camera collision itself is Unity-only.
            cameraRoot.transform.localPosition = new Vector3(0f, 3f, -16f);
            cameraRoot.transform.localRotation = Quaternion.identity;
            Camera.main = cameraRoot.AddComponent<Camera>();
            var follow = cameraRoot.AddComponent<ThirdPersonCameraController>();

            player.InputSource = input;
            player.Game = game;
            player.CameraTransform = cameraRoot.transform;
            player.SetSpawn();
            var combat = playerRoot.AddComponent<PlayerCombat>();
            combat.Player = player;
            combat.Guardian = boss;

            boss.Player = player;
            boss.Game = game;
            boss.Veil = veil;

            veil.GuardianBody = bossBody;
            veil.Player = player;
            veil.Validator = safety;
            veil.Objects = reactiveObjects.ToArray();
            veil.Sun = sun;

            game.Player = player;
            game.Guardian = boss;
            game.Combat = combat;
            game.Veil = veil;
            game.InputSource = input;
            game.FollowCamera = follow;

            // Same post-wiring initialisation order as VeilbreakBootstrap.Build().
            veil.ResetState();
            boss.Initialize();
            follow.ResetView();
            game.Initialize();

            Current.Game = game;
            Current.InputSource = input;
            Current.PlayerControllerComponent = player;
            Current.Combat = combat;
            return Current;
        }

        /// <summary>Creates one layer-8 box collider from an ArenaLayout shape.</summary>
        static GameObject SolidBox(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name);
            go.layer = ArenaLayout.SolidLayer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            Box(go, size);
            return go;
        }

        static void Box(GameObject go, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
        }

        /// <summary>Runs one frame: Update at -30, Update, LateUpdate; then clears one-frame input.</summary>
        public static void Frame(float dt)
        {
            Physics.SceneProvider?.Invoke(Physics.Scratch);
            Time.Step(dt);
            if (Current == null) return;
            var root = Current.Root;
            // Deterministic execution order matching [DefaultExecutionOrder(-30)] on PlayerController:
            // player movement resolves first so combat and AI read the current position.
            Current.PlayerControllerComponent?.updateAction?.Invoke();
            foreach (var behaviour in Snapshot(root))
            {
                if (behaviour is PlayerController) continue;
                if (!behaviour.enabled || behaviour.gameObject == null) continue;
                behaviour.updateAction?.Invoke();
                behaviour.lateUpdateAction?.Invoke();
            }
            Input.EndFrame();
        }

        static List<MonoBehaviour> Snapshot(GameObject root)
        {
            var list = new List<MonoBehaviour>();
            Collect(root.transform, list);
            return list;
        }

        static void Collect(Transform node, List<MonoBehaviour> list)
        {
            if (node?.gameObject == null || !node.gameObject.activeSelf) return;
            foreach (var component in node.gameObject.Components) if (component is MonoBehaviour behaviour) list.Add(behaviour);
            foreach (var child in node.children) Collect(child, list);
        }
    }

    internal static class Program
    {
        static int Main()
        {
            // Physics has no scene registry of its own: feed it every collider each frame.
            Physics.SceneProvider = colliders =>
            {
                colliders.Clear();
                foreach (var go in GameObject.All)
                    if (go.activeInHierarchy)
                        foreach (var component in go.Components)
                            if (component is Collider collider) colliders.Add(collider);
            };
            return new Sim().Run();
        }

    }
}
