namespace KeyTrace.Core.Text;

/// <summary>热图上的一个键位（宽度以 1 个标准键为单位）。</summary>
public sealed class KeyDefinition
{
    public KeyDefinition(int virtualKey, string label, double widthUnits = 1.0)
    {
        VirtualKey = virtualKey;
        Label = label;
        WidthUnits = widthUnits;
    }

    public int VirtualKey { get; }

    public string Label { get; }

    public double WidthUnits { get; }

    /// <summary>是否为功能键（用于样式区分）。</summary>
    public bool IsModifier => VirtualKeys.IsModifier(VirtualKey);
}

/// <summary>QWERTY 104 键布局，五行等宽（每行 15 个单位）。</summary>
public static class KeyboardLayout
{
    private static readonly KeyDefinition[][] Layout =
    {
        new[]
        {
            new KeyDefinition(0xC0, "`"), new KeyDefinition(0x31, "1"), new KeyDefinition(0x32, "2"),
            new KeyDefinition(0x33, "3"), new KeyDefinition(0x34, "4"), new KeyDefinition(0x35, "5"),
            new KeyDefinition(0x36, "6"), new KeyDefinition(0x37, "7"), new KeyDefinition(0x38, "8"),
            new KeyDefinition(0x39, "9"), new KeyDefinition(0x30, "0"), new KeyDefinition(0xBD, "-"),
            new KeyDefinition(0xBB, "="), new KeyDefinition(VirtualKeys.Back, "Backspace", 2.0),
        },
        new[]
        {
            new KeyDefinition(VirtualKeys.Tab, "Tab", 1.5), new KeyDefinition(0x51, "Q"),
            new KeyDefinition(0x57, "W"), new KeyDefinition(0x45, "E"), new KeyDefinition(0x52, "R"),
            new KeyDefinition(0x54, "T"), new KeyDefinition(0x59, "Y"), new KeyDefinition(0x55, "U"),
            new KeyDefinition(0x49, "I"), new KeyDefinition(0x4F, "O"), new KeyDefinition(0x50, "P"),
            new KeyDefinition(0xDB, "["), new KeyDefinition(0xDD, "]"),
            new KeyDefinition(0xDC, "\\", 1.5),
        },
        new[]
        {
            new KeyDefinition(VirtualKeys.Capital, "Caps", 1.75), new KeyDefinition(0x41, "A"),
            new KeyDefinition(0x53, "S"), new KeyDefinition(0x44, "D"), new KeyDefinition(0x46, "F"),
            new KeyDefinition(0x47, "G"), new KeyDefinition(0x48, "H"), new KeyDefinition(0x4A, "J"),
            new KeyDefinition(0x4B, "K"), new KeyDefinition(0x4C, "L"), new KeyDefinition(0xBA, ";"),
            new KeyDefinition(0xDE, "'"), new KeyDefinition(VirtualKeys.Return, "Enter", 2.25),
        },
        new[]
        {
            new KeyDefinition(VirtualKeys.LShift, "Shift", 2.25), new KeyDefinition(0x5A, "Z"),
            new KeyDefinition(0x58, "X"), new KeyDefinition(0x43, "C"), new KeyDefinition(0x56, "V"),
            new KeyDefinition(0x42, "B"), new KeyDefinition(0x4E, "N"), new KeyDefinition(0x4D, "M"),
            new KeyDefinition(0xBC, ","), new KeyDefinition(0xBE, "."), new KeyDefinition(0xBF, "/"),
            new KeyDefinition(VirtualKeys.RShift, "Shift", 2.75),
        },
        new[]
        {
            new KeyDefinition(VirtualKeys.LControl, "Ctrl", 1.25), new KeyDefinition(VirtualKeys.LWin, "Win", 1.25),
            new KeyDefinition(VirtualKeys.LMenu, "Alt", 1.25), new KeyDefinition(VirtualKeys.Space, "Space", 6.25),
            new KeyDefinition(VirtualKeys.RMenu, "Alt", 1.25), new KeyDefinition(VirtualKeys.RWin, "Win", 1.25),
            new KeyDefinition(VirtualKeys.Apps, "Menu", 1.25), new KeyDefinition(VirtualKeys.RControl, "Ctrl", 1.25),
        },
    };

    public static IReadOnlyList<IReadOnlyList<KeyDefinition>> Rows { get; } =
        Layout.Select(static row => (IReadOnlyList<KeyDefinition>)row).ToList();

    /// <summary>所有真实键位（不含镜像键），用于统计"键盘覆盖率"。</summary>
    public static int KeyCount => Layout.Sum(static row => row.Length);
}
