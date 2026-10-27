using System.Diagnostics;
using System.Text;

namespace KeyTrace.Core.Input;

/// <summary>前台窗口信息。只用于"是否应当静默"的判定，不落盘。</summary>
public sealed record ForegroundWindowInfo(IntPtr Handle, string ProcessName, string WindowTitle);

/// <summary>前台窗口提供器（测试中可替换）。</summary>
public interface IForegroundProvider
{
    ForegroundWindowInfo? GetForeground();
}

/// <summary>带缓存的前台窗口查询：钩子每秒最多触发一次真正的 Win32 查询。</summary>
public sealed class ForegroundWatcher : IForegroundProvider
{
    private readonly object _gate = new();
    private readonly int _cacheMilliseconds;

    private IntPtr _cachedHandle = IntPtr.Zero;
    private long _cachedAt;
    private ForegroundWindowInfo? _cached;

    public ForegroundWatcher(int cacheMilliseconds = 1000)
    {
        _cacheMilliseconds = Math.Max(0, cacheMilliseconds);
    }

    public ForegroundWindowInfo? GetForeground()
    {
        var handle = Win32.GetForegroundWindow();
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var now = Environment.TickCount64;
        lock (_gate)
        {
            if (handle == _cachedHandle && now - _cachedAt < _cacheMilliseconds)
            {
                return _cached;
            }
        }

        var processName = string.Empty;
        try
        {
            _ = Win32.GetWindowThreadProcessId(handle, out var processId);
            if (processId != 0)
            {
                using var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
        }
        catch
        {
            // 受保护 / 已退出 / 权限不足的进程，忽略即可。
        }

        var title = ReadWindowTitle(handle);
        var info = new ForegroundWindowInfo(handle, processName, title);

        lock (_gate)
        {
            _cachedHandle = handle;
            _cachedAt = now;
            _cached = info;
        }

        return info;
    }

    private static string ReadWindowTitle(IntPtr handle)
    {
        try
        {
            var length = Win32.GetWindowTextLength(handle);
            if (length <= 0)
            {
                return string.Empty;
            }

            var buffer = new StringBuilder(length + 1);
            _ = Win32.GetWindowText(handle, buffer, buffer.Capacity);
            return buffer.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }
}
