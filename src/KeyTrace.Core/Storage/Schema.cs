using Microsoft.Data.Sqlite;

namespace KeyTrace.Core.Storage;

/// <summary>数据库结构定义与版本迁移。</summary>
internal static class Schema
{
    public const int CurrentVersion = 1;

    private const string CreateSql = """
        CREATE TABLE IF NOT EXISTS daily_stats
        (
            date          TEXT    NOT NULL PRIMARY KEY,
            total_keys    INTEGER NOT NULL DEFAULT 0,
            mouse_m       REAL    NOT NULL DEFAULT 0,
            total_clicks  INTEGER NOT NULL DEFAULT 0,
            active_min    INTEGER NOT NULL DEFAULT 0,
            mouse_px      REAL    NOT NULL DEFAULT 0,
            left_clicks   INTEGER NOT NULL DEFAULT 0,
            right_clicks  INTEGER NOT NULL DEFAULT 0,
            middle_clicks INTEGER NOT NULL DEFAULT 0,
            scroll_steps  INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS key_daily
        (
            date     TEXT    NOT NULL,
            key_code INTEGER NOT NULL,
            key_name TEXT    NOT NULL,
            count    INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (date, key_code)
        );

        CREATE TABLE IF NOT EXISTS hourly_dist
        (
            date      TEXT    NOT NULL,
            hour      INTEGER NOT NULL,
            key_count INTEGER NOT NULL DEFAULT 0,
            mouse_m   REAL    NOT NULL DEFAULT 0,
            mouse_px  REAL    NOT NULL DEFAULT 0,
            PRIMARY KEY (date, hour)
        );

        CREATE TABLE IF NOT EXISTS combo_daily
        (
            date  TEXT    NOT NULL,
            combo TEXT    NOT NULL,
            count INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (date, combo)
        );

        CREATE TABLE IF NOT EXISTS click_zone
        (
            date     TEXT    NOT NULL,
            x_bucket INTEGER NOT NULL,
            y_bucket INTEGER NOT NULL,
            count    INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (date, x_bucket, y_bucket)
        );

        CREATE TABLE IF NOT EXISTS active_minute
        (
            date   TEXT    NOT NULL,
            minute INTEGER NOT NULL,
            PRIMARY KEY (date, minute)
        );

        CREATE TABLE IF NOT EXISTS achievements
        (
            id          TEXT NOT NULL PRIMARY KEY,
            unlocked_at TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS ix_key_daily_date  ON key_daily (date);
        CREATE INDEX IF NOT EXISTS ix_hourly_date     ON hourly_dist (date);
        CREATE INDEX IF NOT EXISTS ix_combo_date      ON combo_daily (date);
        CREATE INDEX IF NOT EXISTS ix_click_zone_date ON click_zone (date);
        """;

    public static void EnsureCreated(SqliteConnection connection)
    {
        var version = GetUserVersion(connection);

        if (version == 0)
        {
            using var command = connection.CreateCommand();
            command.CommandText = CreateSql;
            command.ExecuteNonQuery();
            SetUserVersion(connection, CurrentVersion);
            return;
        }

        if (version > CurrentVersion)
        {
            throw new InvalidOperationException(
                $"数据库结构版本 {version} 高于当前程序支持的 {CurrentVersion}，请升级 KeyTrace。");
        }

        // 目前只有 v1；以后新增列时在这里追加 ApplyMigrationV2(...) 之类的步骤。
    }

    public static int GetUserVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar() ?? 0);
    }

    private static void SetUserVersion(SqliteConnection connection, int version)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA user_version = {version};";
        command.ExecuteNonQuery();
    }

    /// <summary>幂等地补一列（用于未来的增量迁移）。</summary>
    internal static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        if (ColumnExists(connection, table, column))
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        command.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnection connection, string table, string column)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
