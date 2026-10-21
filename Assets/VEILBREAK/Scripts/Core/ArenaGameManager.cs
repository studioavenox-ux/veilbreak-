using System;
using UnityEngine;

namespace Veilbreak
{
    public enum ArenaState { Title, Playing, Paused, Victory, Defeat }
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
        public void Initialize()
        {
            Player.Health.Died += Lose; Guardian.Health.Died += Win;
            SetState(ArenaState.Title);
        }
        void OnDestroy()
        {
            if (Player) Player.Health.Died -= Lose;
            if (Guardian) Guardian.Health.Died -= Win;
            Time.timeScale = 1;
        }
        void OnApplicationPause(bool paused) { if (paused && IsPlaying) SetState(ArenaState.Paused); }
        void Update() { if (Input.GetKeyDown(KeyCode.Escape)) TogglePause(); }
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
            Time.timeScale = 1;
            InputSource.Clear(); Player.ResetPlayer(); Combat.ResetCombat(); Veil.ResetState(); Guardian.ResetGuardian(); FollowCamera.ResetView();
            SetState(ArenaState.Playing);
        }
        void SetState(ArenaState state)
        {
            State = state; InputSource.Clear();
            Time.timeScale = IsPlaying ? 1 : 0;
            Cursor.lockState = IsPlaying && !Application.isMobilePlatform ? CursorLockMode.Confined : CursorLockMode.None;
            Cursor.visible = true; Changed?.Invoke(state);
        }
    }
}
