using System.Text.Encodings.Web;
using System.Text.Json;
using KeyTrace.Core.Diagnostics;

namespace KeyTrace.Core.Settings;

/// <summary>设置的持久化抽象。</summary>
public interface ISettingsStore
{
    string FilePath { get; }

    AppSettings Load();

    void Save(AppSettings settings);
}

/// <summary>基于 JSON 的设置存储：原子写入，损坏文件自动备份后回退默认值。</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IAppLogger _logger;

    public JsonSettingsStore(string? filePath = null, IAppLogger? logger = null)
    {
        FilePath = filePath ?? AppPaths.SettingsFile;
        _logger = logger ?? NullLogger.Instance;
    }

    public string FilePath { get; }

    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
            if (settings is null)
            {
                return new AppSettings();
            }

            settings.Normalize();
            return settings;
        }
        catch (Exception ex)
        {
            _logger.Error("读取设置失败，已回退默认设置。", ex);
            TryBackupCorruptFile();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.Error("保存设置失败。", ex);
            throw;
        }
    }

    private void TryBackupCorruptFile()
    {
        try
        {
            var backup = $"{FilePath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(FilePath, backup, overwrite: true);
        }
        catch
        {
            // 备份失败不影响主流程。
        }
    }
}
