using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KeyTrace.App.Infrastructure;
using KeyTrace.App.Services;
using KeyTrace.Core;
using KeyTrace.Core.Collection;
using KeyTrace.Core.Diagnostics;
using KeyTrace.Core.Settings;
using KeyTrace.Core.Stats;

namespace KeyTrace.App.ViewModels;

/// <summary>主窗口 Shell：导航、全局状态、托盘联动。</summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settings;
    private readonly LiveStatsProvider _live;
    private readonly CollectionService _collection;
    private readonly IAppLogger _logger;
    private readonly DispatcherTimer _timer;
    private readonly SettingsViewModel _settingsPage;

    private PageViewModel _currentPage;
    private string _statusText = "正在启动";
    private Brush _statusBrush = UiBrushes.TextSecondary;
    private string _statusDetail = string.Empty;
    private string _todayKeysText = "0";
    private bool _isPaused;
    private bool _isPrivacySuppressed;
    private bool _isBusy;
    private string _loadError = string.Empty;
    private TrayService? _tray;

    public MainViewModel(
        SettingsService settings,
        LiveStatsProvider live,
        CollectionService collection,
        IAppLogger logger,
        DashboardViewModel dashboard,
        KeyboardViewModel keyboard,
        MouseViewModel mouse,
        TimeViewModel time,
        SettingsViewModel settingsPage)
    {
        _settings = settings;
        _live = live;
        _collection = collection;
        _logger = logger;
        _settingsPage = settingsPage;

        Pages = new ObservableCollection<PageViewModel> { dashboard, keyboard, mouse, time, settingsPage };
        _currentPage = dashboard;

        mouse.SettingsRequested += (_, _) => CurrentPage = _settingsPage;

        TogglePauseCommand = new RelayCommand(TogglePause);
        ShowWindowCommand = new RelayCommand(() => ShowRequested?.Invoke(this, EventArgs.Empty));
        ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));
        NavigateSettingsCommand = new RelayCommand(() => CurrentPage = _settingsPage);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>请求显示主窗口（托盘菜单 / 第二实例唤起）。</summary>
    public event EventHandler? ShowRequested;

    /// <summary>请求退出应用（托盘菜单）。</summary>
    public event EventHandler? ExitRequested;

    public ObservableCollection<PageViewModel> Pages { get; }

    public ICommand TogglePauseCommand { get; }

    public ICommand ShowWindowCommand { get; }

    public ICommand ExitCommand { get; }

    public ICommand NavigateSettingsCommand { get; }

    public TrayService? Tray
    {
        get => _tray;
        set
        {
            _tray = value;
            _tray?.SetPaused(IsPaused);
        }
    }

    public PageViewModel CurrentPage
    {
        get => _currentPage;
        set
        {
            if (value is null || ReferenceEquals(value, _currentPage))
            {
                return;
            }

            SetProperty(ref _currentPage, value);
            LoadPage(value);
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public string StatusDetail
    {
        get => _statusDetail;
        private set => SetProperty(ref _statusDetail, value);
    }

    public string TodayKeysText
    {
        get => _todayKeysText;
        private set => SetProperty(ref _todayKeysText, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (SetProperty(ref _isPaused, value))
            {
                (TogglePauseCommand as RelayCommand)?.RaiseCanExecuteChanged();
                Tray?.SetPaused(value);
            }
        }
    }

    public bool IsPrivacySuppressed
    {
        get => _isPrivacySuppressed;
        private set => SetProperty(ref _isPrivacySuppressed, value);
    }

    public string LoadError
    {
        get => _loadError;
        private set
        {
            if (SetProperty(ref _loadError, value))
            {
                OnPropertyChanged(nameof(HasLoadError));
            }
        }
    }

    public bool HasLoadError => !string.IsNullOrEmpty(_loadError);

    public string FooterText => $"键迹 KeyTrace · v{AppInfo.Version} · 数据仅保存在本机";

    public void Initialize()
    {
        LoadPage(_currentPage);
        UpdateStatus(_live.GetToday());
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        try
        {
            var live = _live.GetToday();
            UpdateStatus(live);
            _currentPage.OnLiveTick(live);

            _tray?.SetTooltip(
                $"键迹 KeyTrace\n今日按键 {Format.Number(live.TotalKeys)} 次\n" +
                $"鼠标 {Format.Distance(live.Meters)} · 点击 {Format.Number(live.TotalClicks)} 次\n" +
                $"活跃 {Format.Duration(live.ActiveMinutes)}");
        }
        catch (Exception ex)
        {
            _logger.Error("刷新实时状态失败。", ex);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private void UpdateStatus(TodayLive live)
    {
        IsPaused = _settings.Current.CollectionPaused;
        IsPrivacySuppressed = _collection.IsPrivacySuppressed;

        if (!_collection.IsRunning)
        {
            StatusText = "采集未运行";
            StatusBrush = UiBrushes.Danger;
        }
        else if (IsPaused)
        {
            StatusText = "已暂停";
            StatusBrush = UiBrushes.TextSecondary;
        }
        else if (IsPrivacySuppressed)
        {
            StatusText = "隐私静默中";
            StatusBrush = UiBrushes.Warning;
        }
        else
        {
            StatusText = "正在记录";
            StatusBrush = UiBrushes.Success;
        }

        TodayKeysText = Format.Number(live.TotalKeys);
        StatusDetail = $"今日 {TodayKeysText} 次 · {Format.Distance(live.Meters)}";
    }

    private void TogglePause()
    {
        var paused = !_settings.Current.CollectionPaused;
        _settings.SetPaused(paused);
        UpdateStatus(_live.GetToday());
    }

    private async void LoadPage(PageViewModel page)
    {
        try
        {
            LoadError = string.Empty;
            await page.LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.Error($"加载页面 {page.Title} 失败。", ex);
            LoadError = $"页面加载失败：{ex.Message}";
        }
    }
}
