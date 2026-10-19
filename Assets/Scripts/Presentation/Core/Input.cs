namespace LastWish.Presentation
{
    public enum EventType { MouseMove, MouseDown, MouseUp, Wheel, KeyDown, KeyUp, Text }

    /// <summary>Keys the original game reads (pygame.K_*).</summary>
    public enum Key
    {
        None, Escape, Return, KeypadEnter, Space, Tab, Backspace, Delete, Left, Right, Up, Down, PageUp, PageDown, Home, End,
        D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
        A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Minus, Plus, LeftShift, RightShift,
    }

    /// <summary>One input event in virtual canvas coordinates (a pygame.event.Event).</summary>
    public readonly struct InputEvent
    {
        public readonly EventType Type;
        public readonly float X, Y;
        /// <summary>Mouse button: 1 left, 2 middle, 3 right.</summary>
        public readonly int Button;
        public readonly float Wheel;
        public readonly Key Key;
        public readonly bool Repeat;
        public readonly bool Shift;
        public readonly char Char;
        public InputEvent(EventType type, float x = 0, float y = 0, int button = 0, float wheel = 0, Key key = Key.None, bool repeat = false, bool shift = false, char ch = '\0')
        { Type = type; X = x; Y = y; Button = button; Wheel = wheel; Key = key; Repeat = repeat; Shift = shift; Char = ch; }
        public (float x, float y) Pos => (X, Y);
        public bool IsLeftDown => Type == EventType.MouseDown && Button == 1;
        public bool IsLeftUp => Type == EventType.MouseUp && Button == 1;
        public bool IsRightDown => Type == EventType.MouseDown && Button == 3;
        public bool IsKey(Key key) => Type == EventType.KeyDown && Key == key;
        public bool IsConfirmKey => Type == EventType.KeyDown && (Key == Key.Return || Key == Key.KeypadEnter || Key == Key.Space);
        public int Digit => Key >= Key.D0 && Key <= Key.D9 ? Key - Key.D0 : -1;
    }

    /// <summary>Live input state a scene may poll (pygame.mouse.get_pos / get_pressed / key.get_mods).</summary>
    public sealed class InputState
    {
        public float MouseX { get; internal set; } = -1;
        public float MouseY { get; internal set; } = -1;
        public bool LeftDown { get; internal set; }
        public bool RightDown { get; internal set; }
        public bool Shift { get; internal set; }
        public (float x, float y) Mouse => (MouseX, MouseY);
        public void Apply(in InputEvent e)
        {
            if (e.Type == EventType.MouseMove || e.Type == EventType.MouseDown || e.Type == EventType.MouseUp) { MouseX = e.X; MouseY = e.Y; }
            if (e.Type == EventType.MouseDown) { if (e.Button == 1) LeftDown = true; if (e.Button == 3) RightDown = true; }
            if (e.Type == EventType.MouseUp) { if (e.Button == 1) LeftDown = false; if (e.Button == 3) RightDown = false; }
            if (e.Type == EventType.KeyDown || e.Type == EventType.KeyUp) Shift = e.Shift;
        }
    }
}
