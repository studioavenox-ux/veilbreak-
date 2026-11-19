// Self-test for the headless engine mock. A simulation can only be trusted if the mock itself
// satisfies basic vector/transform/physics invariants, so these run before any gameplay case.
using System;
using UnityEngine;

namespace Veilbreak.Sim
{
    internal static class EngineSelfTest
    {
        internal static int Failures;

        static void Check(string what, bool ok, string detail = null)
        {
            if (ok) return;
            Failures++;
            Console.WriteLine("ENGINE FAIL " + what + (detail == null ? "" : " [" + detail + "]"));
        }

        static void Near(string what, float expected, float actual, float tolerance = 1e-3f) =>
            Check(what, Math.Abs(expected - actual) <= tolerance, "expected " + expected.ToString("0.####") + " got " + actual.ToString("0.####"));

        static void Near(string what, Vector3 expected, Vector3 actual, float tolerance = 1e-3f) =>
            Check(what + " " + expected, (expected - actual).magnitude <= tolerance, "got " + actual);

        public static void Run()
        {
            Failures = 0;

            // --- vectors ---
            Near("Vector3.magnitude", 5f, new Vector3(3, 0, 4).magnitude);
            Near("Vector3.Cross", new Vector3(0, 0, 1), Vector3.Cross(Vector3.right, Vector3.up));
            Near("Quaternion*Vector3 forward", Vector3.forward, Quaternion.identity * Vector3.forward);
            Near("Euler(0,90,0)*forward", Vector3.right, Quaternion.Euler(0, 90, 0) * Vector3.forward);
            Near("Euler(0,180,0)*forward", Vector3.back, Quaternion.Euler(0, 180, 0) * Vector3.forward);
            Near("LookRotation(+Z)", Vector3.forward, Quaternion.LookRotation(Vector3.forward) * Vector3.forward);
            Near("LookRotation(+X)", Vector3.right, Quaternion.LookRotation(Vector3.right) * Vector3.forward);
            Near("LookRotation(-Z)", Vector3.back, Quaternion.LookRotation(Vector3.back) * Vector3.forward);
            Near("ProjectOnPlane", new Vector3(1, 0, 2), Vector3.ProjectOnPlane(new Vector3(1, 3, 2), Vector3.up));
            Near("Angle 90", 90f, Vector3.Angle(Vector3.forward, Vector3.right), 1e-2f);

            // --- transform hierarchy ---
            var parent = new GameObject("parent");
            parent.transform.position = new Vector3(3, 1, -2);
            parent.transform.localRotation = Quaternion.Euler(0, 90, 0);
            var child = new GameObject("child");
            child.transform.SetParent(parent.transform, false);
            child.transform.localPosition = new Vector3(0, 2, 5);
            Near("child world position", new Vector3(8, 3, -2), child.transform.position);
            child.transform.position = new Vector3(0, 0, 2);
            Near("child position round-trip", new Vector3(0, 0, 2), child.transform.position);
            // Parent yawed +90 about Y: local +X maps to world -Z and local +Z maps to world +X.
            // World (0,0,2) is (-3,-1,4) from the parent, i.e. local (-4,-1,-3).
            Near("child local after world set", new Vector3(-4, -1, -3), child.transform.localPosition, 1e-2f);

            // --- physics: a capsule rests on a box floor ---
            Scene.Reset();
            var root = new GameObject("floor root");
            var floor = new GameObject("floor");
            floor.layer = 8;
            floor.transform.SetParent(root.transform, false);
            floor.transform.localPosition = new Vector3(0, -.5f, 0);
            var box = floor.AddComponent<BoxCollider>();
            box.size = new Vector3(10, 1, 10);
            var body = new GameObject("body");
            body.transform.SetParent(root.transform, false);
            var controller = body.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = .32f;
            controller.center = Vector3.up * .9f;
            Physics.Recollect();
            var downward = Physics.RaycastAll(new Vector3(0, 1, 0), Vector3.down, 3f, 1 << 8, QueryTriggerInteraction.Ignore);
            Check("floor raycast hits the floor", downward.Length > 0, "hits=" + downward.Length);
            if (downward.Length > 0) Near("floor top raycast", 0f, downward[0].point.y);

            body.transform.position = new Vector3(0, 2, 0); // start above the floor, then fall
            for (int i = 0; i < 120; i++)
            {
                controller.Move(Vector3.down * 10f * (1f / 60f));
                Physics.Recollect();
            }
            // Direct unit check of the ground helper, then the integrated fall.
            Near("capsule rests on floor", 0f, body.transform.position.y, 0.06f);
            Check("capsule reports grounded", controller.isGrounded);
            Near("no horizontal drift", Vector3.zero, new Vector3(body.transform.position.x, 0, body.transform.position.z));

            // --- physics: a rotated, scaled wall is hit at the right distance ---
            var rotated = new GameObject("rotated wall");
            rotated.layer = 8;
            rotated.transform.SetParent(root.transform, false);
            rotated.transform.localPosition = new Vector3(0, 1, 6);
            rotated.transform.localRotation = Quaternion.Euler(0, 90, 0);
            // Yawed 90 degrees, so the box's local X axis (scaled 3x) points along world Z.
            // Local thickness 0.4 * 3 = 1.2 m, so the near face sits at z = 6 - 0.6 = 5.4.
            rotated.transform.localScale = new Vector3(3, 1, 1);
            var rotatedBox = rotated.AddComponent<BoxCollider>();
            rotatedBox.size = new Vector3(.4f, 2f, 10f);
            Physics.Recollect();
            float nearest = -1f;
            foreach (var rotatedHit in Physics.RaycastAll(new Vector3(0, 1, 0), Vector3.forward, 10f, 1 << 8, QueryTriggerInteraction.Ignore))
                if (rotatedHit.collider == rotatedBox) nearest = rotatedHit.distance;
            Near("rotated scaled wall hit distance", 5.4f, nearest, .05f);

            // --- physics: a non-uniformly scaled box is hit at its true world face ---
            // size 4 in Y with a world scale of 2 means the box spans y in [-4, 4]; a downward ray
            // from y = 10 must stop at y = 4, i.e. distance 6. Applying the scale twice (scaled
            // extents AND a scaled ray) would report 2, so this pins the transform order.
            var tall = new GameObject("scaled slab");
            tall.layer = 8;
            tall.transform.SetParent(root.transform, false);
            tall.transform.localPosition = Vector3.zero;
            tall.transform.localScale = new Vector3(1, 2, 1);
            var tallBox = tall.AddComponent<BoxCollider>();
            tallBox.size = new Vector3(2, 4, 2);
            Physics.Recollect();
            // RaycastAll is unordered, so find this shape by identity rather than by index.
            float slabDistance = -1f;
            foreach (var slabHit in Physics.RaycastAll(new Vector3(0, 10, 0), Vector3.down, 20f, 1 << 8, QueryTriggerInteraction.Ignore))
                if (slabHit.collider == tallBox) slabDistance = slabHit.distance;
            Near("scaled box top face is hit at its world height", 6f, slabDistance, .01f);
            tall.SetActive(false);

            // --- physics: walls stop horizontal motion ---
            var wall = new GameObject("wall");
            wall.layer = 8;
            wall.transform.SetParent(root.transform, false);
            wall.transform.localPosition = new Vector3(0, 1, 2);
            var wallBox = wall.AddComponent<BoxCollider>();
            wallBox.size = new Vector3(10, 2, .4f);
            Physics.Recollect();
            for (int i = 0; i < 90; i++) { controller.Move(new Vector3(0, -.2f, 6f * (1f / 60f))); Physics.Recollect(); }
            Check("wall blocks forward motion", body.transform.position.z < 1.6f, "z=" + body.transform.position.z.ToString("0.###"));

            Scene.Reset();
            Console.WriteLine(Failures == 0 ? "engine self-test: OK" : "engine self-test: " + Failures + " FAILURES");
        }
    }
}
