using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Veilbreak.Tests
{
    public sealed class SceneArtTests
    {
        GameObject root;
        [UnityTearDown] public IEnumerator Cleanup()
        { if (root) Object.Destroy(root); Time.timeScale = 1; yield return null; }

        [UnityTest] public IEnumerator AssembledArtKeepsControllersAndUsesSharedGeometry()
        {
            root = new GameObject("art integration arena"); root.AddComponent<VeilbreakBootstrap>();
            yield return null; // Allow temporary floor construction objects/colliders to be released.
            var game = root.GetComponent<ArenaGameManager>();
            Assert.AreEqual(1, game.Player.GetComponents<CharacterController>().Length);
            Assert.AreEqual(1, game.Guardian.GetComponents<CharacterController>().Length);
            Assert.Greater(game.Player.Visual.GetComponentsInChildren<MeshRenderer>().Length, 35);
            Assert.Greater(game.Guardian.Visual.GetComponentsInChildren<MeshRenderer>().Length, 40);
            Assert.AreEqual(0, game.Player.Visual.GetComponentsInChildren<Collider>().Length, "Actor art must not add collision controllers");
            Assert.AreEqual(0, game.Guardian.Visual.GetComponentsInChildren<Collider>().Length);
            var parts = game.Player.Visual.GetComponentsInChildren<MeshFilter>();
            var boot = parts.First(p => p.name == "Boot");
            var chest = parts.First(p => p.name == "Chest cuirass");
            Assert.AreSame(boot.sharedMesh, chest.sharedMesh, "Low-poly blocks should reuse one mesh");
            Assert.Greater(boot.sharedMesh.vertexCount, 0);
            Assert.AreEqual(0, boot.sharedMesh.triangles.Length % 3);
            Assert.IsTrue(Vector3.Dot(game.Guardian.transform.forward,
                (game.Player.transform.position-game.Guardian.transform.position).normalized) > .9f, "Core faces the arrival camera/player");
        }

        [UnityTest] public IEnumerator ArtStateChangesPreservePlayerCyanAndDoNotAllocateOnRestart()
        {
            root = new GameObject("state art arena"); root.AddComponent<VeilbreakBootstrap>(); yield return null;
            var game = root.GetComponent<ArenaGameManager>(); game.Restart();
            yield return new WaitForSeconds(.1f);
            var blade = game.Player.Visual.GetComponentsInChildren<Renderer>().First(r => r.name == "Visible cyan energy blade");
            Material bladeMaterial = blade.sharedMaterial;
            Color cyan = bladeMaterial.color;
            int renderers = root.GetComponentsInChildren<Renderer>(true).Length;
            var materials = root.GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial).Distinct().ToArray();
            var brokenPlatform = game.Veil.Objects.First(o => o.name == "Broken / fractured eastern terrace");
            Assert.IsFalse(brokenPlatform.gameObject.activeSelf);
            Assert.AreEqual(3, brokenPlatform.GetComponentsInChildren<BoxCollider>(true).Length);
            Assert.IsTrue(game.Guardian.ArmorPlate.gameObject.activeSelf);
            Assert.IsTrue(game.Veil.TryShift());
            Assert.IsTrue(brokenPlatform.gameObject.activeSelf);
            Assert.IsFalse(game.Guardian.ArmorPlate.gameObject.activeSelf);
            Assert.AreEqual(cyan, blade.sharedMaterial.color, "Player signature cyan must not become violet");
            Assert.AreNotSame(bladeMaterial, game.Veil.EnergyMaterial);
            for(int i=0;i<3;i++) game.Restart();
            Assert.AreEqual(renderers, root.GetComponentsInChildren<Renderer>(true).Length);
            CollectionAssert.AreEquivalent(materials, root.GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial).Distinct().ToArray());
            Assert.IsFalse(brokenPlatform.gameObject.activeSelf);
            Assert.IsTrue(game.Guardian.ArmorPlate.gameObject.activeSelf);
        }
    }
}
