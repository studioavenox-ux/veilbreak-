using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Veilbreak.Tests
{
    /// <summary>
    /// Proves the running arena is built from <see cref="ArenaLayout"/> rather than from numbers
    /// re-typed inside PrototypeSceneArt: every solid shape the layout describes must exist in the
    /// bootstrapped scene, with the same world position, size, layer and reality state.
    /// </summary>
    public sealed class ArenaLayoutContractTests
    {
        GameObject root;
        [UnityTearDown] public IEnumerator Cleanup()
        { if (root) Object.Destroy(root); Time.timeScale = 1; yield return null; }

        [UnityTest] public IEnumerator BootstrapProducesExactlyTheLayoutGeometry()
        {
            root = new GameObject("layout contract arena");
            root.AddComponent<VeilbreakBootstrap>();
            yield return null;

            var byName = new Dictionary<string, List<BoxCollider>>();
            var all = root.GetComponentsInChildren<BoxCollider>(true);
            for (int i = 0; i < all.Length; i++)
            {
                // Actor CharacterControllers are not BoxColliders, so every box found here is scene geometry.
                if (!byName.TryGetValue(all[i].gameObject.name, out var list))
                    byName[all[i].gameObject.name] = list = new List<BoxCollider>();
                list.Add(all[i]);
            }

            int expected = 0;
            foreach (var box in ArenaLayout.AllSolidShapes())
            {
                expected++;
                Assert.IsTrue(byName.ContainsKey(box.Name), "Bootstrapped scene is missing solid shape '" + box.Name + "'");
                var match = FindMatching(byName[box.Name], root.transform, box, out string reason);
                Assert.IsNotNull(match, "No live collider matches " + box + " :: " + reason);
                Assert.AreEqual(ArenaLayout.SolidLayer, match.gameObject.layer, box.Name + " must be on the solid layer");
                Assert.AreEqual(box.Size.x, match.size.x, .0005f, box.Name + " size.x");
                Assert.AreEqual(box.Size.y, match.size.y, .0005f, box.Name + " size.y");
                Assert.AreEqual(box.Size.z, match.size.z, .0005f, box.Name + " size.z");
            }
            // AllSolidShapes already enumerates one boundary stone per courtyard side.
            Assert.AreEqual(4, byName[ArenaLayout.ParapetStone.Name].Count, "Each courtyard side needs a boundary stone");
            Assert.AreEqual(ArenaLayout.SolidShapeCount, expected, "AllSolidShapes disagreed with SolidShapeCount");
            Assert.AreEqual(expected, all.Length,
                "The bootstrapped scene has a different number of solid colliders than ArenaLayout describes");

            var game = root.GetComponent<ArenaGameManager>();
            Assert.IsNotNull(game, "Bootstrap must produce a running encounter");

            foreach (var structure in ArenaLayout.ReactiveStructures)
            {
                VeilReactiveObject found = null;
                for (int i = 0; i < game.Veil.Objects.Length; i++)
                    if (game.Veil.Objects[i].name == structure.Name) found = game.Veil.Objects[i];
                Assert.IsNotNull(found, "Missing reactive group " + structure.Name);
                Assert.AreEqual(structure.State, found.ActiveState, structure.Name + " reality state");
                Assert.AreEqual(structure.Boxes.Length, found.GetComponentsInChildren<BoxCollider>(true).Length,
                    structure.Name + " collider count");
            }
            Assert.AreEqual(ArenaLayout.ReactiveStructures.Length, game.Veil.Objects.Length,
                "The scene registered a different number of reactive groups than the layout defines");
        }

        static BoxCollider FindMatching(List<BoxCollider> candidates, Transform root, ArenaBox box, out string reason)
        {
            reason = "no candidate";
            foreach (var candidate in candidates)
            {
                // Compare the collider's world centre against the layout centre expressed in world space.
                Vector3 worldCentre = candidate.transform.position
                    + candidate.transform.rotation * Vector3.Scale(candidate.transform.lossyScale, candidate.center);
                Vector3 expected = root.position + root.rotation * box.Center;
                float distance = (worldCentre - expected).magnitude;
                if (distance < .01f) return candidate;
                reason = "nearest offset " + distance.ToString("0.###") + " m";
            }
            return null;
        }
    }
}
