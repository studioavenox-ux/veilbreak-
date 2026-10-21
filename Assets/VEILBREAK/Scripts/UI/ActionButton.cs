using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Veilbreak
{
    public sealed class ActionButton : MonoBehaviour, IPointerDownHandler
    {
        public Action Pressed;
        public void OnPointerDown(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Pressed?.Invoke(); }
    }
}
