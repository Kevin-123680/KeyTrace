using System.Globalization;
using KeyTrace.Core.Aggregation;
using KeyTrace.Core.Models;
using KeyTrace.Core.Text;
using Microsoft.Data.Sqlite;

namespace KeyTrace.Core.Storage;

/// <summary>基于 SQLite 的统计仓储。所有写操作是一次事务内的批量 UPSERT。</summary>
public sealed class SqliteStatsRepository : IStatsRepository
{
    private const string DateFormat = "yyyy-MM-dd";

    private readonly KeyTraceDatabase _database;

    public SqliteStatsRepository(KeyTraceDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public SqliteStatsRepository(string databasePath)
        : this(new KeyTraceDatabase(databasePath))
    {
    }

    public void Initialize() => _database.Write(connection => Schema.EnsureCreated(connection));

    public void ApplyDelta(AggregateDelta delta, double metersPerPixel)
    {
        ArgumentNullException.ThrowIfNull(delta);
        if (delta.IsEmpty)
        {
            return;
        }

        if (double.IsNaN(metersPerPixel) || metersPerPixel <= 0)
        {
            metersPerPixel = Calibration.CalibrationProfile.DefaultMetersPerPixel;
        }

        _database.Write(connection =>
        {
            using var transaction = connection.BeginTransaction();

            using var hourlyCommand = connection.CreateCommand();
            hourlyCommand.Transaction = transaction;
            hourlyCommand.CommandText = """
                INSERT INTO hourly_dist (date, hour, key_count, mouse_m, mouse_px)
                VALUES ($date, $hour, $keys, $meters, $pixels)
                ON CONFLICT(date, hour) DO UPDATE SET
                    key_count = key_count + excluded.key_count,
                    mouse_m   = mouse_m + excluded.mouse_m,
                    mouse_px  = mouse_px + excluded.mouse_px;
                """;
            var hourlyDate = AddParameter(hourlyCommand, "$date", SqliteType.Text);
            var hourlyHour = AddParameter(hourlyCommand, "$hour", SqliteType.Integer);
            var hourlyKeys = AddParameter(hourlyCommand, "$keys", SqliteType.Integer);
            var hourlyMeters = AddParameter(hourlyCommand, "$meters", SqliteType.Real);
            var hourlyPixels = AddParameter(hourlyCommand, "$pixels", SqliteType.Real);

            using var dailyCommand = connection.CreateCommand();
            dailyCommand.Transaction = transaction;
            dailyCommand.CommandText = """
                INSERT INTO daily_stats
                    (date, total_keys, mouse_m, total_clicks, active_min, mouse_px,
                     left_clicks, right_clicks, middle_clicks, scroll_steps)
                VALUES ($date, $keys, $meters, $clicks, 0, $pixels, $left, $right, $middle, $scroll)
                ON CONFLICT(date) DO UPDATE SET
                    total_keys    = total_keys + excluded.total_keys,
                    mouse_m       = mouse_m + excluded.mouse_m,
                    total_clicks  = total_clicks + excluded.total_clicks,
                    mouse_px      = mouse_px + excluded.mouse_px,
                    left_clicks   = left_clicks + excluded.left_clicks,
                    right_clicks  = right_clicks + excluded.right_clicks,
                    middle_clicks = middle_clicks + excluded.middle_clicks,
                    scroll_steps  = scroll_steps + excluded.scroll_steps;
                """;
            var dailyDate = AddParameter(dailyCommand, "$date", SqliteType.Text);
            var dailyKeys = AddParameter(dailyCommand, "$keys", SqliteType.Integer);
            var dailyMeters = AddParameter(dailyCommand, "$meters", SqliteType.Real);
            var dailyClicks = AddParameter(dailyCommand, "$clicks", SqliteType.Integer);
            var dailyPixels = AddParameter(dailyCommand, "$pixels", SqliteType.Real);
            var dailyLeft = AddParameter(dailyCommand, "$left", SqliteType.Integer);
            var dailyRight = AddParameter(dailyCommand, "$right", SqliteType.Integer);
            var dailyMiddle = AddParameter(dailyCommand, "$middle", SqliteType.Integer);
            var dailyScroll = AddParameter(dailyCommand, "$scroll", SqliteType.Integer);

            using var keyCommand = connection.CreateCommand();
            keyCommand.Transaction = transaction;
            keyCommand.CommandText = """
                INSERT INTO key_daily (date, key_code, key_name, count)
                VALUES ($date, $code, $name, $count)
                ON CONFLICT(date, key_code) DO UPDATE SET
                    count    = count + excluded.count,
                    key_name = excluded.key_name;
                """;
            var keyDate = AddParameter(keyCommand, "$date", SqliteType.Text);
            var keyCode = AddParameter(keyCommand, "$code", SqliteType.Integer);
            var keyName = AddParameter(keyCommand, "$name", SqliteType.Text);
            var keyCount = AddParameter(keyCommand, "$count", SqliteType.Integer);

            using var comboCommand = connection.CreateCommand();
            comboCommand.Transaction = transaction;
            comboCommand.CommandText = """
                INSERT INTO combo_daily (date, combo, count)
                VALUES ($date, $combo, $count)
                ON CONFLICT(date, combo) DO UPDATE SET count = count + excluded.count;
                """;
            var comboDate = AddParameter(comboCommand, "$date", SqliteType.Text);
            var comboName = AddParameter(comboCommand, "$combo", SqliteType.Text);
            var comboCount = AddParameter(comboCommand, "$count", SqliteType.Integer);

            using var zoneCommand = connection.CreateCommand();
            zoneCommand.Transaction = transaction;
            zoneCommand.CommandText = """
                INSERT INTO click_zone (date, x_bucket, y_bucket, count)
                VALUES ($date, $x, $y, $count)
                ON CONFLICT(date, x_bucket, y_bucket) DO UPDATE SET count = count + excluded.count;
                """;
            var zoneDate = AddParameter(zoneCommand, "$date", SqliteType.Text);
            var zoneX = AddParameter(zoneCommand, "$x", SqliteType.Integer);
            var zoneY = AddParameter(zoneCommand, "$y", SqliteType.Integer);
            var zoneCount = AddParameter(zoneCommand, "$count", SqliteType.Integer);

            using var minuteCommand = connection.CreateCommand();
            minuteCommand.Transaction = transaction;
            minuteCommand.CommandText = "INSERT OR IGNORE INTO active_minute (date, minute) VALUES ($date, $minute);";
            var minuteDate = AddParameter(minuteCommand, "$date", SqliteType.Text);
            var minuteValue = AddParameter(minuteCommand, "$minute", SqliteType.Integer);

            using var activeMinutesCommand = connection.CreateCommand();
            activeMinutesCommand.Transaction = transaction;
            activeMinutesCommand.CommandText = """
                UPDATE daily_stats
                SET active_min = MAX(active_min, (SELECT COUNT(*) FROM active_minute WHERE date = $date))
                WHERE date = $date;
                """;
            var activeDate = AddParameter(activeMinutesCommand, "$date", SqliteType.Text);

            foreach (var hour in delta.Hours)
            {
                var dateText = FormatDate(hour.Date);
                var meters = hour.MousePixels * metersPerPixel;

                hourlyDate.Value = dateText;
                hourlyHour.Value = hour.Hour;
                hourlyKeys.Value = hour.KeyCount;
                hourlyMeters.Value = meters;
                hourlyPixels.Value = (double)hour.MousePixels;
                hourlyCommand.ExecuteNonQuery();

                dailyDate.Value = dateText;
                dailyKeys.Value = hour.KeyCount;
                dailyMeters.Value = meters;
                dailyClicks.Value = hour.ClickCount;
                dailyPixels.Value = (double)hour.MousePixels;
                dailyLeft.Value = hour.LeftClicks;
                dailyRight.Value = hour.RightClicks;
                dailyMiddle.Value = hour.MiddleClicks;
                dailyScroll.Value = hour.ScrollSteps;
                dailyCommand.ExecuteNonQuery();

                foreach (var keyPair in hour.KeyCounts)
                {
                    keyDate.Value = dateText;
                    keyCode.Value = keyPair.Key;
                    keyName.Value = KeyNameMap.GetName(keyPair.Key);
                    keyCount.Value = keyPair.Value;
                    keyCommand.ExecuteNonQuery();
                }

                foreach (var comboPair in hour.ComboCounts)
                {
                    comboDate.Value = dateText;
                    comboName.Value = comboPair.Key;
                    comboCount.Value = comboPair.Value;
                    comboCommand.ExecuteNonQuery();
                }

                foreach (var zonePair in hour.ClickZones)
                {
                    var (column, row) = ClickZoneGrid.ToCell(zonePair.Key);
                    zoneDate.Value = dateText;
                    zoneX.Value = column;
                    zoneY.Value = row;
                    zoneCount.Value = zonePair.Value;
                    zoneCommand.ExecuteNonQuery();
                }
            }

            foreach (var pair in delta.ActiveMinutes)
            {
                var dateText = FormatDate(pair.Key);
                foreach (var minute in pair.Value)
                {
                    minuteDate.Value = dateText;
                    minuteValue.Value = minute;
                    minuteCommand.ExecuteNonQuery();
                }

                activeDate.Value = dateText;
                activeMinutesCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        });
    }

    public DailyStat? GetDaily(DateOnly date) => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT date, total_keys, mouse_m, total_clicks, active_min, mouse_px,
                   left_clicks, right_clicks, middle_clicks, scroll_steps
            FROM daily_stats WHERE date = $date;
            """;
        command.Parameters.AddWithValue("$date", FormatDate(date));

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadDaily(reader) : null;
    });

    public IReadOnlyList<DailyStat> GetDailyRange(DateOnly from, DateOnly to) => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT date, total_keys, mouse_m, total_clicks, active_min, mouse_px,
                   left_clicks, right_clicks, middle_clicks, scroll_steps
            FROM daily_stats
            WHERE date BETWEEN $from AND $to
            ORDER BY date;
            """;
        command.Parameters.AddWithValue("$from", FormatDate(from));
        command.Parameters.AddWithValue("$to", FormatDate(to));

        var results = new List<DailyStat>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadDaily(reader));
        }

