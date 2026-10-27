using KeyTrace.Core.Input;
using KeyTrace.Core.Settings;
using KeyTrace.Core.Storage;

namespace KeyTrace.Core.Tests;

/// <summary>可手动拨动的时钟，使用本机时区以模拟真实运行环境。</summary>
internal sealed class FakeClock : IClock
{
    private DateTime _local;

    public FakeClock(DateTime? start = null)
    {
        _local = start ?? new DateTime(2026, 5, 20, 9, 0, 0, DateTimeKind.Unspecified);
    }

    public DateTimeOffset Now => new(_local, TimeZoneInfo.Local.GetUtcOffset(_local));

    public void Advance(TimeSpan by) => _local = _local.Add(by);

    public void Set(DateTime local) => _local = local;
}

internal sealed class MemorySettingsStore : ISettingsStore
{
    public AppSettings Settings { get; private set; } = new();

    public string FilePath => ":memory:";

    public int SaveCount { get; private set; }

    public AppSettings Load() => Settings.Clone();

    public void Save(AppSettings settings)
    {
        Settings = settings.Clone();
        SaveCount++;
    }
}

internal sealed class FakeInputSource : IInputSource
{
    public bool IsCapturing { get; private set; }

    public string? LastError => null;

    public int StartCount { get; private set; }

    public void Start()
    {
        IsCapturing = true;
        StartCount++;
    }

    public void Stop() => IsCapturing = false;

    public void Dispose() => Stop();
}

internal sealed class StubForegroundProvider : IForegroundProvider
{
    public ForegroundWindowInfo? Current { get; set; }

    public ForegroundWindowInfo? GetForeground() => Current;
}

/// <summary>临时目录 + 真实 SQLite 文件，测试结束后清理。</summary>
internal sealed class TempRepository : IDisposable
{
    private readonly KeyTraceDatabase _database;

    public TempRepository()
    {
        // 放在测试程序集目录下：不需要额外的临时目录权限，构建清理时一并删除。
        var root = Environment.GetEnvironmentVariable("KEYTRACE_TEST_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.Combine(AppContext.BaseDirectory, "test-data");
        }

        DirectoryPath = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);

        _database = new KeyTraceDatabase(Path.Combine(DirectoryPath, "data.db"));
        Repository = new SqliteStatsRepository(_database);
        Repository.Initialize();
    }

    public SqliteStatsRepository Repository { get; }

    public string DirectoryPath { get; }

    public void Dispose()
    {
        _database.Dispose();
        try
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch
        {
            // 临时目录清理失败不影响测试结果。
        }
    }
}
