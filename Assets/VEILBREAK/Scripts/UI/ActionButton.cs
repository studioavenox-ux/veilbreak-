using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Veilbreak
{
    public sealed class ActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action Pressed;
        public bool IsPressed => pointer != int.MinValue;
        bool available = true;
        public bool Available
        {
            get => available;
            set { if (available == value) return; available = value; Refresh(); }
        }
        Image image;
        Color normal;
        int pointer = int.MinValue;
        void Awake() { image = GetComponent<Image>(); if (image) normal = image.color; }
        void Refresh()
        {
            if (!image) { image = GetComponent<Image>(); if (image) normal = image.color; }
            if (image) image.color = !available ? normal * new Color(.55f,.55f,.55f,1) :
                IsPressed ? new Color(.12f,.58f,.65f,1) : normal;
        }
        public void OnPointerDown(PointerEventData data)
        {
            if (!available || IsPressed || data.button != PointerEventData.InputButton.Left) return;
            pointer = data.pointerId; Refresh(); Pressed?.Invoke();
        }
        public void OnPointerUp(PointerEventData data)
        { if (pointer == data.pointerId) ResetInput(); }
        public void ResetInput() { pointer = int.MinValue; Refresh(); }
        void OnDisable() => ResetInput();
    }
}
