using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KeyTrace.App.Infrastructure;
using KeyTrace.App.Services;
using KeyTrace.Core;
using KeyTrace.Core.Calibration;
using KeyTrace.Core.Collection;
using KeyTrace.Core.Diagnostics;
using KeyTrace.Core.Lifetime;
using KeyTrace.Core.Settings;
using KeyTrace.Core.Storage;

namespace KeyTrace.App.ViewModels;

/// <summary>设置页：采集开关、隐私静默、DPI 校准、数据导出与清空。</summary>
public sealed class SettingsViewModel : PageViewModel
{
    private readonly SettingsService _settings;
    private readonly CollectionService _collection;
    private readonly IStatsRepository _repository;
    private readonly StartupRegistrar _startup;
    private readonly IDialogService _dialogs;
    private readonly IAppLogger _logger;
    private readonly DispatcherTimer _calibrationTimer;

    private CalibrationSession? _session;
    private bool _syncing;

    private bool _isPaused;
    private bool _startWithWindows;
    private bool _minimizeToTray;
    private bool _privacyFilterEnabled;
    private bool _captureClickZones;
    private bool _ignoreInjectedInput;
    private bool _trackShortcutCombos;
    private int _dailyKeyGoal;
    private int _flushIntervalSeconds;

    private bool _isCalibrating;
    private string _calibrationPixelsText = "0 px";
    private string _calibrationHint = "把鼠标沿直线划过 10 厘米后点击「完成校准」";
    private bool _canCompleteCalibration;
    private string _metersPerPixelText = string.Empty;
    private string _dpiText = string.Empty;

    private string _newProcessText = string.Empty;
    private string _newTitleText = string.Empty;
    private string _statusMessage = string.Empty;
    private Brush _statusBrush = UiBrushes.TextSecondary;
    private bool _hasStatus;
    private string _databaseInfoText = string.Empty;

    public SettingsViewModel(
        SettingsService settings,
        CollectionService collection,
        IStatsRepository repository,
        StartupRegistrar startup,
        IDialogService dialogs,
        IAppLogger? logger = null)
        : base("设置", "所有数据只存在本机，随时可以导出或清空", "⚙")
    {
        _settings = settings;
        _collection = collection;
        _repository = repository;
        _startup = startup;
        _dialogs = dialogs;
        _logger = logger ?? NullLogger.Instance;

        TogglePauseCommand = new RelayCommand(() => IsPaused = !IsPaused);
        StartCalibrationCommand = new RelayCommand(StartCalibration, () => !IsCalibrating);
        CompleteCalibrationCommand = new RelayCommand(CompleteCalibration, () => IsCalibrating && _session?.IsReady == true);
        CancelCalibrationCommand = new RelayCommand(CancelCalibration, () => IsCalibrating);
        ResetCalibrationCommand = new RelayCommand(ResetCalibration);
        AddProcessCommand = new RelayCommand(AddProcess);
        RemoveProcessCommand = new RelayCommand(RemoveProcess);
        AddTitleCommand = new RelayCommand(AddTitle);
        RemoveTitleCommand = new RelayCommand(RemoveTitle);
        ExportDataCommand = new RelayCommand(ExportData);
        ClearDataCommand = new RelayCommand(ClearData);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
        OpenRepositoryCommand = new RelayCommand(OpenRepository);

        _calibrationTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(200),
        };
        _calibrationTimer.Tick += OnCalibrationTick;

