using System;
using UnityEngine;

namespace Veilbreak
{
    [DisallowMultipleComponent]
    public sealed class VeilStateManager : MonoBehaviour
    {
        [SerializeField, Min(.1f)] float shiftCooldown = 1;
        public VeilState Current { get; private set; }
        public float CooldownRemaining => Mathf.Max(0, nextShift - Time.time);
        public VeilReactiveObject[] Objects { get; set; }
        public CharacterController GuardianBody { get; set; }
        public PlayerController Player { get; set; }
        public ShiftSafetyValidator Validator { get; set; }
        public Light Sun { get; set; }
        public Material EnergyMaterial { get; set; }
        public event Action<VeilState> Changed;
        public event Action<string> Rejected;
        float nextShift;
        void Update()
        {
            if (Player != null && Player.Game != null && Player.Game.IsPlaying && Player.InputSource != null && Player.InputSource.ConsumeShift()) TryShift();
        }
        public bool TryShift()
        {
            if (!Player || !Player.Game || !Player.Game.IsPlaying || CooldownRemaining > 0 || !Validator) return false;
            var target = Current == VeilState.Living ? VeilState.Broken : VeilState.Living;
            if (!Validator.CanShift(Player.Body, Objects, target, out string reason)) { Rejected?.Invoke(reason); return false; }
            if (GuardianBody && !Validator.CanOccupy(GuardianBody, Objects, target))
            { Rejected?.Invoke("Shift blocked: the guardian occupies forming stone."); return false; }
            Apply(target); nextShift = Time.time + shiftCooldown; return true;
        }
        public void ResetState() { nextShift = 0; Apply(VeilState.Living); }
        void Apply(VeilState state)
        {
            Current = state;
            if (Objects != null) foreach (var item in Objects) if (item) item.Apply(state);
            bool broken = state == VeilState.Broken;
            Color energy = broken ? new Color(.65f, .15f, 1) : new Color(.05f, .9f, 1);
            if (EnergyMaterial) { EnergyMaterial.color = energy; EnergyMaterial.SetColor("_EmissionColor", energy * 1.4f); }
            if (Sun)
            {
                Sun.color = broken ? new Color(.65f, .55f, 1) : new Color(1, .86f, .65f);
                Sun.intensity = broken ? .7f : 1.15f;
            }
            RenderSettings.ambientLight = broken ? new Color(.22f, .17f, .32f) : new Color(.42f, .47f, .51f);
            RenderSettings.fogColor = broken ? new Color(.065f, .025f, .12f) : new Color(.18f, .29f, .35f);
            if (Camera.main) Camera.main.backgroundColor = RenderSettings.fogColor;
            Physics.SyncTransforms(); Changed?.Invoke(state);
        }
    }
}
