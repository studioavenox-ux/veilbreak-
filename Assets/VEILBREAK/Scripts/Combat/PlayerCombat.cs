using UnityEngine;

namespace Veilbreak
{
    public sealed class PlayerCombat : MonoBehaviour
    {
        [SerializeField] float range = 3.4f, cooldown = .55f, windup = .14f, damage = 18;
        [SerializeField, Range(0, 180)] float halfAngle = 75;
        public PlayerController Player { get; set; }
        public GuardianAI Guardian { get; set; }
        public Transform BladePivot { get; set; }
        public event System.Action SwingStarted;
        public event System.Action HitLanded;
        public bool IsAttacking => hitAt >= 0;
        float nextAttack, hitAt = -1, feedbackEnd;
        public void ResetCombat() { nextAttack = feedbackEnd = 0; hitAt = -1; if (BladePivot) BladePivot.localRotation = Quaternion.identity; }
        void Update()
        {
            if (!Player.Game.IsPlaying) return;
            // Dodge cancels a pending melee hit rather than allowing damage while evading.
            if (Player.IsDodging) { hitAt = -1; feedbackEnd = 0; }
            if (Player.InputSource.ConsumeAttack() && Time.time >= nextAttack && !Player.IsDodging)
            {
                nextAttack = Time.time + cooldown; hitAt = Time.time + windup; feedbackEnd = Time.time + .32f; SwingStarted?.Invoke();
            }
            if (BladePivot) BladePivot.localRotation = Time.time < feedbackEnd
                ? Quaternion.Euler(0, Mathf.Lerp(-65, 90, 1 - (feedbackEnd - Time.time) / .32f), -30) : Quaternion.identity;
            if (hitAt < 0 || Time.time < hitAt) return;
            hitAt = -1; // Exactly one damage evaluation per swing.
            Vector3 to = Vector3.ProjectOnPlane(Guardian.transform.position - transform.position, Vector3.up);
            if (!Guardian.Health.IsDead && to.magnitude <= range && Vector3.Angle(transform.forward, to) <= halfAngle &&
                !Physics.Linecast(transform.position + Vector3.up, Guardian.transform.position + Vector3.up, 1 << 8))
            { Guardian.ReceiveHit(damage); HitLanded?.Invoke(); }
        }
    }
}
