using UnityEngine;

namespace Veilbreak
{
    [RequireComponent(typeof(CharacterController), typeof(HealthComponent))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] float speed = 5, turnSpeed = 14, gravity = -24;
        [SerializeField] float dodgeSpeed = 13, dodgeDuration = .27f, dodgeCooldown = .85f;
        public GameInput InputSource { get; set; }
        public Transform CameraTransform { get; set; }
        public ArenaGameManager Game { get; set; }
        public Transform Visual { get; set; }
        public CharacterController Body { get; private set; }
        public HealthComponent Health { get; private set; }
        public bool IsDodging => dodgeRemaining > 0;
        float vertical, dodgeRemaining, nextDodge;
        Vector3 dodgeDirection;
        Vector3 spawn;
        Quaternion spawnRotation;
        void Awake() { Body = GetComponent<CharacterController>(); Health = GetComponent<HealthComponent>(); }
        public void SetSpawn() { spawn = transform.position; spawnRotation = transform.rotation; }
        void Update()
        {
            if (Game == null || !Game.IsPlaying || Health.IsDead) return;
            Vector2 input = InputSource.Move;
            Vector3 forward = Vector3.ProjectOnPlane(CameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 direction = forward * input.y + right * input.x;
            if (InputSource.ConsumeDodge() && Time.time >= nextDodge)
            {
                dodgeRemaining = dodgeDuration; nextDodge = Time.time + dodgeCooldown;
                dodgeDirection = direction.sqrMagnitude > .01f ? direction.normalized : transform.forward;
            }
            bool dodging = IsDodging;
            Vector3 motion = dodging ? dodgeDirection * dodgeSpeed : direction * speed;
            dodgeRemaining = Mathf.Max(0, dodgeRemaining - Time.deltaTime);
            if (direction.sqrMagnitude > .01f && !dodging)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
            if (Body.isGrounded && vertical < 0) vertical = -2;
            vertical += gravity * Time.deltaTime;
            Body.Move((motion + Vector3.up * vertical) * Time.deltaTime);
            if (Visual) Visual.localScale = dodging ? new Vector3(1, .8f, 1.15f) : Vector3.one;
            if (transform.position.y < -6) Health.Damage(Health.Maximum);
        }
        public void ReceiveDamage(float amount)
        {
            if (!IsDodging && Game.IsPlaying) Health.Damage(amount);
        }
        public void ResetPlayer()
        {
            Body.enabled = false; transform.SetPositionAndRotation(spawn, spawnRotation); Body.enabled = true;
            vertical = dodgeRemaining = nextDodge = 0; Health.ResetHealth();
            if (Visual) Visual.localScale = Vector3.one;
        }
    }
}
