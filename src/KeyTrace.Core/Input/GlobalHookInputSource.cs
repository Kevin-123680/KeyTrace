using System.Runtime.InteropServices;
using KeyTrace.Core.Diagnostics;
using KeyTrace.Core.Models;

namespace KeyTrace.Core.Input;

/// <summary>
/// 基于 <c>SetWindowsHookEx(WH_KEYBOARD_LL / WH_MOUSE_LL)</c> 的全局输入源。
/// 独立后台线程 + 消息泵，回调里只做数值解包并立即返回，不拦截任何输入。
/// </summary>
public sealed class GlobalHookInputSource : IInputSource
{
    private readonly IInputSink _sink;
    private readonly IAppLogger _logger;
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Win32.LowLevelHookProc _keyboardProc;
    private readonly Win32.LowLevelHookProc _mouseProc;

    private Thread? _thread;
    private uint _threadId;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private volatile bool _running;
    private volatile string? _lastError;
    private bool _disposed;

    public GlobalHookInputSource(IInputSink sink, IAppLogger? logger = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _logger = logger ?? NullLogger.Instance;
        // 委托必须保存在字段里，否则会被 GC 回收导致 native 回调崩溃。
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
    }

    public bool IsCapturing => _running && (_keyboardHook != IntPtr.Zero || _mouseHook != IntPtr.Zero);

    public string? LastError => _lastError;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_running)
        {
            return;
        }

        _running = true;
        _ready.Reset();
        _thread = new Thread(RunHookThread)
        {
            IsBackground = true,
            Name = "KeyTrace.InputHook",
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        if (!_ready.Wait(TimeSpan.FromSeconds(5)))
        {
            _running = false;
            _lastError = "全局钩子线程启动超时。";
            throw new InvalidOperationException(_lastError);
        }

        if (_keyboardHook == IntPtr.Zero && _mouseHook == IntPtr.Zero)
        {
            _running = false;
            _lastError = $"全局钩子注册失败（Win32 错误码 {Marshal.GetLastWin32Error()}）。";
            throw new InvalidOperationException(_lastError);
        }

        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            _logger.Warn("仅部分钩子注册成功：keyboard=" + (_keyboardHook != IntPtr.Zero) + ", mouse=" + (_mouseHook != IntPtr.Zero));
        }
    }

    public void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;

        var threadId = Volatile.Read(ref _threadId);
        if (threadId != 0)
        {
            Win32.PostThreadMessage(threadId, Win32.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        }

        var thread = _thread;
        if (thread is not null && !thread.Join(TimeSpan.FromSeconds(2)))
        {
            _logger.Warn("钩子线程未在 2 秒内退出。");
        }

        _thread = null;
        _threadId = 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        _ready.Dispose();
    }

    private void RunHookThread()
    {
        _threadId = Win32.GetCurrentThreadId();
        _ = Win32.SetThreadDescription(Win32.GetCurrentThread(), "KeyTrace.InputHook");

        var module = Win32.GetModuleHandle(null);
        _keyboardHook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _keyboardProc, module, 0);
        var keyboardError = Marshal.GetLastWin32Error();
        _mouseHook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, module, 0);
        var mouseError = Marshal.GetLastWin32Error();

        if (_keyboardHook == IntPtr.Zero)
        {
            _logger.Error($"键盘钩子注册失败，错误码 {keyboardError}。");
        }

        if (_mouseHook == IntPtr.Zero)
        {
            _logger.Error($"鼠标钩子注册失败，错误码 {mouseError}。");
        }

        _ready.Set();

        while (_running && Win32.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            _ = Win32.TranslateMessage(ref message);
            _ = Win32.DispatchMessage(ref message);
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            _ = Win32.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            _ = Win32.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _running)
        {
            try
            {
                var message = (int)wParam;
                var isDown = message is Win32.WM_KEYDOWN or Win32.WM_SYSKEYDOWN;
                var isUp = message is Win32.WM_KEYUP or Win32.WM_SYSKEYUP;

                if (isDown || isUp)
                {
                    var data = Marshal.PtrToStructure<Win32.KbdLlHookStruct>(lParam);
                    _sink.OnKey((int)data.VkCode, isDown, (data.Flags & Win32.LLKHF_INJECTED) != 0);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("键盘钩子回调异常。", ex);
            }
        }

        return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _running)
        {
            try
            {
                var message = (int)wParam;
                var data = Marshal.PtrToStructure<Win32.MsllHookStruct>(lParam);
                var injected = (data.Flags & Win32.LLMHF_INJECTED) != 0;

                switch (message)
                {
                    case Win32.WM_MOUSEMOVE:
                        _sink.OnPointerMove(data.Pt.X, data.Pt.Y, injected);
                        break;

                    case Win32.WM_LBUTTONDOWN:
                        _sink.OnMouseButton(MouseButton.Left, data.Pt.X, data.Pt.Y, injected);
                        break;

                    case Win32.WM_RBUTTONDOWN:
                        _sink.OnMouseButton(MouseButton.Right, data.Pt.X, data.Pt.Y, injected);
                        break;

                    case Win32.WM_MBUTTONDOWN:
                        _sink.OnMouseButton(MouseButton.Middle, data.Pt.X, data.Pt.Y, injected);
                        break;

                    case Win32.WM_MOUSEWHEEL:
                        var delta = (short)((data.MouseData >> 16) & 0xFFFF);
                        _sink.OnMouseWheel(delta, data.Pt.X, data.Pt.Y, injected);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error("鼠标钩子回调异常。", ex);
            }
        }

        return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }
}
