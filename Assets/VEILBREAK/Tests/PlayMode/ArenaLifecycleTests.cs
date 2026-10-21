using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Veilbreak.Tests
{
    public sealed class ArenaLifecycleTests
    {
        GameObject root;
        [UnityTearDown] public IEnumerator Cleanup()
        { if (root) Object.Destroy(root); Time.timeScale = 1; yield return null; }
        [UnityTest] public IEnumerator TitlePauseWinLoseAndRepeatedRestartResetTheEncounter()
        {
            root = new GameObject("integration arena"); root.AddComponent<VeilbreakBootstrap>();
            yield return null;
            var game = root.GetComponent<ArenaGameManager>();
            Assert.AreEqual(ArenaState.Title, game.State);
            game.Restart(); Assert.IsTrue(game.IsPlaying);
            yield return new WaitForSeconds(.1f);
            Assert.IsTrue(game.Veil.TryShift()); Assert.AreEqual(VeilState.Broken, game.Veil.Current);
            Assert.IsFalse(game.Veil.TryShift(), "Shift must respect cooldown");
            game.TogglePause(); Assert.AreEqual(ArenaState.Paused, game.State); Assert.AreEqual(0, Time.timeScale);
            game.TogglePause(); Assert.IsTrue(game.IsPlaying);
            game.Guardian.Health.Damage(10000); Assert.AreEqual(ArenaState.Victory, game.State);
            for (int i = 0; i < 3; i++)
            {
                game.Restart(); Assert.AreEqual(VeilState.Living, game.Veil.Current);
                Assert.AreEqual(0, game.Veil.CooldownRemaining);
                Assert.AreEqual(game.Player.Health.Maximum, game.Player.Health.Current);
                Assert.AreEqual(game.Guardian.Health.Maximum, game.Guardian.Health.Current);
                Assert.AreEqual(GuardianAI.Phase.Observe, game.Guardian.State);
                Assert.AreEqual(1, root.GetComponentsInChildren<GuardianAI>(true).Length);
                game.Player.ReceiveDamage(10000); Assert.AreEqual(ArenaState.Defeat, game.State);
            }
            game.ReturnToTitle(); Assert.AreEqual(ArenaState.Title, game.State);
        }
        [UnityTest] public IEnumerator BrokenCoreTakesMoreDamageThanLivingArmor()
        {
            root = new GameObject("combat arena"); root.AddComponent<VeilbreakBootstrap>(); yield return null;
            var game = root.GetComponent<ArenaGameManager>(); game.Restart(); yield return new WaitForSeconds(.1f);
            float initial = game.Guardian.Health.Current;
            game.Guardian.ReceiveHit(20); float livingDamage = initial - game.Guardian.Health.Current;
            Assert.IsTrue(game.Veil.TryShift()); initial = game.Guardian.Health.Current;
            game.Guardian.ReceiveHit(20); float brokenDamage = initial - game.Guardian.Health.Current;
            Assert.Greater(brokenDamage, livingDamage);
        }
    }
}
