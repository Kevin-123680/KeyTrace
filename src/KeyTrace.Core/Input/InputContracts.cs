using KeyTrace.Core.Models;

namespace KeyTrace.Core.Input;

/// <summary>修饰键状态。</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Control = 1,
    Alt = 2,
    Shift = 4,
    Windows = 8,
}

/// <summary>
/// 采集层收到的原始输入事件（只含按键码与坐标，绝不含按键字符内容）。
/// 由钩子线程直接调用，实现方必须快速返回，不能阻塞。
/// </summary>
public interface IInputSink
{
    void OnKey(int virtualKey, bool isDown, bool isInjected);

    void OnPointerMove(int x, int y, bool isInjected);

    void OnMouseButton(MouseButton button, int x, int y, bool isInjected);

    void OnMouseWheel(int delta, int x, int y, bool isInjected);
}

/// <summary>全局键鼠事件源。</summary>
public interface IInputSource : IDisposable
{
    bool IsCapturing { get; }

    string? LastError { get; }

    void Start();

    void Stop();
}

/// <summary>什么也不做的输入源，用于设计器 / 无钩子环境。</summary>
public sealed class NullInputSource : IInputSource
{
    public static NullInputSource Instance { get; } = new();

    public bool IsCapturing => false;

    public string? LastError => null;

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// 转发型 sink：用来打破"输入源需要 sink、sink 又需要输入源"的构造环。
/// 采信前设置 <see cref="Target"/> 即可。
/// </summary>
public sealed class InputSinkRouter : IInputSink
{
    public IInputSink? Target { get; set; }

    public void OnKey(int virtualKey, bool isDown, bool isInjected) => Target?.OnKey(virtualKey, isDown, isInjected);

    public void OnPointerMove(int x, int y, bool isInjected) => Target?.OnPointerMove(x, y, isInjected);

    public void OnMouseButton(MouseButton button, int x, int y, bool isInjected)
        => Target?.OnMouseButton(button, x, y, isInjected);

    public void OnMouseWheel(int delta, int x, int y, bool isInjected)
        => Target?.OnMouseWheel(delta, x, y, isInjected);
}
