using System.Collections.Generic;
using UnityEngine;

namespace Veilbreak
{
    /// <summary>
    /// The single authoritative description of every arena shape that gameplay can collide with or
    /// whose reality state matters. PrototypeSceneArt turns it into real Unity geometry and the
    /// HeadlessSim harness turns the exact same data into its simulation courtyard, so a coordinate
    /// can never drift between the two. Decorative-only art (flagstones, coping blocks, masonry
    /// drums, foliage, hair, armour panels) stays in PrototypeSceneArt where it belongs.
    ///
    /// Nothing here depends on scene state except where noted; the arrays are rebuilt on demand and
    /// must not be mutated by callers.
    /// </summary>
    public static class ArenaLayout
    {
        /// <summary>Unity layer that holds everything solid: floor, walls, piers, cover, terraces.</summary>
        public const int SolidLayer = 8;
        public const float GroundTopY = 0f;

        static readonly Vector3[] noOffset = { Vector3.zero };

        // ---- courtyard shell -------------------------------------------------
        public static readonly ArenaBox Floor = new ArenaBox(
            "Shared courtyard foundation", new Vector3(0f, -.5f, 0f), new Vector3(32f, 1f, 32f));

        /// <summary>Four walls, one per side, each a yawed group with the stone pushed 16 m out.</summary>
        public static readonly ArenaBox ParapetStone = new ArenaBox(
            "Boundary stone", new Vector3(0f, 1f, 16f), new Vector3(33f, 2f, .8f));

        public static readonly ArenaPier[] Piers = BuildPiers();

        public static readonly ArenaBox[] ArchPiers =
        {
            // South (broken arrival) arch and north (guardian triumph) arch, mirrored piers.
            new ArenaBox("Arch pier", new Vector3(-3f, 2.35f, -14f), new Vector3(1.2f, 4.7f, 1.4f)),
            new ArenaBox("Arch pier", new Vector3(3f, 2.35f, -14f), new Vector3(1.2f, 4.7f, 1.4f)),
            new ArenaBox("Arch pier", new Vector3(-3f, 3f, 14f), new Vector3(1.2f, 6f, 1.4f)),
            new ArenaBox("Arch pier", new Vector3(3f, 3f, 14f), new Vector3(1.2f, 6f, 1.4f)),
        };

        public static readonly ArenaBox ReliquaryPedestal = new ArenaBox(
            "Reliquary pedestal", new Vector3(12f, .6f, 10f), new Vector3(1.5f, 1.2f, 1.5f));

        // ---- encounter-critical reactive geometry ---------------------------
        // These four groups are the shapes the encounter actually turns on: they appear and vanish
        // with the reality shift, so the AI's clearance map and the shift validator must agree on
        // them exactly.

        public static readonly ReactiveStructure LivingRouteCover = new ReactiveStructure(
            "Living / western standing masonry", VeilState.Living,
            new[] { new ArenaBox("Living route cover", new Vector3(-7f, 1f, 1f), new Vector3(1.4f, 2f, 5f)) });

        public static readonly ReactiveStructure BrokenRouteCover = new ReactiveStructure(
            "Broken / eastern fractured masonry", VeilState.Broken,
            new[]
            {
                // Three unequal chunks with real gaps between them; heights walk up 1.4 / 1.7 / 2.0.
                new ArenaBox("Broken route stone chunk", new Vector3(7f, .7f, -.65f), new Vector3(1.4f, 1.4f, 1.5f)),
                new ArenaBox("Broken route stone chunk", new Vector3(7f, .85f, 1f), new Vector3(1.4f, 1.7f, 1.5f)),
                new ArenaBox("Broken route stone chunk", new Vector3(7f, 1f, 2.65f), new Vector3(1.4f, 2f, 1.5f)),
            });

        public static readonly ReactiveStructure LivingTerrace = new ReactiveStructure(
            "Living / intact western terrace", VeilState.Living,
            new[] { new ArenaBox("Intact stepping terrace", new Vector3(-10f, .2f, -5f), new Vector3(4f, .4f, 4f)) });

        public static readonly ReactiveStructure BrokenTerrace = new ReactiveStructure(
            "Broken / fractured eastern terrace", VeilState.Broken,
            new[]
            {
                new ArenaBox("Separated terrace slab", new Vector3(8.65f, .2f, 6f), new Vector3(1.2f, .4f, 3.7f)),
                // The middle slab sits .17 higher, deliberately breaking the step sequence.
                new ArenaBox("Separated terrace slab", new Vector3(10f, .37f, 6f), new Vector3(1.2f, .4f, 3.7f)),
                new ArenaBox("Separated terrace slab", new Vector3(11.35f, .2f, 6f), new Vector3(1.2f, .4f, 3.7f)),
            });

        public static readonly ReactiveStructure[] ReactiveStructures =
            { LivingRouteCover, BrokenRouteCover, LivingTerrace, BrokenTerrace };

        // ---- actor footprints ------------------------------------------------
        // Three of these numbers are also hard-coded in Bootstrap's CharacterController setup and are
        // checked against it by GuardianFootprintTests' guards; changing one without the other fails.