        SyncFromSettings();
        ReloadLists();
    }

    public ICommand TogglePauseCommand { get; }

    public ICommand StartCalibrationCommand { get; }

    public ICommand CompleteCalibrationCommand { get; }

    public ICommand CancelCalibrationCommand { get; }

    public ICommand ResetCalibrationCommand { get; }

    public ICommand AddProcessCommand { get; }

    public ICommand RemoveProcessCommand { get; }

    public ICommand AddTitleCommand { get; }

    public ICommand RemoveTitleCommand { get; }

    public ICommand ExportDataCommand { get; }

    public ICommand ClearDataCommand { get; }

    public ICommand OpenDataFolderCommand { get; }

    public ICommand OpenRepositoryCommand { get; }

    public ObservableCollection<string> BlockedProcesses { get; } = new();

    public ObservableCollection<string> BlockedTitles { get; } = new();

    public string VersionText => $"版本 {AppInfo.Version}";

    public string DataPathText => AppPaths.RootDirectory;

    public string RepositoryUrl => AppInfo.RepositoryUrl;

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (!SetProperty(ref _isPaused, value) || _syncing)
            {
                return;
            }

            Apply(settings => settings.CollectionPaused = value);
            SetStatus(value ? "已暂停采集，所有输入都不会再被计数。" : "已恢复采集。", UiBrushes.TextSecondary);
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (!SetProperty(ref _startWithWindows, value) || _syncing)
            {
                return;
            }

            if (_startup.TrySetEnabled(value, out var error))
            {
                Apply(settings => settings.StartWithWindows = value);
                SetStatus(value ? "已加入开机自启。" : "已取消开机自启。", UiBrushes.TextSecondary);
            }
            else
            {
                _startWithWindows = !value;
                OnPropertyChanged(nameof(StartWithWindows));
                SetStatus(error ?? "无法修改开机自启。", UiBrushes.Danger);
            }
        }
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set
        {
            if (SetProperty(ref _minimizeToTray, value) && !_syncing)
            {
                Apply(settings => settings.MinimizeToTray = value);
            }
        }
    }

    public bool PrivacyFilterEnabled
    {
        get => _privacyFilterEnabled;
        set
        {
            if (SetProperty(ref _privacyFilterEnabled, value) && !_syncing)
            {
                Apply(settings => settings.PrivacyFilterEnabled = value);
            }
        }
    }

    public bool CaptureClickZones
    {
        get => _captureClickZones;
        set
        {
            if (SetProperty(ref _captureClickZones, value) && !_syncing)
            {
                Apply(settings => settings.CaptureClickZones = value);
            }
        }
    }

    public bool IgnoreInjectedInput
    {
        get => _ignoreInjectedInput;
        set
        {
            if (SetProperty(ref _ignoreInjectedInput, value) && !_syncing)
            {
                Apply(settings => settings.IgnoreInjectedInput = value);
            }
        }
    }

    public bool TrackShortcutCombos
    {
        get => _trackShortcutCombos;
        set
        {
            if (SetProperty(ref _trackShortcutCombos, value) && !_syncing)
            {
                Apply(settings => settings.TrackShortcutCombos = value);
            }
        }
    }

    public int DailyKeyGoal
    {
        get => _dailyKeyGoal;
        set
        {
            var clamped = Math.Clamp(value, 0, 1_000_000);
            if (SetProperty(ref _dailyKeyGoal, clamped) && !_syncing)
            {
                Apply(settings => settings.DailyKeyGoal = clamped);
            }
        }
    }

    public int FlushIntervalSeconds
    {
        get => _flushIntervalSeconds;
        set
        {
            var clamped = Math.Clamp(value, 5, 600);
            if (SetProperty(ref _flushIntervalSeconds, clamped) && !_syncing)
            {
                Apply(settings => settings.FlushIntervalSeconds = clamped);
            }
        }
    }

    public bool IsCalibrating
    {
        get => _isCalibrating;
        private set
        {
            if (SetProperty(ref _isCalibrating, value))
            {
                RaiseCalibrationCommands();
            }
        }
    }

    public string CalibrationPixelsText
    {
        get => _calibrationPixelsText;
        private set => SetProperty(ref _calibrationPixelsText, value);
    }

    public string CalibrationHint
    {
        get => _calibrationHint;
        private set => SetProperty(ref _calibrationHint, value);
    }

    public bool CanCompleteCalibration
    {
        get => _canCompleteCalibration;
        private set => SetProperty(ref _canCompleteCalibration, value);
    }

    public string MetersPerPixelText
    {
        get => _metersPerPixelText;
        private set => SetProperty(ref _metersPerPixelText, value);
    }

    public string DpiText
    {
        get => _dpiText;
        private set => SetProperty(ref _dpiText, value);
    }

    public string NewProcessText
    {
        get => _newProcessText;
        set => SetProperty(ref _newProcessText, value);
    }

    public string NewTitleText
    {
        get => _newTitleText;
        set => SetProperty(ref _newTitleText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public bool HasStatus
    {
        get => _hasStatus;
        private set => SetProperty(ref _hasStatus, value);
    }

    public string DatabaseInfoText
    {
        get => _databaseInfoText;
        private set => SetProperty(ref _databaseInfoText, value);
    }

    public override Task LoadAsync()
    {
        SyncFromSettings();
        ReloadLists();
        UpdateDatabaseInfo();
        return Task.CompletedTask;
    }

    private void Apply(Action<AppSettings> mutate)
    {
        if (_syncing)
        {
            return;
        }

        try
        {
            _settings.Update(mutate);
            UpdateCalibrationTexts();
        }
        catch (Exception ex)
        {
            _logger.Error("保存设置失败。", ex);
            SetStatus("保存设置失败：" + ex.Message, UiBrushes.Danger);
        }
    }

    private void SyncFromSettings()
    {
        var settings = _settings.Current;

        _syncing = true;
        try
        {
            _isPaused = settings.CollectionPaused;
            _startWithWindows = _startup.IsEnabled;
            _minimizeToTray = settings.MinimizeToTray;
            _privacyFilterEnabled = settings.PrivacyFilterEnabled;
            _captureClickZones = settings.CaptureClickZones;
            _ignoreInjectedInput = settings.IgnoreInjectedInput;
            _trackShortcutCombos = settings.TrackShortcutCombos;
            _dailyKeyGoal = settings.DailyKeyGoal;
            _flushIntervalSeconds = settings.FlushIntervalSeconds;
        }
        finally
        {
            _syncing = false;
        }

        OnPropertyChanged(
            nameof(IsPaused),
            nameof(StartWithWindows),
            nameof(MinimizeToTray),
            nameof(PrivacyFilterEnabled),
            nameof(CaptureClickZones),
            nameof(IgnoreInjectedInput),
            nameof(TrackShortcutCombos),
            nameof(DailyKeyGoal),
            nameof(FlushIntervalSeconds));

        UpdateCalibrationTexts();
        UpdateDatabaseInfo();
    }

    private void UpdateCalibrationTexts()
    {
        var metersPerPixel = _settings.Current.MetersPerPixel;
        MetersPerPixelText = $"{metersPerPixel * 1000:0.###} 毫米/像素";
        var dpi = metersPerPixel > 0 ? 0.0254 / metersPerPixel : 0;
        DpiText = $"约 {dpi:0} DPI（等效）";
    }

    private void UpdateDatabaseInfo()
    {
        try
        {
            var info = new FileInfo(AppPaths.DatabaseFile);
            var sizeKb = info.Exists ? Math.Max(1, info.Length / 1024) : 0;
            var totals = _repository.GetTotals();
            DatabaseInfoText = $"数据库 {sizeKb} KB · {totals.ActiveDays} 天记录 · 累计 {Format.Number(totals.TotalKeys)} 次按键";
        }
        catch (Exception ex)
        {
            _logger.Warn("读取数据库信息失败。", ex);
            DatabaseInfoText = "无法读取数据库信息";
        }
    }

    private void StartCalibration()
    {
        _session = new CalibrationSession();
        _collection.Calibration = _session;
        IsCalibrating = true;
        CanCompleteCalibration = false;
        CalibrationPixelsText = "0 px";
        CalibrationHint = "现在把鼠标沿直线划过 10 厘米（任意方向），然后点「完成校准」";
        _calibrationTimer.Start();
        SetStatus("校准中：请划出 10 厘米。", UiBrushes.Accent);
    }

    private void OnCalibrationTick(object? sender, EventArgs e)
    {
        var session = _session;
        if (session is null)
        {
            _calibrationTimer.Stop();
            return;
        }

        CalibrationPixelsText = $"{session.AccumulatedPixels:0} px · {session.SampleCount} 个采样点";
        CanCompleteCalibration = session.IsReady;
        (CompleteCalibrationCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void CompleteCalibration()
    {
        var session = _session;
        if (session is null || !session.IsReady)
        {
            SetStatus("位移太短，请再划满 10 厘米。", UiBrushes.Warning);
            return;
        }

        var metersPerPixel = session.ComputeMetersPerPixel(10);
        _settings.SetMetersPerPixel(metersPerPixel);
        StopCalibration();
        UpdateCalibrationTexts();

        var dpi = 0.0254 / metersPerPixel;
        SetStatus($"校准完成：{metersPerPixel * 1000:0.###} 毫米/像素（约 {dpi:0} DPI），历史数据也会按新比例重新换算。", UiBrushes.Success);
    }

    private void CancelCalibration()
    {
        StopCalibration();
        SetStatus("已取消校准。", UiBrushes.TextSecondary);
    }

    private void StopCalibration()
    {
        _calibrationTimer.Stop();
        _collection.Calibration = null;
        _session = null;
        IsCalibrating = false;
        CanCompleteCalibration = false;
        CalibrationHint = "把鼠标沿直线划过 10 厘米后点击「完成校准」";
        RaiseCalibrationCommands();
    }

    private void ResetCalibration()
    {
        _settings.SetMetersPerPixel(CalibrationProfile.DefaultMetersPerPixel);
        UpdateCalibrationTexts();
        SetStatus("已恢复为 96 DPI 的默认换算。", UiBrushes.TextSecondary);
    }

    private void RaiseCalibrationCommands()
    {
        (StartCalibrationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CompleteCalibrationCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CancelCalibrationCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void AddProcess()
    {
        var value = NewProcessText.Trim();
        if (value.Length == 0)
        {
            return;
        }

        _settings.Update(settings =>
        {
            if (!settings.PrivacyProcessBlocklist.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
            {
                settings.PrivacyProcessBlocklist.Add(value);
            }
        });

        NewProcessText = string.Empty;
        ReloadLists();
    }

    private void RemoveProcess(object? parameter)
    {
        if (parameter is not string value)
        {
            return;
        }

        _settings.Update(settings => settings.PrivacyProcessBlocklist.RemoveAll(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)));
        ReloadLists();
    }

    private void AddTitle()
    {
        var value = NewTitleText.Trim();
        if (value.Length == 0)
        {
            return;
        }

        _settings.Update(settings =>
        {
            if (!settings.PrivacyTitleKeywords.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
            {
                settings.PrivacyTitleKeywords.Add(value);
            }
        });

        NewTitleText = string.Empty;
        ReloadLists();
    }

    private void RemoveTitle(object? parameter)
    {
        if (parameter is not string value)
        {
            return;
        }

        _settings.Update(settings => settings.PrivacyTitleKeywords.RemoveAll(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)));
        ReloadLists();
    }

    private void ReloadLists()
    {
        BlockedProcesses.Clear();
        foreach (var item in _settings.Current.PrivacyProcessBlocklist.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase))
        {
            BlockedProcesses.Add(item);
        }

        BlockedTitles.Clear();
        foreach (var item in _settings.Current.PrivacyTitleKeywords.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase))
        {
            BlockedTitles.Add(item);
        }
    }

    private void ExportData()
    {
        var folder = _dialogs.PickFolder("选择导出 CSV 的目录");
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        try
        {
            _collection.Flush();
            var files = CsvExporter.ExportAll(_repository, folder, DateTimeOffset.Now);
            SetStatus($"已导出 {files.Count} 个 CSV 文件到：{folder}", UiBrushes.Success);
        }
        catch (Exception ex)
        {
            _logger.Error("导出 CSV 失败。", ex);
            SetStatus("导出失败：" + ex.Message, UiBrushes.Danger);
        }
    }

    private void ClearData()
    {
        if (!_dialogs.Confirm("清空数据", "将删除本机所有按键、鼠标统计与成就记录，且不可恢复。\n\n确定要继续吗？"))
        {
            return;
        }

        try
        {
            _collection.Flush();
            _repository.ClearAll();
            _collection.Aggregator.Reset();
            UpdateDatabaseInfo();
            SetStatus("已清空所有本地统计数据。", UiBrushes.Success);
        }
        catch (Exception ex)
        {
            _logger.Error("清空数据失败。", ex);
            SetStatus("清空失败：" + ex.Message, UiBrushes.Danger);
        }
    }

    private void OpenDataFolder()
    {
        try
        {
            AppPaths.EnsureCreated();
            Process.Start(new ProcessStartInfo
            {
                FileName = AppPaths.RootDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            SetStatus("无法打开目录：" + ex.Message, UiBrushes.Danger);
        }
    }

    private void OpenRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppInfo.RepositoryUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            SetStatus("无法打开链接：" + ex.Message, UiBrushes.Danger);
        }
    }

    private void SetStatus(string message, Brush brush)
    {
        StatusMessage = message;
        StatusBrush = brush;
        HasStatus = !string.IsNullOrWhiteSpace(message);
    }
}
