using UnityEngine;

namespace Veilbreak
{
    [RequireComponent(typeof(HealthComponent), typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class GuardianAI : MonoBehaviour
    {
        public enum Phase { Observe, Approach, Telegraph, Attack, Recovery, Stagger, Defeated }
        [SerializeField] float approachSpeed = 2.2f, slamRadius = 4.2f, strikeRange = 10, strikeHalfWidth = 1.1f;
        [SerializeField] float windup = 1.3f, recovery = 1.6f, attackCooldown = 1.2f;
        [SerializeField] float slamDamage = 25, strikeDamage = 20, brokenMultiplier = 2;
        [SerializeField] float staggerThreshold = 100, staggerDuration = 1.4f;
        public PlayerController Player { get; set; }
        public VeilStateManager Veil { get; set; }
        public ArenaGameManager Game { get; set; }
        public Transform ArmorPlate { get; set; }
        public Transform Core { get; set; }
        public Transform Visual { get; set; }
        public Transform SlamMarker { get; set; }
        public Transform StrikeMarker { get; set; }
        Transform Telegraph => slam ? SlamMarker : StrikeMarker;
        public HealthComponent Health { get; private set; }
        public Phase State { get; private set; }
        public float StaggerMeter => staggerMeter;
        public string AttackName => slam ? "GROUND SLAM — CLEAR THE RING" : "ENERGY STRIKE — DODGE SIDEWAYS";
        public event System.Action AttackWarned;
        bool initialized;
        ArenaNavigator navigator;
        CharacterController body;
        float until, vertical, nextAttack, hitFlash, staggerMeter;
        bool slam = true;
        Vector3 spawn;
        Quaternion spawnRotation;
        void Awake() { Health = GetComponent<HealthComponent>(); body = GetComponent<CharacterController>(); }
        public void Initialize()
        {
            if (initialized) return;
            initialized = true; navigator = new ArenaNavigator(body.radius, body.height);
            spawn = transform.position; spawnRotation = transform.rotation;
            Veil.Changed += OnVeil; Health.Died += OnDeath; ResetGuardian();
        }
        void OnDestroy() { if (Veil) Veil.Changed -= OnVeil; if (Health) Health.Died -= OnDeath; }
        void OnVeil(VeilState state)
        {
            navigator?.Invalidate();
            if (ArmorPlate) ArmorPlate.gameObject.SetActive(state == VeilState.Living);
            if (Core) Core.localScale = Vector3.one * (state == VeilState.Broken ? .85f : .5f);
        }
        void Enter(Phase state, float duration) { State = state; until = Time.time + duration; }
        void OnDeath()
        {
            staggerMeter = 0; hitFlash = 0;
            Enter(Phase.Defeated, 0);
            if (SlamMarker) SlamMarker.gameObject.SetActive(false);
            if (StrikeMarker) StrikeMarker.gameObject.SetActive(false);
            if (Visual) { Visual.localScale = Vector3.one; Visual.localRotation = Quaternion.Euler(0, 0, 65); }
        }
        void TriggerStagger()
        {
            staggerMeter = 0;
            if (SlamMarker) SlamMarker.gameObject.SetActive(false);
            if (StrikeMarker) StrikeMarker.gameObject.SetActive(false);
            Enter(Phase.Stagger, staggerDuration);
        }
        public void ReceiveHit(float damage)
        {
            if (Game == null || !Game.IsPlaying || Health.IsDead || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage)) return;
            bool broken = Veil != null && Veil.Current == VeilState.Broken;
            float actual = damage * (broken ? brokenMultiplier : .65f);
            Health.Damage(actual);
            if (Health.IsDead) return;
            hitFlash = Time.time + .15f;
            // Only exposed Broken-core damage builds stagger; hits while already staggered do not pre-charge another stagger.
            if (!broken || State == Phase.Stagger) return;
            staggerMeter = Mathf.Min(staggerThreshold, staggerMeter + actual);
            if (State == Phase.Recovery && staggerMeter >= staggerThreshold)
                TriggerStagger();
        }
        void Update()
        {
            if (Game == null || !Game.IsPlaying || Health.IsDead || !Player) return;
            if (body.isGrounded && vertical < 0) vertical = -2;
            vertical += -24 * Time.deltaTime;
            Vector3 horizontal = Vector3.zero;
            Vector3 toPlayer = Vector3.ProjectOnPlane(Player.transform.position - transform.position, Vector3.up);
            if (Visual) Visual.localScale = Time.time < hitFlash ? new Vector3(1.06f, .96f, 1.06f) : Vector3.one;
            switch (State)
            {
                case Phase.Observe:
                    if (Time.time >= until) Enter(Phase.Approach, 0);
                    break;
                case Phase.Approach:
                    if (toPlayer.sqrMagnitude > .01f)
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer), 120 * Time.deltaTime);
                    float desired = slam ? 3.3f : 8;
                    if (toPlayer.magnitude <= desired && ArenaNavigator.HasSight(transform.position, Player.transform.position))
                    { if (Time.time >= nextAttack) BeginAttack(); }
                    else horizontal = navigator.Direction(transform.position, Player.transform.position, desired) * approachSpeed;
                    break;
                case Phase.Telegraph:
                    // Direction and origin are locked for the whole windup.
                    if (Time.time >= until)
                    {
                        Enter(Phase.Attack, .22f); ResolveAttack();
                    }
                    break;
                case Phase.Attack:
                    if (Time.time >= until)
                    {
                        if (Telegraph) Telegraph.gameObject.SetActive(false);
                        if (staggerMeter >= staggerThreshold) TriggerStagger();
                        else Enter(Phase.Recovery, recovery);
                    }
                    break;
                case Phase.Recovery:
                case Phase.Stagger:
                    if (Time.time >= until) { slam = !slam; nextAttack = Time.time + attackCooldown; Enter(Phase.Approach, 0); }
                    break;
            }
            if (Game.IsPlaying && !Health.IsDead)
                body.Move((horizontal + Vector3.up * vertical) * Time.deltaTime);
        }
        void BeginAttack()
        {
            Vector3 to = Vector3.ProjectOnPlane(Player.transform.position - transform.position, Vector3.up);
            if (to.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(to);
            Enter(Phase.Telegraph, windup);
            if (Telegraph)
            {
                Telegraph.gameObject.SetActive(true);
                Telegraph.localPosition = slam ? new Vector3(0, .025f, 0) : new Vector3(0, .025f, strikeRange * .5f);
                Telegraph.localScale = slam ? new Vector3(slamRadius * 2, .0125f, slamRadius * 2) : new Vector3(strikeHalfWidth * 2, .025f, strikeRange);
            }
            AttackWarned?.Invoke();
        }
        void ResolveAttack()
        {
            Vector3 delta = Player.transform.position - transform.position;
            Vector3 local = transform.InverseTransformDirection(delta);
            bool hit = slam ? new Vector2(delta.x, delta.z).magnitude <= slamRadius + Player.Body.radius :
                local.z >= 0 && local.z <= strikeRange && Mathf.Abs(local.x) <= strikeHalfWidth + Player.Body.radius;
            if (hit && Mathf.Abs(delta.y) < 2.5f && !Physics.Linecast(transform.position + Vector3.up, Player.transform.position + Vector3.up, 1 << 8, QueryTriggerInteraction.Ignore))
                Player.ReceiveDamage(slam ? slamDamage : strikeDamage);
        }
        public void ResetGuardian()
        {
            body.enabled = false; transform.SetPositionAndRotation(spawn, spawnRotation); body.enabled = true;
            navigator?.Invalidate();
            Health.ResetHealth(); vertical = nextAttack = hitFlash = staggerMeter = 0; slam = true;
            if (Visual) { Visual.localScale = Vector3.one; Visual.localRotation = Quaternion.identity; }
            if (SlamMarker) SlamMarker.gameObject.SetActive(false);
            if (StrikeMarker) StrikeMarker.gameObject.SetActive(false);
            Enter(Phase.Observe, 1.5f);
            if (Veil) OnVeil(Veil.Current);
        }
    }
}