        public static readonly ArenaBox PlayerSpawn = new ArenaBox(
            "Protagonist (placeholder)", new Vector3(0f, .15f, -9f), Vector3.one);

        public const float PlayerColliderHeight = 1.8f;
        public const float PlayerColliderRadius = .32f;
        public const float ActorStepOffset = .45f;

        public static readonly ArenaBox GuardianSpawn = new ArenaBox(
            "Veil Guardian (placeholder)", new Vector3(0f, .1f, 5f), Vector3.one);

        public const float GuardianYaw = 180f;
        public const float GuardianColliderHeight = 4.7f;
        public const float GuardianColliderRadius = 1.15f;

        // ---- queries ---------------------------------------------------------

        /// <summary>Every solid shape, ignoring reality state. Used for scene construction.</summary>
        public static IEnumerable<ArenaBox> AllSolidShapes()
        {
            yield return Floor;
            for (int side = 0; side < 4; side++)
                yield return ParapetStone.Rotated(Quaternion.Euler(0f, side * 90f, 0f));
            for (int i = 0; i < Piers.Length; i++)
            {
                // Plinths are solid in the scene too, so they are part of the collidable definition.
                yield return Piers[i].Box;
                yield return Piers[i].Plinth;
            }
            for (int i = 0; i < ArchPiers.Length; i++) yield return ArchPiers[i];
            yield return ReliquaryPedestal;
            for (int g = 0; g < ReactiveStructures.Length; g++)
                for (int i = 0; i < ReactiveStructures[g].Boxes.Length; i++)
                    yield return ReactiveStructures[g].Boxes[i];
        }

        /// <summary>Total number of solid collider shapes the courtyard must produce.</summary>
        public static int SolidShapeCount
        {
            get
            {
                int count = 0;
                foreach (var unused in AllSolidShapes()) count++;
                return count;
            }
        }

        static ArenaPier[] BuildPiers()
        {
            var piers = new ArenaPier[8];
            for (int i = 0; i < piers.Length; i++)
            {
                float angle = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                // Alternating heights 2.0 / 3.6 / 4.7 give the ring an uneven, ruined silhouette.
                float height = i % 3 == 0 ? 2.0f : i % 3 == 1 ? 3.6f : 4.7f;
                var origin = new Vector3(Mathf.Sin(angle) * 13.8f, 0f, Mathf.Cos(angle) * 13.8f);
                // Boxes are stored in arena space, so every ArenaBox the layout hands out means the same
                // thing: a position relative to the courtyard root, whatever group draws the decoration.
                piers[i] = new ArenaPier(origin,
                    new ArenaBox("Pier collision", origin + new Vector3(0f, height * .5f, 0f), new Vector3(.86f, height, .86f)),
                    new ArenaBox("Pier plinth", origin + new Vector3(0f, .22f, 0f), new Vector3(1.7f, .44f, 1.7f)));
            }
            return piers;
        }
    }

    /// <summary>An axis-aligned box in its parent's space, with a position and a scale.</summary>
    public readonly struct ArenaBox
    {
        public readonly string Name;
        public readonly Vector3 Center;
        public readonly Vector3 Size;

        public ArenaBox(string name, Vector3 center, Vector3 size)
        {
            Name = name ?? "shape";
            Center = center;
            Size = size;
        }

        public Vector3 HalfExtents => Size * .5f;
        public float Top => Center.y + Size.y * .5f;
        public float Bottom => Center.y - Size.y * .5f;

        /// <summary>Same shape re-expressed under a rotated parent, as a scene would position it.</summary>
        public ArenaBox Rotated(Quaternion rotation) =>
            new ArenaBox(Name, rotation * Center, Size);

        public override string ToString() =>
            Name + " @ " + Center.x.ToString("0.###") + "," + Center.y.ToString("0.###") + "," + Center.z.ToString("0.###")
            + " size " + Size.x.ToString("0.###") + "," + Size.y.ToString("0.###") + "," + Size.z.ToString("0.###");
    }

    /// <summary>
    /// A ruined pier: where its cosmetic column group stands, plus its collision boxes (already
    /// expressed in arena space, i.e. offset by <see cref="Origin"/>).
    /// </summary>
    public readonly struct ArenaPier
    {
        public readonly Vector3 Origin;
        public readonly ArenaBox Box;
        public readonly ArenaBox Plinth;

        public ArenaPier(Vector3 origin, ArenaBox box, ArenaBox plinth)
        {
            Origin = origin;
            Box = box;
            Plinth = plinth;
        }

        public float Height => Box.Size.y;
    }

    /// <summary>A group that only exists in one reality, with the solid boxes it contributes.</summary>
    public sealed class ReactiveStructure
    {
        public readonly string Name;
        public readonly VeilState State;
        public readonly ArenaBox[] Boxes;

        public ReactiveStructure(string name, VeilState state, ArenaBox[] boxes)
        {
            Name = name;
            State = state;
            Boxes = boxes ?? new ArenaBox[0];
        }

        public override string ToString() => Name + " (" + State + ", " + Boxes.Length + " boxes)";
    }
}
