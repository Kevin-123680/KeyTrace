using System.Globalization;
using System.Text;

namespace KeyTrace.Core.Diagnostics;

/// <summary>轻量日志抽象，便于在测试中替换。</summary>
public interface IAppLogger
{
    void Info(string message);

    void Warn(string message, Exception? exception = null);

    void Error(string message, Exception? exception = null);
}

/// <summary>极简文件日志，线程安全、按大小滚动，最多保留 5 个文件。</summary>
public sealed class FileLogger : IAppLogger
{
    private const long MaxFileBytes = 2 * 1024 * 1024;
    private const int MaxFiles = 5;

    private readonly object _gate = new();
    private readonly string _directory;
    private readonly string _filePath;

    public FileLogger(string? directory = null, string fileName = "app.log")
    {
        _directory = directory ?? AppPaths.LogDirectory;
        _filePath = Path.Combine(_directory, fileName);
    }

    public string FilePath => _filePath;

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message, Exception? exception = null) => Write("WARN", message, exception);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        var builder = new StringBuilder(256);
        builder.Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
        builder.Append(" [").Append(level).Append("] ").Append(message);
        if (exception is not null)
        {
            builder.AppendLine();
            builder.Append(exception);
        }

        var line = builder.ToString();

        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(_directory);
                RotateIfNeeded();
                File.AppendAllText(_filePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // 日志本身绝不能影响主流程。
        }
    }

    private void RotateIfNeeded()
    {
        var info = new FileInfo(_filePath);
        if (!info.Exists || info.Length < MaxFileBytes)
        {
            return;
        }

        for (var index = MaxFiles - 1; index >= 1; index--)
        {
            var source = $"{_filePath}.{index}";
            var target = $"{_filePath}.{index + 1}";
            if (!File.Exists(source))
            {
                continue;
            }

            if (File.Exists(target))
            {
                File.Delete(target);
            }

            File.Move(source, target);
        }

        if (File.Exists($"{_filePath}.1"))
        {
            File.Delete($"{_filePath}.1");
        }

        File.Move(_filePath, $"{_filePath}.1");
    }
}

/// <summary>测试用空实现。</summary>
public sealed class NullLogger : IAppLogger
{
    public static NullLogger Instance { get; } = new();

    public void Info(string message)
    {
    }

    public void Warn(string message, Exception? exception = null)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
