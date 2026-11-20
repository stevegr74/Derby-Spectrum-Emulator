namespace Derby;

internal enum OnScreenKeyboardLayout
{
    Spectrum48K,
    Spectrum128K
}

[Flags]
internal enum OnScreenShift
{
    None = 0,
    Caps = 1,
    Symbol = 2
}

internal sealed class OnScreenKeyStroke
{
    public OnScreenKeyStroke(IEnumerable<Keys> keys)
    {
        Positions = keys.SelectMany(SpectrumKeyboardMapping.GetPositions).Distinct().ToArray();
        Rows = Positions.Select(position => position.Row).Distinct().ToArray();
    }

    public SpectrumKeyboardMapping.MatrixPosition[] Positions { get; }
    public int[] Rows { get; }
}

internal sealed record OnScreenKey(
    string Legend, string Keyword, string Symbol, float Width, Keys[] Contacts, OnScreenShift Shift = OnScreenShift.None)
{
    public OnScreenKeyStroke CreateStroke(OnScreenShift armed)
    {
        IEnumerable<Keys> keys = Contacts;
        if (armed.HasFlag(OnScreenShift.Caps))
            keys = keys.Append(Keys.ShiftKey);
        if (armed.HasFlag(OnScreenShift.Symbol))
            keys = keys.Append(Keys.ControlKey);
        return new OnScreenKeyStroke(keys);
    }
}

internal sealed record OnScreenKeyboardRow(float Offset, OnScreenKey[] Keys);

internal static class OnScreenKeyboardLayouts
{
    private static readonly OnScreenKey[] Numbers =
    [
        Key("1", Keys.D1, "EDIT", "!"), Key("2", Keys.D2, "CAPS LOCK", "@"),
        Key("3", Keys.D3, "TRUE VIDEO", "#"), Key("4", Keys.D4, "INV VIDEO", "$"),
        Key("5", Keys.D5, "LEFT", "%"), Key("6", Keys.D6, "DOWN", "&"),
        Key("7", Keys.D7, "UP", "'"), Key("8", Keys.D8, "RIGHT", "("),
        Key("9", Keys.D9, "GRAPHICS", ")"), Key("0", Keys.D0, "DELETE", "_")
    ];

    private static readonly OnScreenKey[] TopLetters =
    [
        Key("Q", Keys.Q, "PLOT", "<="), Key("W", Keys.W, "DRAW", "<>"),
        Key("E", Keys.E, "REM", ">="), Key("R", Keys.R, "RUN", "<"),
        Key("T", Keys.T, "RAND", ">"), Key("Y", Keys.Y, "RETURN", "AND"),
        Key("U", Keys.U, "IF", "OR"), Key("I", Keys.I, "INPUT", "AT"),
        Key("O", Keys.O, "POKE", ";"), Key("P", Keys.P, "PRINT", "\"")
    ];

    private static readonly OnScreenKey[] HomeLetters =
    [
        Key("A", Keys.A, "NEW", "STOP"), Key("S", Keys.S, "SAVE", "NOT"),
        Key("D", Keys.D, "DIM", "STEP"), Key("F", Keys.F, "FOR", "TO"),
        Key("G", Keys.G, "GOTO", "THEN"), Key("H", Keys.H, "GOSUB", "^"),
        Key("J", Keys.J, "LOAD", "-"), Key("K", Keys.K, "LIST", "+"),
        Key("L", Keys.L, "LET", "=")
    ];

    private static readonly OnScreenKey[] BottomLetters =
    [
        Key("Z", Keys.Z, "COPY", ":"), Key("X", Keys.X, "CLEAR", "£"),
        Key("C", Keys.C, "CONT", "?"), Key("V", Keys.V, "CLS", "/"),
        Key("B", Keys.B, "BORDER", "*"), Key("N", Keys.N, "NEXT", ","),
        Key("M", Keys.M, "PAUSE", ".")
    ];

    private static readonly OnScreenKeyboardRow[] Rows48K =
    [
        new(0, Numbers),
        new(.25f, TopLetters),
        new(.5f, [..HomeLetters, Special("ENTER", 1, Keys.Enter)]),
        new(0, [Shift(OnScreenShift.Caps, 1.25f), ..BottomLetters,
            Shift(OnScreenShift.Symbol, 1.125f), Special("SPACE", 1.125f, Keys.Space) with { Keyword = "BREAK" }])
    ];

    private static readonly OnScreenKeyboardRow[] Rows128K =
    [
        new(0, [Special("TRUE\nVIDEO", 1, Keys.ShiftKey, Keys.D3), Special("INV\nVIDEO", 1, Keys.ShiftKey, Keys.D4),
            ..Numbers.Select(key => key with { Keyword = "" }), Special("BREAK", 1.5f, Keys.Escape)]),
        new(0, [Special("DELETE", 1.5f, Keys.Back), Special("GRAPH", 1, Keys.ShiftKey, Keys.D9), ..TopLetters]),
        new(0, [Special("EXTEND\nMODE", 1.5f, Keys.ShiftKey, Keys.ControlKey), Special("EDIT", 1.5f, Keys.ShiftKey, Keys.D1),
            ..HomeLetters, Special("ENTER", 1.5f, Keys.Enter)]),
        new(0, [Shift(OnScreenShift.Caps, 2.25f), Special("CAPS\nLOCK", 1, Keys.CapsLock),
            ..BottomLetters, Special(".", 1, Keys.OemPeriod), Shift(OnScreenShift.Caps, 2.25f)]),
        new(0, [Shift(OnScreenShift.Symbol, 1.25f), Special(";", 1, Keys.OemSemicolon),
            Special("\"", 1, Keys.OemQuotes), Special("←", 1, Keys.Left), Special("→", 1, Keys.Right),
            Special("SPACE", 4, Keys.Space), Special("↑", 1, Keys.Up), Special("↓", 1, Keys.Down),
            Special(",", 1, Keys.Oemcomma), Shift(OnScreenShift.Symbol, 1.25f)])
    ];

    public static IReadOnlyList<OnScreenKeyboardRow> GetRows(OnScreenKeyboardLayout layout) =>
        layout == OnScreenKeyboardLayout.Spectrum48K ? Rows48K : Rows128K;

    private static OnScreenKey Key(string legend, Keys key, string keyword, string symbol) =>
        new(legend, keyword, symbol, 1, [key]);

    private static OnScreenKey Special(string legend, float width, params Keys[] keys) =>
        new(legend, "", "", width, keys);

    private static OnScreenKey Shift(OnScreenShift shift, float width) =>
        new(shift == OnScreenShift.Caps ? "CAPS\nSHIFT" : "SYMBOL\nSHIFT", "", "", width, [], shift);
}
