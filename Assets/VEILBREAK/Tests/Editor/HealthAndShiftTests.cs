using NUnit.Framework;
using UnityEngine;

namespace Veilbreak.Tests
{
    public sealed class HealthAndShiftTests
    {
        GameObject player, floor, obstruction, service;
        [TearDown] public void Cleanup()
        {
            foreach (var go in new[] { player, floor, obstruction, service }) if (go) Object.DestroyImmediate(go);
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
        ShiftSafetyValidator SetupSafety()
        {
            player = new GameObject("player"); var body = player.AddComponent<CharacterController>(); body.height = 1.8f; body.radius = .32f; body.center = Vector3.up * .9f;
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.layer = 8;
            floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(20, 1, 20);
            service = new GameObject("validator"); return service.AddComponent<ShiftSafetyValidator>();
        }
    }
}
