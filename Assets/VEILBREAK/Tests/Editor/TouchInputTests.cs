using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Veilbreak.Tests
{
    public sealed class TouchInputTests
    {
        GameObject root, events;
        [TearDown] public void Cleanup()
        {
            if (root) Object.DestroyImmediate(root);
            if (events) Object.DestroyImmediate(events);
        }
        [Test] public void ActionPressOwnsOneFingerAndResetsItsVisual()
        {
            events = new GameObject("Test events", typeof(EventSystem));
            root = new GameObject("Button", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var button = root.AddComponent<ActionButton>(); var image = root.GetComponent<UnityEngine.UI.Image>();
            Color normal = image.color; int count=0; button.Pressed += () => count++;
            var first = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId=1 };
            var second = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId=2 };
            button.OnPointerDown(first); button.OnPointerDown(second);
            Assert.AreEqual(1,count); Assert.AreNotEqual(normal,image.color);
            button.OnPointerUp(second); Assert.IsTrue(button.IsPressed);
            button.OnPointerUp(first); Assert.IsFalse(button.IsPressed); Assert.AreEqual(normal,image.color);
            button.Available=false; button.OnPointerDown(first); Assert.AreEqual(1,count);
            button.Available=true; button.OnPointerDown(first); Assert.AreEqual(2,count);
        }
        [Test] public void ClearReleasesCameraPointerAndAllowsANewFinger()
        {
            events = new GameObject("Test events", typeof(EventSystem));
            root = new GameObject("Input");
            var input = root.AddComponent<GameInput>();
            var area = root.AddComponent<TouchLookArea>(); area.InputSource = input; input.LookArea = area;
            var first = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 1 };
            var second = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 2 };
            area.OnPointerDown(first);
            area.OnPointerDown(second); // Second finger cannot steal the current owner.
            area.OnPointerUp(second); Assert.IsTrue(area.IsDragging);
            input.Clear(); Assert.IsFalse(area.IsDragging, "Lost-focus/state reset must release a missing pointer-up");
            area.OnPointerDown(second); Assert.IsTrue(area.IsDragging);
            area.OnPointerUp(first); Assert.IsTrue(area.IsDragging);
            area.OnPointerUp(second); Assert.IsFalse(area.IsDragging);
        }
    }
}
