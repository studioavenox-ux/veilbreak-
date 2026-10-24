using NUnit.Framework;
using UnityEngine;

namespace Veilbreak.Tests
{
    public sealed class HealthAndShiftTests
    {
        GameObject player, floor, obstruction, secondary, service;
        [TearDown] public void Cleanup()
        {
            foreach (var go in new[] { player, floor, obstruction, secondary, service }) if (go) Object.DestroyImmediate(go);
        }
        [Test] public void HealthClampsAndDeathFiresOnceUntilReset()
        {
            player = new GameObject("health test"); var health = player.AddComponent<HealthComponent>(); health.Configure(100);
            int deaths = 0; health.Died += () => deaths++;
            health.Damage(-10); Assert.AreEqual(100, health.Current);
            health.Damage(30); Assert.AreEqual(70, health.Current);
            health.Damage(1000); health.Damage(1000); Assert.AreEqual(0, health.Current); Assert.AreEqual(1, deaths);
            health.ResetHealth(); health.Damage(100); Assert.AreEqual(2, deaths);
        }
        [Test] public void ShiftRejectsInactiveDestinationGeometryOverlappingPlayer()
        {
            var validator = SetupSafety();
            obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstruction.transform.position = Vector3.up; obstruction.transform.localScale = new Vector3(2, 2, 2);
            var reactive = obstruction.AddComponent<VeilReactiveObject>(); reactive.ActiveState = VeilState.Broken;
            obstruction.SetActive(false);
            Assert.IsFalse(validator.CanShift(player.GetComponent<CharacterController>(), new[] { reactive }, VeilState.Broken, out string reason));
            Assert.IsFalse(obstruction.activeSelf, "Rejected check must leave inactive destination geometry inactive");
            StringAssert.Contains("blocked", reason);
        }
        [Test] public void ShiftAcceptsSharedFloorButRejectsUnsupportedPosition()
        {
            var validator = SetupSafety(); var body = player.GetComponent<CharacterController>();
            Assert.IsTrue(validator.CanShift(body, new VeilReactiveObject[0], VeilState.Broken, out _));
            body.enabled = false; player.transform.position = Vector3.up * 5; body.enabled = true;
            Assert.IsFalse(validator.CanShift(body, new VeilReactiveObject[0], VeilState.Broken, out _));
        }
        [Test] public void ReactiveObjectChangesBothVisibilityAndColliderParticipation()
        {
            obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var reactive = obstruction.AddComponent<VeilReactiveObject>(); reactive.ActiveState = VeilState.Broken;
            reactive.Apply(VeilState.Living); Assert.IsFalse(obstruction.activeInHierarchy);
            reactive.Apply(VeilState.Broken); Assert.IsTrue(obstruction.activeInHierarchy); Assert.IsTrue(obstruction.GetComponent<Collider>().enabled);
        }
        [Test] public void NonFiniteHealthInputsDoNotPoisonState()
        {
            player = new GameObject("finite health"); var health = player.AddComponent<HealthComponent>();
            health.Configure(float.NaN); Assert.AreEqual(100, health.Maximum);
            health.Damage(float.PositiveInfinity); health.Damage(float.NaN);
            Assert.AreEqual(100, health.Current); Assert.IsFalse(health.IsDead);
            health.Configure(float.NegativeInfinity); Assert.AreEqual(100, health.Current);
        }
        [Test] public void OccupancyRejectsGuardianSizedCapsuleAndIgnoresDisabledColliders()
        {
            var validator = SetupSafety(); var body = player.GetComponent<CharacterController>();
            body.height = 4.7f; body.center = Vector3.up*2.35f; body.radius = 1.15f;
            obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube); obstruction.transform.position = Vector3.up;
            var reactive = obstruction.AddComponent<VeilReactiveObject>(); reactive.ActiveState = VeilState.Broken;
            obstruction.SetActive(false); Physics.SyncTransforms();
            Assert.IsFalse(validator.CanOccupy(body, new[] { reactive }, VeilState.Broken));
            obstruction.GetComponent<Collider>().enabled = false;
            Assert.IsTrue(validator.CanOccupy(body, new[] { reactive }, VeilState.Broken));
        }
        [Test] public void NavigatorFindsASidewaysRouteAroundCover()
        {
            SetupSafety(); obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube); obstruction.layer = 8;
            obstruction.transform.position = Vector3.up; obstruction.transform.localScale = new Vector3(1,2,6);
            Physics.SyncTransforms();
            var navigation = new ArenaNavigator(1.15f,4.7f);
            Vector3 direction = navigation.Direction(new Vector3(-2,.06f,0), new Vector3(4,.06f,0), .5f);
            Assert.Greater(direction.sqrMagnitude,.5f);
            Assert.Less(Vector3.Dot(direction,Vector3.right),.5f, "Must route sideways instead of pushing directly into cover");
        }
        [Test] public void ShiftValidatesPlatformEdgesAndIncomingReactiveSupport()
        {
            var validator = SetupSafety();
            var body = player.GetComponent<CharacterController>();
            // Incoming 0.4 m slab spanning X: [2, 6], Z: [-2, 2], Y: [0, 0.4].
            obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube); obstruction.layer = 8;
            obstruction.transform.position = new Vector3(4, .2f, 0); obstruction.transform.localScale = new Vector3(4, .4f, 4);
            var incoming = obstruction.AddComponent<VeilReactiveObject>(); incoming.ActiveState = VeilState.Broken;
            obstruction.SetActive(false);

            // Capsule (r = 0.32) grazing incoming slab edge at X = 1.85 penetrates 0.17 m > 0.025 m -> blocked.
            body.enabled = false; player.transform.position = new Vector3(1.85f, 0, 0); body.enabled = true;
            Assert.IsFalse(validator.CanShift(body, new[] { incoming }, VeilState.Broken, out string edgeReason));
            StringAssert.Contains("forming stone", edgeReason);
            Assert.IsFalse(obstruction.activeSelf);

            // Capsule cleanly outside slab edge at X = 1.60 on shared ground -> allowed.
            body.enabled = false; player.transform.position = new Vector3(1.60f, 0, 0); body.enabled = true;
            Assert.IsTrue(validator.CanShift(body, new[] { incoming }, VeilState.Broken, out _));

            // Elevated incoming destination platform at Y: [3.6, 4.0] with no shared floor within 1.35 m:
            // resting on top (Y = 4.02) is supported in target reality; stepping past its edge (X = 6.15) is rejected.
            obstruction.transform.position = new Vector3(4, 3.8f, 0);
            body.enabled = false; player.transform.position = new Vector3(4, 4.02f, 0); body.enabled = true;
            Assert.IsTrue(validator.CanShift(body, new[] { incoming }, VeilState.Broken, out _), "Inactive incoming platform must count as destination support");
            body.enabled = false; player.transform.position = new Vector3(6.15f, 4.02f, 0); body.enabled = true;
            Assert.IsFalse(validator.CanShift(body, new[] { incoming }, VeilState.Broken, out string dropReason));
            StringAssert.Contains("stable ground", dropReason);
        }
        [Test] public void NavigatorEscapesCornerOffsetAndStepsAcrossLowTerraces()
        {
            SetupSafety();
            // Reproduce exact Broken-cover corner geometry X: [6.3, 7.7], Z: [1.9, 3.4] where (5.4, 4.4) -> (5, 3) clips the corner.
            obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube); obstruction.layer = 8;
            obstruction.transform.position = new Vector3(7, 1, 2.65f); obstruction.transform.localScale = new Vector3(1.4f, 2, 1.5f);
            // Low 0.4 m stepable terrace around (-4, 0) must not block 0.45 m stepOffset boss navigation.
            secondary = GameObject.CreatePrimitive(PrimitiveType.Cube); secondary.layer = 8;
            secondary.transform.position = new Vector3(5, .2f, 4); secondary.transform.localScale = new Vector3(2.4f, .4f, 3);
            Physics.SyncTransforms();

            var navigation = new ArenaNavigator(1.15f, 4.7f);
            Vector3 dir = navigation.Direction(new Vector3(5.4f, .4f, 4.4f), new Vector3(9f, 0, 2.65f), 3.3f);
            Assert.Greater(dir.sqrMagnitude, .5f, "Navigator must align to clear seed rather than deadlocking at corners or 0.4 m terraces");
        }
        [Test] public void CameraFramingBiasDoesNotPushFocusInsideCoverWall()
        {
            SetupSafety();
            obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube); obstruction.layer = 8;
            obstruction.transform.position = new Vector3(-7f, 1f, 1f); obstruction.transform.localScale = new Vector3(1.4f, 2f, 5f);
            player.transform.position = new Vector3(-8.02f, 0f, 1f);
            secondary = new GameObject("guardian"); secondary.transform.position = new Vector3(-4f, .1f, 1f);
            Physics.SyncTransforms();

            var camGo = new GameObject("camera");
            camGo.transform.SetParent(service.transform);
            var input = service.AddComponent<GameInput>();
            var game = service.AddComponent<ArenaGameManager>();
            var follow = camGo.AddComponent<ThirdPersonCameraController>();
            follow.Target = player.transform; follow.Guardian = secondary.transform; follow.InputSource = input; follow.Game = game;
            follow.ResetView();
            // Position camera yaw so default orbit looks from +X (east of the wall) toward player on -X side of wall.
            var method = typeof(ThirdPersonCameraController).GetField("yaw", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.SetValue(follow, 270f);
            typeof(ThirdPersonCameraController).GetMethod("Position", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(follow, new object[] { true });
            Assert.Less(camGo.transform.position.x, -7.7f, "Camera must retract before wall rather than tunneling through cover");
        }
        [Test] public void GuardianStaggerIgnoresLivingHitsClampsOutsideRecoveryAndResetsOnRestart()
        {
            var validator = SetupSafety();
            var playerHealth = player.AddComponent<HealthComponent>(); playerHealth.Configure(100);
            var playerController = player.AddComponent<PlayerController>(); playerController.SetSpawn();
            var combat = player.AddComponent<PlayerCombat>();

            secondary = new GameObject("guardian");
            var bossBody = secondary.AddComponent<CharacterController>(); bossBody.height = 4.7f; bossBody.radius = 1.15f; bossBody.center = Vector3.up * 2.35f;
            var bossHealth = secondary.AddComponent<HealthComponent>(); bossHealth.Configure(540);
            var guardian = secondary.AddComponent<GuardianAI>();

            var input = service.AddComponent<GameInput>();
            var veil = service.AddComponent<VeilStateManager>();
            var game = service.AddComponent<ArenaGameManager>();
            var cam = service.AddComponent<ThirdPersonCameraController>();

            playerController.InputSource = input; playerController.Game = game;
            combat.Player = playerController; combat.Guardian = guardian;
            veil.Player = playerController; veil.GuardianBody = bossBody; veil.Validator = validator; veil.Objects = new VeilReactiveObject[0];
            guardian.Player = playerController; guardian.Veil = veil; guardian.Game = game;
            game.Player = playerController; game.Combat = combat; game.Guardian = guardian; game.Veil = veil; game.InputSource = input; game.FollowCamera = cam;

            veil.ResetState(); guardian.Initialize(); game.Initialize(); game.Restart();

            guardian.ReceiveHit(200); // 130 actual damage in Living Veil (> 100 threshold)
            Assert.AreEqual(410f, guardian.Health.Current, .01f);
            Assert.AreEqual(0f, guardian.StaggerMeter, .001f, "Armored Living Veil hits must never build stagger");

            Assert.IsTrue(veil.TryShift());
            guardian.ReceiveHit(30); // 60 Broken-core damage
            Assert.AreEqual(60f, guardian.StaggerMeter, .01f);
            guardian.ReceiveHit(30); // +60 -> clamped to 100 outside Recovery without interrupting Observe
            Assert.AreEqual(100f, guardian.StaggerMeter, .01f);
            Assert.AreEqual(GuardianAI.Phase.Observe, guardian.State);

            game.Restart();
            Assert.AreEqual(0f, guardian.StaggerMeter, .001f);
            Assert.AreEqual(VeilState.Living, veil.Current);
            Assert.AreEqual(540f, guardian.Health.Current, .01f);
        }
        ShiftSafetyValidator SetupSafety()
        {
            player = new GameObject("player"); var body = player.AddComponent<CharacterController>(); body.height = 1.8f; body.radius = .32f; body.center = Vector3.up * .9f;
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.layer = 8;
            floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(20, 1, 20);
            service = new GameObject("validator"); return service.AddComponent<ShiftSafetyValidator>();
        }
    }
}
