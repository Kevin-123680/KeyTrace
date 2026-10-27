using Microsoft.Data.Sqlite;

namespace KeyTrace.Core.Storage;

/// <summary>
/// SQLite 连接包装。单连接 + 锁 + WAL：
/// 写入是每 30 秒一次的批量 UPSERT，读是界面查询，冲突概率极低，单连接最简单也最稳。
/// </summary>
public sealed class KeyTraceDatabase : IDisposable
{
    private readonly object _gate = new();
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public KeyTraceDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("数据库路径不能为空。", nameof(databasePath));
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        };

        _connection = new SqliteConnection(builder.ToString());
        _connection.Open();

        using var pragma = _connection.CreateCommand();
        pragma.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA temp_store = MEMORY;
            PRAGMA busy_timeout = 5000;
            """;
        pragma.ExecuteNonQuery();
    }

    public string DatabasePath => _connection.DataSource;

    /// <summary>在连接锁内执行一段只读查询。</summary>
    public T Read<T>(Func<SqliteConnection, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate)
        {
            ThrowIfDisposed();
            return action(_connection);
        }
    }

    /// <summary>在连接锁内执行一段写入（可自行开启事务）。</summary>
    public void Write(Action<SqliteConnection> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate)
        {
            ThrowIfDisposed();
            action(_connection);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                using var checkpoint = _connection.CreateCommand();
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                checkpoint.ExecuteNonQuery();
            }
            catch
            {
                // 关闭阶段的清理失败无需处理。
            }

            _connection.Dispose();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
