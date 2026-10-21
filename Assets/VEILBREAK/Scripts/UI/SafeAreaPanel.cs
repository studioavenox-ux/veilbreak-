using UnityEngine;

namespace Veilbreak
{
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        Rect last;
        Vector2 size;
        void Update()
        {
            if (last == Screen.safeArea && size == new Vector2(Screen.width, Screen.height)) return;
            last = Screen.safeArea; size = new Vector2(Screen.width, Screen.height);
            var rect = (RectTransform)transform;
            rect.anchorMin = last.min / size; rect.anchorMax = last.max / size;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
