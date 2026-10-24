using UnityEngine;

namespace Veilbreak
{
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        Rect last;
        Vector2 size;
        void Awake() => Apply();
        void Update() => Apply();
        void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 currentSize = new Vector2(Screen.width, Screen.height);
            if (last == Screen.safeArea && size == currentSize) return;
            last = Screen.safeArea; size = currentSize;
            var rect = (RectTransform)transform;
            Vector2 min = Vector2.Max(Vector2.zero, Vector2.Min(Vector2.one, last.min / size));
            Vector2 max = Vector2.Max(min, Vector2.Min(Vector2.one, last.max / size));
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
