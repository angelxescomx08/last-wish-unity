using System;
using System.Collections.Generic;
using LastWish.Presentation;
using UnityEngine.InputSystem;
using Key = LastWish.Presentation.Key;
using UKey = UnityEngine.InputSystem.Key;

namespace LastWish.Runtime
{
    /// <summary>Turns Input System devices into pygame-like events in virtual canvas coordinates.</summary>
    public sealed class InputBridge : IDisposable
    {
        private static readonly (Key key, UKey unity)[] Keys = BuildKeys();
        private readonly List<InputEvent> events = new List<InputEvent>(64);
        private readonly Queue<char> typed = new Queue<char>();
        private Keyboard hooked;
        private float lastX = -1, lastY = -1;
        private bool left, right, middle;

        private static (Key, UKey)[] BuildKeys()
        {
            var list = new List<(Key, UKey)> {
                (Key.Escape, UKey.Escape), (Key.Return, UKey.Enter), (Key.KeypadEnter, UKey.NumpadEnter), (Key.Space, UKey.Space), (Key.Tab, UKey.Tab),
                (Key.Backspace, UKey.Backspace), (Key.Delete, UKey.Delete), (Key.Left, UKey.LeftArrow), (Key.Right, UKey.RightArrow), (Key.Up, UKey.UpArrow),
                (Key.Down, UKey.DownArrow), (Key.PageUp, UKey.PageUp), (Key.PageDown, UKey.PageDown), (Key.Home, UKey.Home), (Key.End, UKey.End),
                (Key.Minus, UKey.Minus), (Key.Plus, UKey.Equals), (Key.LeftShift, UKey.LeftShift), (Key.RightShift, UKey.RightShift),
            };
            for (var i = 0; i <= 9; i++) list.Add((Key.D0 + i, UKey.Digit0 + i));
            for (var i = 0; i <= 9; i++) list.Add((Key.D0 + i, UKey.Numpad0 + i));
            for (var i = 0; i < 26; i++) list.Add((Key.A + i, UKey.A + i));
            return list.ToArray();
        }

        private void Hook()
        {
            var keyboard = Keyboard.current;
            if (keyboard == hooked) return;
            if (hooked != null) hooked.onTextInput -= OnText;
            hooked = keyboard;
            if (hooked != null) hooked.onTextInput += OnText;
        }
        private void OnText(char c) { if (!char.IsControl(c) && typed.Count < 64) typed.Enqueue(c); }

        /// <summary>Events of this frame; <paramref name="toVirtual"/> maps screen pixels (bottom-left origin) to the canvas.</summary>
        public List<InputEvent> Poll(Func<float, float, (float x, float y)> toVirtual)
        {
            events.Clear(); Hook();
            var keyboard = Keyboard.current;
            var shift = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var p = mouse.position.ReadValue();
                var (x, y) = toVirtual(p.x, p.y);
                if (x != lastX || y != lastY) { lastX = x; lastY = y; events.Add(new InputEvent(EventType.MouseMove, x, y, shift: shift)); }
                Button(mouse.leftButton.isPressed, ref left, 1, x, y, shift);
                Button(mouse.rightButton.isPressed, ref right, 3, x, y, shift);
                Button(mouse.middleButton.isPressed, ref middle, 2, x, y, shift);
                var scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0)
                {
                    var notches = Math.Abs(scroll) >= 10 ? scroll / 120f : scroll;
                    events.Add(new InputEvent(EventType.Wheel, x, y, wheel: notches, shift: shift));
                    // SDL2 (pygame 2) also reports every wheel notch as mouse button 4 (up) / 5 (down).
                    var button = notches > 0 ? 4 : 5;
                    for (var i = 0; i < Math.Max(1, (int)Math.Round(Math.Abs(notches))); i++)
                    {
                        events.Add(new InputEvent(EventType.MouseDown, x, y, button, shift: shift));
                        events.Add(new InputEvent(EventType.MouseUp, x, y, button, shift: shift));
                    }
                }
            }
            if (keyboard != null)
                foreach (var (key, unity) in Keys)
                {
                    var control = keyboard[unity];
                    if (control.wasPressedThisFrame) events.Add(new InputEvent(EventType.KeyDown, lastX, lastY, key: key, shift: shift));
                    else if (control.wasReleasedThisFrame) events.Add(new InputEvent(EventType.KeyUp, lastX, lastY, key: key, shift: shift));
                }
            while (typed.Count > 0) events.Add(new InputEvent(EventType.Text, lastX, lastY, ch: typed.Dequeue(), shift: shift));
            return events;
        }

        private void Button(bool now, ref bool was, int button, float x, float y, bool shift)
        {
            if (now == was) return;
            was = now;
            events.Add(new InputEvent(now ? EventType.MouseDown : EventType.MouseUp, x, y, button, shift: shift));
        }

        public void Dispose() { if (hooked != null) hooked.onTextInput -= OnText; hooked = null; }
    }
}
