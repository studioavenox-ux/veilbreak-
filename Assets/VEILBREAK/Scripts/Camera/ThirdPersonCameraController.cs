using UnityEngine;

namespace Veilbreak
{
    public sealed class ThirdPersonCameraController : MonoBehaviour
    {
        [SerializeField] float distance = 7, pitch = 24, sensitivity = .15f, smooth = 14;
        [SerializeField] LayerMask obstructionMask = 1 << 8;
        public Transform Target { get; set; }
        public Transform Guardian { get; set; }
        public GameInput InputSource { get; set; }
        public ArenaGameManager Game { get; set; }
        public float Sensitivity { get => sensitivity; set => sensitivity = Mathf.Clamp(value, .03f, .5f); }
        float yaw, currentDistance;
        public void ResetView() { yaw = 0; pitch = 24; Snap(); }
        void LateUpdate()
        {
            if (!Target || InputSource == null) return;
            Vector2 delta = InputSource.ConsumeLook();
            if (Game != null && Game.IsPlaying) { yaw += delta.x * sensitivity; pitch = Mathf.Clamp(pitch - delta.y * sensitivity, 10, 65); }
            Position(false);
        }
        void Snap() { if (Target) Position(true); }
        void Position(bool snap)
        {
            Vector3 focus = Target.position + Vector3.up * 1.5f;
            // Slight framing bias, not an automatic lock-on; clamp against walls so focus never enters cover.
            if (Guardian && Vector3.Distance(Target.position, Guardian.position) < 12)
            {
                Vector3 bias = Vector3.ClampMagnitude(Guardian.position - Target.position, 2) * .18f;
                float biasLen = bias.magnitude;
                if (biasLen > .001f && Physics.SphereCast(focus, .24f, bias / biasLen, out RaycastHit biasHit, biasLen, obstructionMask, QueryTriggerInteraction.Ignore))
                    bias = (bias / biasLen) * Mathf.Max(0, biasHit.distance - .02f);
                focus += bias;
            }
            Vector3 offset = Quaternion.Euler(pitch, yaw, 0) * Vector3.back * distance;
            float length = distance;
            if (Physics.SphereCast(focus, .22f, offset.normalized, out RaycastHit hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
                length = Mathf.Max(.15f, hit.distance - .15f);
            // Ease only distance along the checked ray, not world position around an unchecked corner.
            currentDistance = snap || length < currentDistance ? length : Mathf.Lerp(currentDistance, length, 1 - Mathf.Exp(-smooth * Time.unscaledDeltaTime));
            transform.position = focus + offset.normalized * currentDistance;
            transform.LookAt(focus);
        }
    }
}
