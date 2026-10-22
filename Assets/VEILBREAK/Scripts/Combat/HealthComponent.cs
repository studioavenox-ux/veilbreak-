using System;
using UnityEngine;

namespace Veilbreak
{
    public sealed class HealthComponent : MonoBehaviour
    {
        [SerializeField, Min(1)] float maximum = 100;
        public float Maximum => maximum;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0;
        public event Action Changed;
        public event Action Died;
        void Awake() => ResetHealth();
        public void Configure(float value) { maximum = float.IsNaN(value) || float.IsInfinity(value) ? 100 : Mathf.Max(1, value); ResetHealth(); }
        public void ResetHealth() { Current = maximum; Changed?.Invoke(); }
        public void Damage(float amount)
        {
            if (IsDead || amount <= 0 || (float.IsNaN(amount) || float.IsInfinity(amount))) return;
            Current = Mathf.Max(0, Current - amount);
            Changed?.Invoke();
            if (IsDead) Died?.Invoke();
        }
    }
}
