using UnityEngine;
using UnityEngine.EventSystems;

namespace Veilbreak
{
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform Knob { get; set; }
        public Vector2 Value { get; private set; }
        int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData data)
        { if (pointer != int.MinValue) return; pointer = data.pointerId; OnDrag(data); }
        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId != pointer) return;
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out Vector2 point);
            float radius = rect.rect.width * .36f;
            Value = Vector2.ClampMagnitude(point / radius, 1); Knob.anchoredPosition = Value * radius;
        }
        public void OnPointerUp(PointerEventData data) { if (data.pointerId == pointer) ResetInput(); }
        public void ResetInput() { pointer = int.MinValue; Value = Vector2.zero; if (Knob) Knob.anchoredPosition = Vector2.zero; }
        void OnDisable() => ResetInput();
    }
}
