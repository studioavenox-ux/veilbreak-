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
        [Test] public void ZeroSizedJoystickAvoidsNaNAndClearResetsLookDragAndButtons()
        {
            events = new GameObject("Test events", typeof(EventSystem));
            root = new GameObject("Touch Rig", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = Vector2.zero; // Zero width before layout must not divide by zero.

            var input = root.AddComponent<GameInput>();
            var joystick = root.AddComponent<VirtualJoystick>();
            var look = root.AddComponent<TouchLookArea>();
            var action = root.AddComponent<ActionButton>();
            look.InputSource = input;
            input.Joystick = joystick;
            input.LookArea = look;
            input.RegisterButton(action);

            var rightClick = new PointerEventData(events.GetComponent<EventSystem>())
            {
                pointerId = -2,
                button = PointerEventData.InputButton.Right,
                delta = new Vector2(20, 10)
            };
            joystick.OnPointerDown(rightClick);
            look.OnPointerDown(rightClick);
            Assert.IsFalse(look.IsDragging, "Non-left mouse buttons must not claim touch look");

            var touch = new PointerEventData(events.GetComponent<EventSystem>())
            {
                pointerId = 4,
                button = PointerEventData.InputButton.Left,
                position = new Vector2(10, 10),
                delta = new Vector2(15, -5)
            };
            joystick.OnPointerDown(touch);
            Assert.IsFalse(float.IsNaN(joystick.Value.x) || float.IsNaN(joystick.Value.y));
            Assert.AreEqual(Vector2.zero, joystick.Value);

            look.OnPointerDown(touch);
            look.OnDrag(touch);
            Vector2 delta = input.ConsumeLook();
            Assert.Greater(Mathf.Abs(delta.x), 0f, "TouchLookArea drag must forward look delta to GameInput");

            action.OnPointerDown(touch);
            Assert.IsTrue(action.IsPressed);
            input.Clear();
            Assert.IsFalse(action.IsPressed, "GameInput.Clear must release held action button pointers");
            Assert.IsFalse(look.IsDragging);
        }
    }
}
