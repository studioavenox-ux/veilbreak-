using System;
using UnityEngine;

namespace Veilbreak
{
    public enum ArenaState { Title, Playing, Paused, Victory, Defeat }
    [DisallowMultipleComponent]
    public sealed class ArenaGameManager : MonoBehaviour
    {
        public ArenaState State { get; private set; } = ArenaState.Title;
        public bool IsPlaying => State == ArenaState.Playing;
        public PlayerController Player { get; set; }
        public PlayerCombat Combat { get; set; }
        public GuardianAI Guardian { get; set; }
        public VeilStateManager Veil { get; set; }
        public GameInput InputSource { get; set; }
        public ThirdPersonCameraController FollowCamera { get; set; }
        public event Action<ArenaState> Changed;
        bool initialized;
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Player.Health.Died += Lose; Guardian.Health.Died += Win;
            SetState(ArenaState.Title);
        }
        void OnDestroy()
        {
            if (Player) Player.Health.Died -= Lose;
            if (Guardian) Guardian.Health.Died -= Win;
            Time.timeScale = 1;
        }
        void OnApplicationFocus(bool focused) { if (!focused && initialized && IsPlaying) SetState(ArenaState.Paused); }
        void OnApplicationPause(bool paused) { if (paused && initialized && IsPlaying) SetState(ArenaState.Paused); }
        void Update() { if (initialized && Input.GetKeyDown(KeyCode.Escape)) TogglePause(); }
        void Win() { if (IsPlaying) SetState(ArenaState.Victory); }
        void Lose() { if (IsPlaying) SetState(ArenaState.Defeat); }
        public void TogglePause()
        {
            if (State == ArenaState.Playing) SetState(ArenaState.Paused);
            else if (State == ArenaState.Paused) SetState(ArenaState.Playing);
        }
        public void ReturnToTitle() => SetState(ArenaState.Title);
        public void Restart()
        {
            State = ArenaState.Title;
            Time.timeScale = 1;
            if (InputSource) InputSource.Clear();
            Player.ResetPlayer(); Combat.ResetCombat(); Veil.ResetState(); Guardian.ResetGuardian(); FollowCamera.ResetView();
            SetState(ArenaState.Playing);
        }
        void SetState(ArenaState state)
        {
            State = state;
            if (InputSource) InputSource.Clear();
            Time.timeScale = IsPlaying ? 1 : 0;
            Cursor.lockState = IsPlaying && !Application.isMobilePlatform ? CursorLockMode.Confined : CursorLockMode.None;
            Cursor.visible = true; Changed?.Invoke(state);
        }
    }
}
