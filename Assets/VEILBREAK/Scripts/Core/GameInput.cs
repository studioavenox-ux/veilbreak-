using UnityEngine;
using UnityEngine.EventSystems;

namespace Veilbreak
{
    // Legacy input is deliberately used for a dependency-light prototype. Generator selects Input Manager.
    public sealed class GameInput : MonoBehaviour
    {
        public VirtualJoystick Joystick { private get; set; }
        public TouchLookArea LookArea { private get; set; }
        readonly System.Collections.Generic.List<RaycastResult> uiHits = new System.Collections.Generic.List<RaycastResult>();
        readonly System.Collections.Generic.List<ActionButton> buttons = new System.Collections.Generic.List<ActionButton>();
        PointerEventData mousePointer;
        EventSystem pointerSystem;
        Vector2 look;
        bool attack, dodge, shift;
        public Vector2 Move => Vector2.ClampMagnitude((Joystick ? Joystick.Value : Vector2.zero) +
            new Vector2(Input.GetKey(KeyCode.D) ? 1 : Input.GetKey(KeyCode.A) ? -1 : 0,
                        Input.GetKey(KeyCode.W) ? 1 : Input.GetKey(KeyCode.S) ? -1 : 0), 1);
        public void Attack() => attack = true;
        public void Dodge() => dodge = true;
        public void Shift() => shift = true;
        public void AddLook(Vector2 value) => look += value;
        public Vector2 ConsumeLook()
        {
            Vector2 value = look; look = Vector2.zero;
            if (!Application.isMobilePlatform)
                value += new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 12;
            return value;
        }
        public bool ConsumeAttack()
        {
            bool value = attack || (!Application.isMobilePlatform && Input.GetMouseButtonDown(0) &&
                !MouseOverUI());
            attack = false; return value;
        }
        bool MouseOverUI()
        {
            var system = EventSystem.current; if (!system) return false;
            if (pointerSystem != system) { pointerSystem = system; mousePointer = new PointerEventData(system); }
            // Query this frame's position; cached IsPointerOverGameObject can be stale before EventSystem.Update.
            mousePointer.Reset(); mousePointer.position = Input.mousePosition;
            uiHits.Clear(); system.RaycastAll(mousePointer, uiHits); return uiHits.Count > 0;
        }
        public bool ConsumeDodge() { bool v = dodge || Input.GetKeyDown(KeyCode.Space); dodge = false; return v; }
        public bool ConsumeShift() { bool v = shift || Input.GetKeyDown(KeyCode.Q); shift = false; return v; }
        public void RegisterButton(ActionButton button) { if (button && !buttons.Contains(button)) buttons.Add(button); }
        void OnApplicationFocus(bool focused) { if (!focused) Clear(); }
        void OnApplicationPause(bool paused) { if (paused) Clear(); }
        public void Clear()
        {
            attack = dodge = shift = false; look = Vector2.zero;
            if (Joystick) Joystick.ResetInput();
            if (LookArea) LookArea.ResetInput();
            foreach (var button in buttons) if (button) button.ResetInput();
        }
    }
}
