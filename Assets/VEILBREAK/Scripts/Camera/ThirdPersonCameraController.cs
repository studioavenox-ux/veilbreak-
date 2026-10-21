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
        float yaw;
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
            // Slight framing bias, not an automatic lock-on; player rotation always remains authoritative.
            if (Guardian && Vector3.Distance(Target.position, Guardian.position) < 12)
                focus += Vector3.ClampMagnitude(Guardian.position - Target.position, 2) * .18f;
            Vector3 offset = Quaternion.Euler(pitch, yaw, 0) * Vector3.back * distance;
            float length = distance;
            if (Physics.SphereCast(focus, .22f, offset.normalized, out RaycastHit hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
                length = Mathf.Max(.5f, hit.distance - .15f);
            Vector3 desired = focus + offset.normalized * length;
            // Retract immediately at obstructions, ease out when clear.
            transform.position = snap || length < distance ? desired : Vector3.Lerp(transform.position, desired, 1 - Mathf.Exp(-smooth * Time.unscaledDeltaTime));
            transform.LookAt(focus);
        }
    }
}
