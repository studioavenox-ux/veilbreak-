using UnityEngine;

namespace Veilbreak
{
    public sealed class ShiftSafetyValidator : MonoBehaviour
    {
        public bool CanShift(CharacterController player, VeilReactiveObject[] objects, VeilState target, out string reason)
        {
            Physics.SyncTransforms();
            if (!CanOccupy(player, objects, target))
            { reason = "Shift blocked: move away from the forming stone."; return false; }
            // Stable floor is shared in this arena. Reject shifts when support would disappear over a drop.
            Vector3 foot = player.transform.TransformPoint(player.center) - Vector3.up * (player.height * .5f);
            foreach (var hit in Physics.RaycastAll(foot + Vector3.up * .15f, Vector3.down, 1.35f, 1 << 8, QueryTriggerInteraction.Ignore))
            {
                var reactive = hit.collider.GetComponentInParent<VeilReactiveObject>();
                if (!reactive || reactive.ExistsIn(target)) { reason = null; return true; }
            }
            reason = "Shift blocked: reach stable ground first."; return false;
        }
        public bool CanOccupy(CharacterController player, VeilReactiveObject[] objects, VeilState target)
        {
            foreach (var reactive in objects)
            {
                if (!reactive.ExistsIn(target)) continue;
                foreach (Collider obstacle in reactive.GetComponentsInChildren<Collider>(true))
                {
                    if (obstacle.isTrigger || !obstacle.enabled) continue;
                    // ComputePenetration uses geometry directly, including inactive destination objects.
                    if (Physics.ComputePenetration(player, player.transform.position, player.transform.rotation,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out float depth) && depth > .025f)
                    return false;
                }
            }
            return true;
        }
    }
}
