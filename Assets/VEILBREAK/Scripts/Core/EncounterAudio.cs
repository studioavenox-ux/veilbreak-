using UnityEngine;

namespace Veilbreak
{
    // Original generated feedback tones, not downloaded assets or licensed recordings.
    // Clips and the single 2D source are created once, reused, and released with the scene.
    public sealed class EncounterAudio : MonoBehaviour
    {
        ArenaGameManager game;
        AudioSource source;
        AudioClip swing, dodge, shift, warning, hit, result;
        float lastHealth;
        public void Initialize(ArenaGameManager manager)
        {
            game = manager;
            source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.volume = .18f; source.spatialBlend = 0;
            swing = Tone("Blade", 620, 180, .12f); dodge = Tone("Dodge", 190, 90, .13f);
            shift = Tone("Veil shift", 260, 740, .25f); warning = Tone("Guardian warning", 160, 240, .32f);
            hit = Tone("Impact", 100, 45, .1f); result = Tone("Encounter result", 390, 590, .45f);
            lastHealth = game.Player.Health.Current;
            game.Combat.SwingStarted += OnSwing; game.Combat.HitLanded += OnHit;
            game.Player.Dodged += OnDodge; game.Player.Health.Changed += OnHealth;
            game.Guardian.AttackWarned += OnWarning; game.Veil.Changed += OnShift; game.Changed += OnState;
        }
        static AudioClip Tone(string name, float start, float end, float duration)
        {
            const int rate = 16000;
            float[] samples = new float[Mathf.CeilToInt(duration * rate)];
            float phase = 0;
            for (int i=0;i<samples.Length;i++)
            {
                float t=i/(float)samples.Length;
                phase += 2*Mathf.PI*Mathf.Lerp(start,end,t)/rate;
                samples[i] = Mathf.Sin(phase)*Mathf.Sin(Mathf.PI*t)*.65f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false); clip.SetData(samples,0); return clip;
        }
        void Play(AudioClip clip) { if (game && game.IsPlaying && source) source.PlayOneShot(clip); }
        void OnSwing() => Play(swing);
        void OnDodge() => Play(dodge);
        void OnHit() => Play(hit);
        void OnWarning() => Play(warning);
        void OnShift(VeilState state) => Play(shift);
        void OnHealth()
        { if (game.Player.Health.Current < lastHealth) Play(hit); lastHealth = game.Player.Health.Current; }
        void OnState(ArenaState state)
        {
            source.Stop(); source.pitch = state == ArenaState.Defeat ? .65f : 1;
            if (state == ArenaState.Victory || state == ArenaState.Defeat) source.PlayOneShot(result);
        }
        void OnDestroy()
        {
            if (game)
            {
                game.Combat.SwingStarted -= OnSwing; game.Combat.HitLanded -= OnHit;
                game.Player.Dodged -= OnDodge; game.Player.Health.Changed -= OnHealth;
                game.Guardian.AttackWarned -= OnWarning; game.Veil.Changed -= OnShift; game.Changed -= OnState;
            }
            foreach(var clip in new[] { swing, dodge, shift, warning, hit, result }) if(clip) Destroy(clip);
        }
    }
}
