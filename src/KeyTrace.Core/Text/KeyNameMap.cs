using KeyTrace.Core.Input;

namespace KeyTrace.Core.Text;

/// <summary>VK 码 → 可读名称。只做映射，不涉及任何输入内容。</summary>
public static class KeyNameMap
{
    private static readonly Dictionary<int, string> Names = BuildNames();

    public static string GetName(int virtualKey)
        => Names.TryGetValue(virtualKey, out var name) ? name : $"VK_{virtualKey:X2}";

    public static bool TryGetName(int virtualKey, out string name) => Names.TryGetValue(virtualKey, out name!);

    /// <summary>是否为"打字键"（用于 WPM 估算）：字母、数字、标点、空格、退格、回车、Tab、小键盘。</summary>
    public static bool IsTypingKey(int virtualKey)
    {
        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            return true; // A-Z
        }

        if (virtualKey is >= 0x30 and <= 0x39)
        {
            return true; // 0-9
        }

        if (virtualKey is >= 0x60 and <= 0x6F)
        {
            return true; // 小键盘 0-9 与运算符
        }

        if (virtualKey is >= 0xBA and <= 0xDE)
        {
            return true; // OEM 标点
        }

        return virtualKey is VirtualKeys.Space or VirtualKeys.Back or VirtualKeys.Return
            or VirtualKeys.Tab or 0xE2; // 0xE2: OEM 102 键（部分键盘的 \ ）
    }

    /// <summary>把修饰键与主键拼成 "Ctrl+Shift+S" 形式。</summary>
    public static string FormatCombo(KeyModifiers modifiers, int virtualKey)
    {
        var parts = new List<string>(4);
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(KeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(GetName(virtualKey));
        return string.Join('+', parts);
    }

    private static Dictionary<int, string> BuildNames()
    {
        var names = new Dictionary<int, string>(160)
        {
            [VirtualKeys.Back] = "Backspace",
            [VirtualKeys.Tab] = "Tab",
            [VirtualKeys.Return] = "Enter",
            [VirtualKeys.Shift] = "Shift",
            [VirtualKeys.Control] = "Ctrl",
            [VirtualKeys.Menu] = "Alt",
            [VirtualKeys.Pause] = "Pause",
            [VirtualKeys.Capital] = "CapsLock",
            [VirtualKeys.Escape] = "Esc",
            [VirtualKeys.Space] = "Space",
            [VirtualKeys.Prior] = "PageUp",
            [VirtualKeys.Next] = "PageDown",
            [VirtualKeys.End] = "End",
            [VirtualKeys.Home] = "Home",
            [VirtualKeys.Left] = "←",
            [VirtualKeys.Up] = "↑",
            [VirtualKeys.Right] = "→",
            [VirtualKeys.Down] = "↓",
            [VirtualKeys.Snapshot] = "PrintScreen",
            [VirtualKeys.Insert] = "Insert",
            [VirtualKeys.Delete] = "Delete",
            [VirtualKeys.LWin] = "Win",
            [VirtualKeys.RWin] = "Win",
            [VirtualKeys.Apps] = "Menu",
            [VirtualKeys.NumLock] = "NumLock",
            [VirtualKeys.Scroll] = "ScrollLock",
            [VirtualKeys.LShift] = "Shift",
            [VirtualKeys.RShift] = "Shift",
            [VirtualKeys.LControl] = "Ctrl",
            [VirtualKeys.RControl] = "Ctrl",
            [VirtualKeys.LMenu] = "Alt",
            [VirtualKeys.RMenu] = "Alt",
            [0xBA] = ";",
            [0xBB] = "=",
            [0xBC] = ",",
            [0xBD] = "-",
            [0xBE] = ".",
            [0xBF] = "/",
            [0xC0] = "`",
            [0xDB] = "[",
            [0xDC] = "\\",
            [0xDD] = "]",
            [0xDE] = "'",
            [0x6A] = "Num *",
            [0x6B] = "Num +",
            [0x6C] = "Num ,",
            [0x6D] = "Num -",
            [0x6E] = "Num .",
            [0x6F] = "Num /",
            [0xAD] = "Mute",
            [0xAE] = "Vol-",
            [0xAF] = "Vol+",
            [0xB0] = "Next Track",
            [0xB1] = "Prev Track",
            [0xB3] = "Play/Pause",
        };

        for (var key = 0x30; key <= 0x39; key++)
        {
            names[key] = ((char)key).ToString();
        }

        for (var key = 0x41; key <= 0x5A; key++)
        {
            names[key] = ((char)key).ToString();
        }

        for (var index = 0; index <= 9; index++)
        {
            names[0x60 + index] = $"Num {index}";
        }

        for (var index = 1; index <= 24; index++)
        {
            names[0x6F + index] = $"F{index}";
        }

        return names;
    }
}
