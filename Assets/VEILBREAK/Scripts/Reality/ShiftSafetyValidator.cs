using System.Collections.Generic;
using UnityEngine;

namespace Veilbreak
{
    public sealed class ShiftSafetyValidator : MonoBehaviour
    {
        readonly List<GameObject> activated = new List<GameObject>();
        CapsuleCollider probe;

        public bool CanShift(CharacterController player, VeilReactiveObject[] objects, VeilState target, out string reason)
        {
            if (!player) { reason = "Shift blocked: player unavailable."; return false; }
            ActivateTargetObjects(objects, target);
            try
            {
                Physics.SyncTransforms();
                if (!CanOccupyActivated(player, objects, target))
                { reason = "Shift blocked: move away from the forming stone."; return false; }
                // Stable floor is shared in this arena. Reject shifts when support would disappear over a drop.
                float halfHeight = player.height * Mathf.Abs(player.transform.lossyScale.y) * .5f;
                Vector3 foot = player.transform.TransformPoint(player.center) - Vector3.up * halfHeight;
                foreach (var hit in Physics.RaycastAll(foot + Vector3.up * .15f, Vector3.down, 1.35f, 1 << 8, QueryTriggerInteraction.Ignore))
                {
                    var reactive = hit.collider.GetComponentInParent<VeilReactiveObject>();
                    if (!reactive || reactive.ExistsIn(target)) { reason = null; return true; }
                }
                reason = "Shift blocked: reach stable ground first.";
                return false;
            }
            finally
            {
                RestoreTargetObjects();
            }
        }

        public bool CanOccupy(CharacterController actor, VeilReactiveObject[] objects, VeilState target)
        {
            if (!actor) return false;
            ActivateTargetObjects(objects, target);
            try
            {
                Physics.SyncTransforms();
                return CanOccupyActivated(actor, objects, target);
            }
            finally
            {
                RestoreTargetObjects();
            }
        }

        void ActivateTargetObjects(VeilReactiveObject[] objects, VeilState target)
        {
            activated.Clear();
            if (objects == null) return;
            foreach (var reactive in objects)
            {
                if (!reactive || !reactive.ExistsIn(target)) continue;
                // Inactive GameObjects have no active PhysX shapes; enable destination roots transiently for exact queries.
                for (Transform current = reactive.transform; current != null; current = current.parent)
                {
                    if (!current.gameObject.activeSelf)
                    {
                        current.gameObject.SetActive(true);
                        activated.Add(current.gameObject);
                    }
                }
            }
        }

        void RestoreTargetObjects()
        {
            if (activated.Count == 0) return;
            for (int i = activated.Count - 1; i >= 0; i--)
                if (activated[i]) activated[i].SetActive(false);
            activated.Clear();
            Physics.SyncTransforms();
        }

        bool CanOccupyActivated(CharacterController actor, VeilReactiveObject[] objects, VeilState target)
        {
            if (objects == null) return true;
            CapsuleCollider capsule = ConfigureProbe(actor, out Vector3 center);
            capsule.enabled = true;
            Physics.SyncTransforms();
            try
            {
                foreach (var reactive in objects)
                {
                    if (!reactive || !reactive.ExistsIn(target)) continue;
                    foreach (Collider obstacle in reactive.GetComponentsInChildren<Collider>(true))
                    {
                        if (obstacle == actor || obstacle == capsule || obstacle.isTrigger || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy) continue;
                        if (Physics.ComputePenetration(capsule, center, Quaternion.identity,
                            obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out float depth) && depth > .025f)
                            return false;
                    }
                }
                return true;
            }
            finally
            {
                capsule.enabled = false;
            }
        }

        CapsuleCollider ConfigureProbe(CharacterController actor, out Vector3 center)
        {
            if (!probe)
            {
                var go = new GameObject("ShiftSafetyCapsuleProbe");
                go.layer = 2;
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(0, -1000f, 0);
                probe = go.AddComponent<CapsuleCollider>();
            }
            Vector3 scale = actor.transform.lossyScale;
            float radius = actor.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(radius * 2f, actor.height * Mathf.Abs(scale.y));
            probe.center = Vector3.zero;
            probe.direction = 1;
            probe.radius = radius;
            probe.height = height;
            center = actor.transform.TransformPoint(actor.center);
            return probe;
        }
    }
}
