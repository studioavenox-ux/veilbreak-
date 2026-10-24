using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Veilbreak.Tests
{
    public sealed class EncounterReliabilityTests
    {
        GameObject root;
        ArenaGameManager Create()
        {
            root = new GameObject("release acceptance arena"); root.AddComponent<VeilbreakBootstrap>();
            return root.GetComponent<ArenaGameManager>();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        { if(root) Object.Destroy(root); Time.timeScale=1; yield return null; }
        static void Place(PlayerController player, Vector3 point)
        { player.Body.enabled=false; player.transform.SetPositionAndRotation(point,Quaternion.identity); player.Body.enabled=true; }
        [UnityTest] public IEnumerator MeleeWindupDamagesOnceAndDodgeCancelsTheNextSwing()
        {
            var game=Create(); yield return null; game.Restart(); game.Guardian.enabled=false;
            Place(game.Player,new Vector3(0,.15f,2));
            float initial=game.Guardian.Health.Current;
            game.InputSource.Attack(); yield return null;
            Assert.IsTrue(game.Combat.IsAttacking); Assert.AreEqual(initial,game.Guardian.Health.Current);
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(initial-18*.65f,game.Guardian.Health.Current,.01f);
            float afterHit=game.Guardian.Health.Current;
            game.InputSource.Attack(); yield return new WaitForSeconds(.2f);
            Assert.AreEqual(afterHit,game.Guardian.Health.Current,"Cooldown/persistent swing cannot deal extra damage");
            yield return new WaitForSeconds(.3f);
            game.InputSource.Attack(); yield return null; Assert.IsTrue(game.Combat.IsAttacking);
            game.InputSource.Dodge(); yield return null;
            Assert.IsTrue(game.Player.IsDodging); Assert.IsFalse(game.Combat.IsAttacking);
            float playerHealth=game.Player.Health.Current; game.Player.ReceiveDamage(50);
            Assert.AreEqual(playerHealth,game.Player.Health.Current,"Dodge grants immunity");
            yield return new WaitForSeconds(.35f); Assert.AreEqual(afterHit,game.Guardian.Health.Current);
        }
        [UnityTest] public IEnumerator ShiftRejectsGuardianOverlapWithoutChangingStateOrPosition()
        {
            var game=Create(); yield return null; game.Restart();
            var bossBody=game.Guardian.GetComponent<CharacterController>(); bossBody.enabled=false;
            game.Guardian.transform.position=new Vector3(7,.05f,1); bossBody.enabled=true;
            Vector3 before=game.Player.transform.position; string reason=null; game.Veil.Rejected += value=>reason=value;
            Assert.IsFalse(game.Veil.TryShift()); Assert.AreEqual(VeilState.Living,game.Veil.Current);
            Assert.AreEqual(before,game.Player.transform.position); StringAssert.Contains("guardian",reason);
            Assert.AreEqual(0,game.Veil.CooldownRemaining);
        }
        [UnityTest] public IEnumerator HudReadsAuthoritativeStateAndRestartCancelsPendingDamage()
        {
            var game=Create(); yield return null; game.Restart(); game.Guardian.enabled=false;
            Place(game.Player,new Vector3(0,.15f,2)); game.InputSource.Attack(); yield return null;
            game.TogglePause(); Assert.AreEqual(ArenaState.Paused,game.State);
            yield return new WaitForSecondsRealtime(.2f); Assert.IsTrue(game.Combat.IsAttacking);
            game.Restart(); Assert.IsFalse(game.Combat.IsAttacking);
            game.Player.ReceiveDamage(20); Assert.IsTrue(game.Veil.TryShift()); yield return null;
            var labels=root.GetComponentsInChildren<Text>(true);
            StringAssert.Contains("80",labels.First(t=>t.name=="Player health").text);
            Assert.AreEqual("BROKEN VEIL",labels.First(t=>t.name=="Reality").text);
            yield return new WaitForSeconds(.2f);
            Assert.AreEqual(game.Guardian.Health.Maximum,game.Guardian.Health.Current);
            game.Guardian.Health.Damage(10000); Assert.AreEqual(ArenaState.Victory,game.State);
            game.Restart(); game.Player.ReceiveDamage(10000); Assert.AreEqual(ArenaState.Defeat,game.State);
            game.ReturnToTitle(); game.Restart(); Assert.IsTrue(game.IsPlaying);
            Assert.AreEqual(1,root.GetComponentsInChildren<EncounterAudio>().Length);
        }
        static IEnumerator AwaitPhase(ArenaGameManager game, GuardianAI.Phase phase, float seconds)
        {
            float deadline=Time.time+seconds;
            while(game.IsPlaying && game.Guardian.State!=phase && Time.time<deadline) yield return null;
            Assert.AreEqual(phase,game.Guardian.State);
        }
        [UnityTest] public IEnumerator GuardianAlternatesWarnedAttacksAndEachHitsOnlyOnce()
        {
            var game=Create(); yield return null; game.Restart();
            Place(game.Player,new Vector3(0,.15f,2)); game.Player.enabled=false;
            yield return AwaitPhase(game,GuardianAI.Phase.Telegraph,3);
            StringAssert.Contains("GROUND SLAM",game.Guardian.AttackName);
            Assert.AreEqual(100,game.Player.Health.Current);
            yield return new WaitForSeconds(.2f); Assert.AreEqual(100,game.Player.Health.Current);
            yield return AwaitPhase(game,GuardianAI.Phase.Recovery,2);
            Assert.AreEqual(75,game.Player.Health.Current);
            yield return AwaitPhase(game,GuardianAI.Phase.Telegraph,4);
            StringAssert.Contains("ENERGY STRIKE",game.Guardian.AttackName);
            yield return AwaitPhase(game,GuardianAI.Phase.Recovery,2);
            Assert.AreEqual(55,game.Player.Health.Current);
            yield return new WaitForSeconds(.4f); Assert.AreEqual(55,game.Player.Health.Current);
        }
        [UnityTest] public IEnumerator CoverIntroducedDuringWindupBlocksDamage()
        {
            var game=Create(); yield return null; game.Restart();
            Place(game.Player,new Vector3(0,.15f,2)); game.Player.enabled=false;
            yield return AwaitPhase(game,GuardianAI.Phase.Telegraph,3);
            var cover=GameObject.CreatePrimitive(PrimitiveType.Cube); cover.transform.SetParent(root.transform);
            cover.layer=8; cover.transform.position=new Vector3(0,1,3.5f); cover.transform.localScale=new Vector3(6,2,.3f);
            Physics.SyncTransforms();
            yield return AwaitPhase(game,GuardianAI.Phase.Recovery,2);
            Assert.AreEqual(100,game.Player.Health.Current,"Locked attacks must still validate impact-time line of sight");
        }
        [UnityTest] public IEnumerator GuardianRoutesAroundLivingCoverToReachThePlayer()
        {
            var game=Create(); yield return null; game.Restart();
            Place(game.Player,new Vector3(-10,.15f,1));
            var body=game.Guardian.GetComponent<CharacterController>(); body.enabled=false;
            game.Guardian.transform.position=new Vector3(-4,.1f,1); body.enabled=true;
            float deadline=Time.time+10;
            while(game.Guardian.transform.position.x > -8 && Time.time<deadline && game.IsPlaying) yield return null;
            Assert.Less(game.Guardian.transform.position.x,-8,"Boss should route around the west cover, not stall against it");
        }
        [UnityTest] public IEnumerator StaggerBuildsOnlyFromBrokenCoreHitsAndTriggersInRecovery()
        {
            var game = Create(); yield return null; game.Restart();
            Place(game.Player, new Vector3(0, .15f, 2)); game.Player.enabled = false;
            yield return AwaitPhase(game, GuardianAI.Phase.Recovery, 4);

            for (int i = 0; i < 10; i++) game.Guardian.ReceiveHit(18);
            Assert.AreEqual(0f, game.Guardian.StaggerMeter, .001f);
            Assert.AreEqual(GuardianAI.Phase.Recovery, game.Guardian.State, "Living Veil hits must never build or trigger stagger");
            Assert.IsTrue(game.Guardian.ArmorPlate.gameObject.activeSelf);

            Assert.IsTrue(game.Veil.TryShift());
            Assert.IsFalse(game.Guardian.ArmorPlate.gameObject.activeSelf);
            game.Guardian.ReceiveHit(18); game.Guardian.ReceiveHit(18);
            Assert.AreEqual(72f, game.Guardian.StaggerMeter, .01f);
            Assert.AreEqual(GuardianAI.Phase.Recovery, game.Guardian.State);

            game.Guardian.ReceiveHit(18);
            Assert.AreEqual(GuardianAI.Phase.Stagger, game.Guardian.State);
            Assert.AreEqual(0f, game.Guardian.StaggerMeter, .001f);

            float healthBefore = game.Guardian.Health.Current;
            game.Guardian.ReceiveHit(18);
            Assert.AreEqual(healthBefore - 36f, game.Guardian.Health.Current, .01f);
            Assert.AreEqual(0f, game.Guardian.StaggerMeter, .001f, "Hits during Stagger must not pre-charge another stagger");
        }
        [UnityTest] public IEnumerator StaggerThresholdReachedOutsideRecoveryTriggersAutomaticallyAndResetsOnRestart()
        {
            var game = Create(); yield return null; game.Restart();
            Place(game.Player, new Vector3(0, .15f, 2)); game.Player.enabled = false;
            Assert.AreEqual(GuardianAI.Phase.Observe, game.Guardian.State);
            Assert.IsTrue(game.Veil.TryShift());

            game.Guardian.ReceiveHit(18); game.Guardian.ReceiveHit(18); game.Guardian.ReceiveHit(18);
            Assert.AreEqual(100f, game.Guardian.StaggerMeter, .01f);
            Assert.AreEqual(GuardianAI.Phase.Observe, game.Guardian.State, "Stagger must not interrupt non-Recovery phases early");

            yield return AwaitPhase(game, GuardianAI.Phase.Telegraph, 2.5f);
            Assert.AreEqual(100f, game.Guardian.StaggerMeter, .01f);

            // Without any additional hit during Recovery, finishing Attack must open Stagger immediately.
            yield return AwaitPhase(game, GuardianAI.Phase.Stagger, 2.2f);
            Assert.AreEqual(0f, game.Guardian.StaggerMeter, .001f);

            yield return AwaitPhase(game, GuardianAI.Phase.Approach, 2f);
            game.Guardian.ReceiveHit(18);
            Assert.AreEqual(36f, game.Guardian.StaggerMeter, .01f);

            game.Restart();
            Assert.AreEqual(0f, game.Guardian.StaggerMeter, .001f);
            Assert.AreEqual(GuardianAI.Phase.Observe, game.Guardian.State);
            Assert.AreEqual(VeilState.Living, game.Veil.Current);
            Assert.IsTrue(game.Guardian.ArmorPlate.gameObject.activeSelf);
        }
        [UnityTest] public IEnumerator ShiftRejectsPlayerPlatformOverlapSupportsEdgeTransitionAndPreservesPauseHint()
        {
            var game = Create(); yield return null; game.Restart(); game.Guardian.enabled = false;
            Place(game.Player, new Vector3(10f, .15f, 6f));
            Vector3 blockedPos = game.Player.transform.position;
            Assert.IsFalse(game.Veil.TryShift(), "Player overlapping incoming Broken terrace must block shift");
            Assert.AreEqual(VeilState.Living, game.Veil.Current);
            Assert.AreEqual(blockedPos, game.Player.transform.position);
            Assert.AreEqual(0f, game.Veil.CooldownRemaining);

            Place(game.Player, new Vector3(-8.2f, .55f, -5f));
            Assert.IsTrue(game.Veil.TryShift());
            Assert.AreEqual(VeilState.Broken, game.Veil.Current);
            Assert.Greater(game.Veil.CooldownRemaining, 0f);

            var hint = root.GetComponentsInChildren<Text>(true).First(t => t.name == "Combat guidance");
            StringAssert.Contains("BROKEN VEIL", hint.text);
            game.TogglePause(); Assert.AreEqual(ArenaState.Paused, game.State);
            game.TogglePause(); Assert.IsTrue(game.IsPlaying);
            StringAssert.Contains("BROKEN VEIL", hint.text, "Unpausing must preserve active feedback rather than repeating startup tutorial");

            yield return new WaitForSeconds(1.05f);
            Assert.IsTrue(game.Veil.TryShift()); Assert.AreEqual(VeilState.Living, game.Veil.Current);
            yield return new WaitForSeconds(1.05f);
            Assert.IsTrue(game.Veil.TryShift()); Assert.AreEqual(VeilState.Broken, game.Veil.Current);
            game.Restart();
            Assert.AreEqual(VeilState.Living, game.Veil.Current);
            Assert.AreEqual(0f, game.Veil.CooldownRemaining);
        }
    }
}
