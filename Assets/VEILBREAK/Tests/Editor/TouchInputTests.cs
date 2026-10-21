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
