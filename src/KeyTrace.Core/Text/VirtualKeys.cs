namespace KeyTrace.Core.Text;

/// <summary>常用虚拟键码常量（Windows VK codes）。</summary>
public static class VirtualKeys
{
    public const int Back = 0x08;
    public const int Tab = 0x09;
    public const int Return = 0x0D;
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Menu = 0x12;
    public const int Pause = 0x13;
    public const int Capital = 0x14;
    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int Prior = 0x21;
    public const int Next = 0x22;
    public const int End = 0x23;
    public const int Home = 0x24;
    public const int Left = 0x25;
    public const int Up = 0x26;
    public const int Right = 0x27;
    public const int Down = 0x28;
    public const int Snapshot = 0x2C;
    public const int Insert = 0x2D;
    public const int Delete = 0x2E;
    public const int LWin = 0x5B;
    public const int RWin = 0x5C;
    public const int Apps = 0x5D;
    public const int NumLock = 0x90;
    public const int Scroll = 0x91;
    public const int LShift = 0xA0;
    public const int RShift = 0xA1;
    public const int LControl = 0xA2;
    public const int RControl = 0xA3;
    public const int LMenu = 0xA4;
    public const int RMenu = 0xA5;

    /// <summary>有些键盘/软件只上报通用修饰键，这里统一映射到左键，保证统计口径一致。</summary>
    public static int NormalizeModifier(int virtualKey) => virtualKey switch
    {
        Shift => LShift,
        Control => LControl,
        Menu => LMenu,
        _ => virtualKey,
    };

    public static bool IsModifier(int virtualKey) => virtualKey is
        Shift or Control or Menu or LShift or RShift or LControl or RControl or LMenu or RMenu or LWin or RWin;
}
