using UnityEngine;
using UnityEngine.EventSystems;

namespace Veilbreak
{
    public sealed class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public GameInput InputSource { get; set; }
        int pointer = int.MinValue;
        public void OnPointerDown(PointerEventData data) { if (pointer == int.MinValue) pointer = data.pointerId; }
        public void OnDrag(PointerEventData data)
        {
            if (data.pointerId == pointer && Application.isMobilePlatform)
                InputSource.AddLook(data.delta * (900f / Mathf.Max(Screen.height, 1)));
        }
        public void OnPointerUp(PointerEventData data) { if (data.pointerId == pointer) pointer = int.MinValue; }
        void OnDisable() => pointer = int.MinValue;
    }
}