        return results;
    });

    public IReadOnlyList<KeyDailyStat> GetKeys(DateOnly date) => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT date, key_code, key_name, count
            FROM key_daily
            WHERE date = $date
            ORDER BY count DESC, key_code;
            """;
        command.Parameters.AddWithValue("$date", FormatDate(date));

        var results = new List<KeyDailyStat>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new KeyDailyStat
            {
                Date = ParseDate(reader.GetString(0)),
                KeyCode = reader.GetInt32(1),
                KeyName = reader.GetString(2),
                Count = reader.GetInt32(3),
            });
        }

        return results;
    });

    public IReadOnlyList<KeySummary> GetKeyTotals(DateOnly from, DateOnly to) => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT key_code, MAX(key_name), SUM(count) AS total
            FROM key_daily
            WHERE date BETWEEN $from AND $to
            GROUP BY key_code
            ORDER BY total DESC, key_code;
            """;
        command.Parameters.AddWithValue("$from", FormatDate(from));
        command.Parameters.AddWithValue("$to", FormatDate(to));

        var results = new List<KeySummary>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new KeySummary
            {
                KeyCode = reader.GetInt32(0),
                KeyName = reader.GetString(1),
                Count = reader.GetInt32(2),
            });
        }

        return results;
    });

    public IReadOnlyList<HourlyStat> GetHourly(DateOnly date) => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT date, hour, key_count, mouse_m, mouse_px
            FROM hourly_dist
            WHERE date = $date
            ORDER BY hour;
            """;
        command.Parameters.AddWithValue("$date", FormatDate(date));

        var results = new List<HourlyStat>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadHourly(reader));
        }

        return results;
    });

    public IReadOnlyList<HourlyStat> GetHourlyRange(DateOnly from, DateOnly to) => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT date, hour, key_count, mouse_m, mouse_px
            FROM hourly_dist
            WHERE date BETWEEN $from AND $to
            ORDER BY date, hour;
            """;
        command.Parameters.AddWithValue("$from", FormatDate(from));
        command.Parameters.AddWithValue("$to", FormatDate(to));

        var results = new List<HourlyStat>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadHourly(reader));
        }

        return results;
    });

    public IReadOnlyList<ComboSummary> GetComboTotals(DateOnly from, DateOnly to, int limit = 10) =>
        _database.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT combo, SUM(count) AS total
                FROM combo_daily
                WHERE date BETWEEN $from AND $to
                GROUP BY combo
                ORDER BY total DESC, combo
                LIMIT $limit;
                """;
            command.Parameters.AddWithValue("$from", FormatDate(from));
            command.Parameters.AddWithValue("$to", FormatDate(to));
            command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 500));

            var results = new List<ComboSummary>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new ComboSummary { Combo = reader.GetString(0), Count = reader.GetInt32(1) });
            }

            return results;
        });

    public IReadOnlyList<ClickZoneStat> GetClickZones(DateOnly from, DateOnly to) => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT date, x_bucket, y_bucket, count
            FROM click_zone
            WHERE date BETWEEN $from AND $to
            ORDER BY count DESC;
            """;
        command.Parameters.AddWithValue("$from", FormatDate(from));
        command.Parameters.AddWithValue("$to", FormatDate(to));

        var results = new List<ClickZoneStat>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new ClickZoneStat
            {
                Date = ParseDate(reader.GetString(0)),
                XBucket = reader.GetInt32(1),
                YBucket = reader.GetInt32(2),
                Count = reader.GetInt32(3),
            });
        }

        return results;
    });

    public TotalsSummary GetTotals() => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(SUM(total_keys), 0),
                   COALESCE(SUM(total_clicks), 0),
                   COALESCE(SUM(mouse_px), 0),
                   COALESCE(SUM(mouse_m), 0),
                   COALESCE(SUM(scroll_steps), 0),
                   COUNT(*),
                   MIN(date)
            FROM daily_stats;
            """;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return TotalsSummary.Empty;
        }

        return new TotalsSummary
        {
            TotalKeys = reader.GetInt64(0),
            TotalClicks = reader.GetInt64(1),
            MousePixels = (long)Math.Round(reader.GetDouble(2)),
            MouseMeters = reader.GetDouble(3),
            ScrollSteps = reader.GetInt32(4),
            ActiveDays = reader.GetInt32(5),
            FirstDate = reader.IsDBNull(6) ? null : ParseDate(reader.GetString(6)),
        };
    });

    public IReadOnlyList<DateOnly> GetActiveDates(DateOnly from, DateOnly to) => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT date FROM daily_stats
            WHERE date BETWEEN $from AND $to
              AND (total_keys > 0 OR total_clicks > 0 OR mouse_px > 0)
            ORDER BY date;
            """;
        command.Parameters.AddWithValue("$from", FormatDate(from));
        command.Parameters.AddWithValue("$to", FormatDate(to));

        var results = new List<DateOnly>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ParseDate(reader.GetString(0)));
        }

        return results;
    });

    public IReadOnlyList<AchievementRecord> GetAchievementRecords() => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, unlocked_at FROM achievements ORDER BY unlocked_at;";

        var results = new List<AchievementRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new AchievementRecord
            {
                Id = reader.GetString(0),
                UnlockedAt = DateTimeOffset.TryParse(
                    reader.GetString(1),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var at)
                    ? at
                    : DateTimeOffset.MinValue,
            });
        }

        return results;
    });

    public void UnlockAchievement(string id, DateTimeOffset unlockedAt)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        _database.Write(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO achievements (id, unlocked_at) VALUES ($id, $at);";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$at", unlockedAt.ToString("o", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        });
    }

    public IReadOnlyList<KeyDailyStat> GetAllKeyDaily() => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT date, key_code, key_name, count FROM key_daily ORDER BY date, key_code;";

        var results = new List<KeyDailyStat>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new KeyDailyStat
            {
                Date = ParseDate(reader.GetString(0)),
                KeyCode = reader.GetInt32(1),
                KeyName = reader.GetString(2),
                Count = reader.GetInt32(3),
            });
        }

        return results;
    });

    public IReadOnlyList<HourlyStat> GetAllHourly() => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT date, hour, key_count, mouse_m, mouse_px FROM hourly_dist ORDER BY date, hour;";

        var results = new List<HourlyStat>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadHourly(reader));
        }

        return results;
    });

    public IReadOnlyList<ComboStat> GetAllCombos() => _database.Read(connection =>
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT date, combo, count FROM combo_daily ORDER BY date, count DESC;";

        var results = new List<ComboStat>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new ComboStat
            {
                Date = ParseDate(reader.GetString(0)),
                Combo = reader.GetString(1),
                Count = reader.GetInt32(2),
            });
        }

        return results;
    });

    public void ClearAll() => _database.Write(connection =>
    {
        using var transaction = connection.BeginTransaction();
        foreach (var table in new[] { "daily_stats", "key_daily", "hourly_dist", "combo_daily", "click_zone", "active_minute", "achievements" })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"DELETE FROM {table};";
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    });

    private static DailyStat ReadDaily(SqliteDataReader reader) => new()
    {
        Date = ParseDate(reader.GetString(0)),
        TotalKeys = reader.GetInt32(1),
        MouseMeters = reader.GetDouble(2),
        TotalClicks = reader.GetInt32(3),
        ActiveMinutes = reader.GetInt32(4),
        MousePixels = (long)Math.Round(reader.GetDouble(5)),
        LeftClicks = reader.GetInt32(6),
        RightClicks = reader.GetInt32(7),
        MiddleClicks = reader.GetInt32(8),
        ScrollSteps = reader.GetInt32(9),
    };

    private static HourlyStat ReadHourly(SqliteDataReader reader) => new()
    {
        Date = ParseDate(reader.GetString(0)),
        Hour = reader.GetInt32(1),
        KeyCount = reader.GetInt32(2),
        MouseMeters = reader.GetDouble(3),
        MousePixels = (long)Math.Round(reader.GetDouble(4)),
    };

    private static SqliteParameter AddParameter(SqliteCommand command, string name, SqliteType type)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.SqliteType = type;
        command.Parameters.Add(parameter);
        return parameter;
    }

    internal static string FormatDate(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    internal static DateOnly ParseDate(string value)
    {
        if (DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return DateOnly.FromDateTime(DateTime.Parse(value, CultureInfo.InvariantCulture));
    }
}
