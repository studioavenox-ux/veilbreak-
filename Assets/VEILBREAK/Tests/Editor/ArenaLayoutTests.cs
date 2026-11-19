using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Veilbreak.Tests
{
    /// <summary>
    /// Pure-data invariants for <see cref="ArenaLayout"/>, the single authoritative description of the
    /// arena's collision geometry. No scene is built here, so these run in EditMode without a render
    /// pipeline. The "does the scene art actually place this?" half of the contract lives in
    /// PlayMode/ArenaLayoutContractTests, which needs a bootstrapped arena.
    /// </summary>
    public sealed class ArenaLayoutTests
    {
        [Test] public void ReactiveStructuresCoverBothRealitiesWithRealCollision()
        {
            Assert.AreEqual(4, ArenaLayout.ReactiveStructures.Length,
                "Two route covers and two terraces are the encounter-critical reactive groups");
            int living = 0, broken = 0;
            foreach (var structure in ArenaLayout.ReactiveStructures)
            {
                Assert.IsNotEmpty(structure.Name);
                Assert.Greater(structure.Boxes.Length, 0, structure.Name + " must contribute collision");
                if (structure.State == VeilState.Living) living++;
                else broken++;
                foreach (var box in structure.Boxes)
                {
                    Assert.Greater(box.Size.x, 0f, box.Name);
                    Assert.Greater(box.Size.y, 0f, box.Name);
                    Assert.Greater(box.Size.z, 0f, box.Name);
                }
            }
            Assert.AreEqual(2, living, "Living Veil needs one route cover and one terrace");
            Assert.AreEqual(2, broken, "Broken Veil needs one route cover and one terrace");
        }

        [Test] public void EverySolidShapeIsWellFormedAndOnTheSolidLayer()
        {
            var names = new HashSet<string>();
            int count = 0;
            foreach (var box in ArenaLayout.AllSolidShapes())
            {
                count++;
                Assert.IsNotEmpty(box.Name);
                Assert.Greater(box.Size.x, 0f, box.Name);
                Assert.Greater(box.Size.y, 0f, box.Name);
                Assert.Greater(box.Size.z, 0f, box.Name);
                Assert.IsFalse(float.IsNaN(box.Center.x) || float.IsNaN(box.Center.y) || float.IsNaN(box.Center.z), box.Name);
            }
            Assert.AreEqual(ArenaLayout.SolidShapeCount, count, "SolidShapeCount must agree with AllSolidShapes");
            // Floor + 4 boundary stones + 8 pier columns + 8 pier plinths + 4 arch piers + pedestal
            // + 1 Living cover + 3 Broken chunks + 1 Living terrace + 3 Broken slabs.
            Assert.AreEqual(34, count, "Adding or removing a solid shape is a gameplay change: update this expectation deliberately");
            Assert.AreEqual(8, ArenaLayout.SolidLayer, "Gameplay code queries layer 8 for navigation and shift safety");
        }

        [Test] public void PlayerAndGuardianSpawnAboveTheFloorAndInsideTheWalls()
        {
            Assert.AreEqual(ArenaLayout.GroundTopY, ArenaLayout.Floor.Top, .0001f,
                "The floor's top face is the ground plane spawn heights are measured from");
            Assert.Greater(ArenaLayout.PlayerSpawn.Center.y, ArenaLayout.GroundTopY, "Player must spawn above the floor");
            Assert.Greater(ArenaLayout.GuardianSpawn.Center.y, ArenaLayout.GroundTopY, "Guardian must spawn above the floor");
            float halfZ = ArenaLayout.Floor.Size.z * .5f;
            Assert.Less(Mathf.Abs(ArenaLayout.PlayerSpawn.Center.z), halfZ - 1f, "Player must spawn inside the walls");
            Assert.Less(Mathf.Abs(ArenaLayout.GuardianSpawn.Center.z), halfZ - 1f, "Guardian must spawn inside the walls");
            Assert.Greater(ArenaLayout.GuardianColliderHeight, ArenaLayout.GuardianColliderRadius * 2f,
                "Guardian must be a capsule, not a sphere");
            Assert.Less(ArenaLayout.PlayerColliderRadius, ArenaLayout.GuardianColliderRadius);
        }

        [Test] public void EncounterCriticalCoverSitsWhereTheFightExpectsIt()
        {
            // The Living cover shields the west; the Broken chunks and the terraces are east. These
            // relationships are what the navigation and shift-safety cases rely on.
            var livingCover = ArenaLayout.LivingRouteCover.Boxes[0];
            Assert.Less(livingCover.Center.x, -5f, "Living cover guards the western approach");
            foreach (var chunk in ArenaLayout.BrokenRouteCover.Boxes)
                Assert.Greater(chunk.Center.x, 5f, "Broken cover guards the eastern approach");
            Assert.Less(ArenaLayout.LivingTerrace.Boxes[0].Center.x, -5f);
            foreach (var slab in ArenaLayout.BrokenTerrace.Boxes)
                Assert.Greater(slab.Center.x, 8f);

            // Chunk heights walk upward and the middle terrace slab is offset, so the sequence is uneven.
            Assert.Less(ArenaLayout.BrokenRouteCover.Boxes[0].Top, ArenaLayout.BrokenRouteCover.Boxes[2].Top);
            Assert.AreNotEqual(ArenaLayout.BrokenTerrace.Boxes[1].Center.y, ArenaLayout.BrokenTerrace.Boxes[0].Center.y,
                "The middle terrace slab must stay raised");
        }

        [Test] public void WallRotationMovesTheStoneAlongTheYawedAxisOnly()
        {
            var east = ArenaLayout.ParapetStone.Rotated(Quaternion.Euler(0f, 90f, 0f));
            Assert.AreEqual(16f, east.Center.x, .0005f);
            Assert.AreEqual(0f, east.Center.z, .0005f);
            Assert.AreEqual(ArenaLayout.ParapetStone.Size, east.Size, "Rotation must never rescale collision");
            var north = ArenaLayout.ParapetStone.Rotated(Quaternion.Euler(0f, 180f, 0f));
            Assert.AreEqual(-16f, north.Center.z, .0005f);
        }
    }
}
