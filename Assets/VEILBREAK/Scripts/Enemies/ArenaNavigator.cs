using UnityEngine;

namespace Veilbreak
{
    // Bounded flat-courtyard routing: 31x31 nodes, four cardinal edges, no NavMesh dependency.
    // Clearance is cached lazily per reality. Search only when the direct attack approach is obstructed.
    public sealed class ArenaNavigator
    {
        const int Width = 31, Count = Width * Width, Mask = 1 << 8;
        const float StepClearance = .46f;
        readonly sbyte[] clearance = new sbyte[Count];
        readonly int[] parents = new int[Count], queue = new int[Count];
        readonly float nodeRadius, sweepRadius, height;
        Vector3 waypoint;
        bool hasWaypoint;
        float nextSearch;
        public ArenaNavigator(float radius, float height)
        {
            sweepRadius = Mathf.Max(.05f, radius - .02f);
            // Guarantee every 1 m cardinal edge between walkable nodes remains clear across its midpoint.
            nodeRadius = Mathf.Sqrt(sweepRadius * sweepRadius + .25f) + .02f;
            this.height = height;
        }
        public void Invalidate()
        { System.Array.Clear(clearance, 0, Count); hasWaypoint = false; nextSearch = 0; }
        static Vector3 Point(int index) => new Vector3(index % Width - 15, 0, index / Width - 15);
        static int Index(Vector3 point) => Mathf.Clamp(Mathf.RoundToInt(point.z) + 15, 0, Width - 1) * Width + Mathf.Clamp(Mathf.RoundToInt(point.x) + 15, 0, Width - 1);
        void Capsule(Vector3 foot, float r, out Vector3 bottom, out Vector3 top)
        {
            float baseY = StepClearance + r;
            bottom = new Vector3(foot.x, baseY, foot.z);
            top = new Vector3(foot.x, Mathf.Max(baseY, height - r), foot.z);
        }
        bool Walkable(int index)
        {
            if (clearance[index] == 0)
            {
                Capsule(Point(index), nodeRadius, out var a, out var b);
                clearance[index] = (sbyte)(Physics.CheckCapsule(a, b, nodeRadius, Mask, QueryTriggerInteraction.Ignore) ? -1 : 1);
            }
            return clearance[index] > 0;
        }
        bool ClearSegment(Vector3 from, Vector3 to)
        {
            Vector3 delta = Vector3.ProjectOnPlane(to - from, Vector3.up);
            if (delta.sqrMagnitude < .001f) return true;
            Capsule(from, sweepRadius, out var a, out var b);
            return !Physics.CapsuleCast(a, b, sweepRadius, delta.normalized, delta.magnitude, Mask, QueryTriggerInteraction.Ignore);
        }
        public static bool HasSight(Vector3 from, Vector3 to) =>
            !Physics.Linecast(from + Vector3.up, to + Vector3.up, Mask, QueryTriggerInteraction.Ignore);
        public Vector3 Direction(Vector3 origin, Vector3 target, float attackRange)
        {
            Vector3 delta = Vector3.ProjectOnPlane(target - origin, Vector3.up);
            var stop = origin + delta.normalized * Mathf.Max(0, delta.magnitude - attackRange + .1f);
            if (HasSight(origin, target) && ClearSegment(origin, stop)) return delta.normalized;
            if (Time.time >= nextSearch || (hasWaypoint && Vector3.ProjectOnPlane(waypoint - origin, Vector3.up).sqrMagnitude < .16f))
            { hasWaypoint = Search(origin, target, attackRange); nextSearch = Time.time + .4f; }
            if (!hasWaypoint || !ClearSegment(origin, waypoint)) return Vector3.zero;
            Vector3 step = Vector3.ProjectOnPlane(waypoint - origin, Vector3.up);
            return step.sqrMagnitude > .0001f ? step.normalized : Vector3.zero;
        }
        bool Search(Vector3 origin, Vector3 target, float range)
        {
            int center = Index(origin), start = -1; float best = float.MaxValue;
            // A controller can sit between cells or against cover; choose the nearest reachable walkable seed.
            for (int dz = -2; dz <= 2; dz++) for (int dx = -2; dx <= 2; dx++)
            {
                int x = center % Width + dx, z = center / Width + dz;
                if (x < 0 || x >= Width || z < 0 || z >= Width) continue;
                int candidate = z * Width + x;
                float distance = Vector3.ProjectOnPlane(Point(candidate) - origin, Vector3.up).sqrMagnitude;
                if (distance < best && Walkable(candidate) && ClearSegment(origin, Point(candidate)))
                { start = candidate; best = distance; }
            }
            if (start < 0) return false;
            for (int i = 0; i < Count; i++) parents[i] = -2;
            int read = 0, write = 0; queue[write++] = start; parents[start] = -1;
            while (read < write)
            {
                int node = queue[read++]; Vector3 point = Point(node);
                if (Vector3.ProjectOnPlane(point - target, Vector3.up).magnitude <= range && HasSight(point, target))
                {
                    while (parents[node] >= 0 && parents[node] != start) node = parents[node];
                    // If cutting diagonally from origin to the next node clips a corner, align to start first.
                    if (node != start && !ClearSegment(origin, Point(node))) node = start;
                    waypoint = Point(node); return true;
                }
                int x = node % Width, z = node / Width;
                if (x > 0) Visit(node - 1, node, ref write);
                if (x < Width - 1) Visit(node + 1, node, ref write);
                if (z > 0) Visit(node - Width, node, ref write);
                if (z < Width - 1) Visit(node + Width, node, ref write);
            }
            return false;
        }
        void Visit(int node, int parent, ref int write)
        {
            if (parents[node] != -2 || !Walkable(node)) return;
            parents[node] = parent; queue[write++] = node;
        }
    }
}
