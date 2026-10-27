using System.IO;
using System.Windows;
using System.Windows.Threading;
using KeyTrace.App.Services;
using KeyTrace.App.ViewModels;
using KeyTrace.Core;
using KeyTrace.Core.Achievements;
using KeyTrace.Core.Aggregation;
using KeyTrace.Core.Collection;
using KeyTrace.Core.Diagnostics;
using KeyTrace.Core.Input;
using KeyTrace.Core.Lifetime;
using KeyTrace.Core.Settings;
using KeyTrace.Core.Stats;
using KeyTrace.Core.Storage;

namespace KeyTrace.App;

/// <summary>
/// 组合根：设置、数据库、采集、托盘、主窗口与主 ViewModel 都在这里组装。
/// </summary>
public partial class App : Application
{
    private const string MutexName = @"Local\KeyTrace.SingleInstance";
    private const string ShowEventName = @"Local\KeyTrace.ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private Thread? _signalThread;

    private FileLogger? _logger;
    private SettingsService? _settings;
    private KeyTraceDatabase? _database;
    private SqliteStatsRepository? _repository;
    private DataAggregator? _aggregator;
    private CollectionService? _collection;
    private LiveStatsProvider? _live;
    private TrayService? _tray;
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;

    private bool _isExiting;
    private bool _hooksFailed;
    private bool _errorDialogShown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 先挂异常处理：后面任何一步失败都不应该变成 Windows 的"应用程序错误"弹窗。
        RegisterExceptionHandlers();

        if (!EnsureSingleInstance())
        {
            Shutdown();
            return;
        }

        if (!AppPaths.TryInitialize(out var pathError))
        {
            var message =
                "KeyTrace 无法创建数据目录，程序无法启动。\n\n" +
                "已尝试以下位置：\n" + pathError + "\n\n" +
                "可以这样解决：\n" +
                "1. 把程序放到你有写权限的目录（例如桌面）后重试；\n" +
                "2. 设置环境变量 KEYTRACE_HOME 指向一个可写目录；\n" +
                "3. 右键 KeyTrace.exe → 以管理员身份运行。";

            TryWriteFatalLog(message);

            MessageBox.Show(
                message,
                "键迹 KeyTrace",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
            return;
        }

        _logger = new FileLogger();
        _logger.Info($"KeyTrace 启动，版本 {AppInfo.Version}，数据目录 {AppPaths.RootDirectory}");

        try
        {
            Compose();
        }
        catch (Exception ex)
        {
            _logger.Error("初始化失败。", ex);
            TryWriteFatalLog("初始化失败：" + ex);
            MessageBox.Show(
                "KeyTrace 初始化失败：\n\n" + ex.Message + "\n\n详细信息已写入 " + AppPaths.LogDirectory,
                "键迹 KeyTrace",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var startMinimized = e.Args.Any(static arg =>
            string.Equals(arg, "--minimized", StringComparison.OrdinalIgnoreCase));

        if (!startMinimized)
        {
            ShowMainWindow();
        }

        if (_hooksFailed)
        {
            _logger.Error("全局钩子启动失败，采集未运行。");
            if (!startMinimized)
            {
                MessageBox.Show(
                    "全局键鼠钩子注册失败，当前不会记录任何数据。\n\n" +
                    "常见原因：被安全软件拦截，或当前处于受限的远程会话中。\n" +
                    "可以尝试以管理员身份运行，或在安全软件中放行 KeyTrace。",
                    "键迹 KeyTrace",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _isExiting = true;

        try
        {
            _showEvent?.Dispose();
        }
        catch
        {
            // 退出阶段忽略清理异常。
        }

        _mutex?.Dispose();
        _logger?.Info("KeyTrace 退出。");
        base.OnExit(e);
    }

    private void Compose()
    {
        IAppLogger logger = (IAppLogger?)_logger ?? NullLogger.Instance;
        _settings = new SettingsService(new JsonSettingsStore(logger: logger), logger);

        _database = new KeyTraceDatabase(AppPaths.DatabaseFile);
        _repository = new SqliteStatsRepository(_database);
        _repository.Initialize();

        _aggregator = new DataAggregator(captureClickZones: _settings.Current.CaptureClickZones);

        // 输入源与采集服务互相依赖，用转发器打破构造环。
        var router = new InputSinkRouter();
        var source = new GlobalHookInputSource(router, logger);
        _collection = new CollectionService(source, _aggregator, _repository, _settings, logger: logger);
        router.Target = _collection;

        _live = new LiveStatsProvider(_repository, _aggregator, _settings);
        var achievements = new AchievementService(_repository);
        var startup = new StartupRegistrar(logger);
        var dialogs = new DialogService();

        var dashboard = new DashboardViewModel(_live, _settings, achievements);
        var keyboard = new KeyboardViewModel(_repository, _live);
        var mouse = new MouseViewModel(_repository, _live, _settings);
        var time = new TimeViewModel(_repository, _live);
        var settingsPage = new SettingsViewModel(_settings, _collection, _repository, startup, dialogs, logger);

        _viewModel = new MainViewModel(
            _settings,
            _live,
            _collection,
            logger,
            dashboard,
            keyboard,
            mouse,
            time,
            settingsPage);

        try
        {
            _collection.Start();
        }
        catch (Exception ex)
        {
            _hooksFailed = true;
            _logger?.Error("启动全局输入钩子失败。", ex);
        }

        try
        {
            _tray = new TrayService(
                ShowMainWindow,
                () => _viewModel?.TogglePauseCommand.Execute(null),
                () =>
                {
                    ShowMainWindow();
                    _viewModel?.NavigateSettingsCommand.Execute(null);
                },
                ExitApp);
        }
        catch (Exception ex)
        {
            // 托盘不可用时降级运行：关闭窗口即退出，避免程序"隐身"在后台无法找回。
            _logger?.Error("创建系统托盘图标失败，将以无托盘模式运行。", ex);
            _tray = null;
        }

        _viewModel.Tray = _tray;
        _viewModel.ShowRequested += (_, _) => ShowMainWindow();
        _viewModel.ExitRequested += (_, _) => ExitApp();

        _mainWindow = new MainWindow
        {
            DataContext = _viewModel,
        };
        _mainWindow.CloseRequested += OnCloseRequested;
        MainWindow = _mainWindow;

        _viewModel.Initialize();
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        // 没有托盘图标时不能"藏起来"，否则用户再也找不回窗口。
        if (_tray is not null && _settings?.Current.MinimizeToTray == true)
        {
            _mainWindow?.Hide();
            return;
        }

        ExitApp();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
        _ = _mainWindow.Focus();
    }

    private void ExitApp()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;

        try
        {
            _viewModel?.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.Warn("释放主 ViewModel 失败。", ex);
        }

        try
        {
            _collection?.Dispose();
        }
        catch (Exception ex)
        {
            _logger?.Error("停止采集服务失败。", ex);
        }

        try
        {
            _tray?.Dispose();
            _tray = null;
        }
        catch (Exception ex)
        {
            _logger?.Warn("释放托盘图标失败。", ex);
        }

        try
        {
            if (_mainWindow is not null)
            {
                _mainWindow.AllowClose = true;
                _mainWindow.Close();
                _mainWindow = null;
            }
        }
        catch (Exception ex)
        {
            _logger?.Warn("关闭主窗口失败。", ex);
        }

        try
        {
            _database?.Dispose();
            _database = null;
        }
        catch (Exception ex)
        {
            _logger?.Warn("关闭数据库失败。", ex);
        }

        Shutdown();
    }

    private bool EnsureSingleInstance()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);

        if (createdNew)
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            _signalThread = new Thread(ListenForShowSignal)
            {
                IsBackground = true,
                Name = "KeyTrace.SingleInstance",
            };
            _signalThread.Start();
            return true;
        }

        try
        {
            using var existing = EventWaitHandle.OpenExisting(ShowEventName);
            existing.Set();
        }
        catch
        {
            // 另一个实例尚未准备好消息循环时，忽略即可。
        }

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    private void ListenForShowSignal()
    {        var handle = _showEvent;
        if (handle is null)
        {
            return;
        }

        while (true)
        {
            try
            {
                if (!handle.WaitOne())
                {
                    return;
                }

                Dispatcher.BeginInvoke(new Action(ShowMainWindow));
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger?.Warn("处理单实例唤醒信号失败。", ex);
                return;
            }
        }
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            _logger?.Error("UI 线程未处理异常。", args.Exception);

            if (!_errorDialogShown)
            {
                _errorDialogShown = true;
                MessageBox.Show(
                    "键迹遇到了一个未处理的错误，已经记录到日志。\n\n" + args.Exception.Message,
                    "键迹 KeyTrace",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _logger?.Error("应用域未处理异常。", args.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _logger?.Error("未观察的任务异常。", args.Exception);
            args.SetObserved();
        };
    }

    /// <summary>数据目录建不出来时，把原因写到 %TEMP% 的独立日志里，方便用户反馈。</summary>
    private static void TryWriteFatalLog(string message)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "KeyTrace-fatal.log");
            File.AppendAllText(
                path,
                $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // 连临时目录都写不了时，只能依赖弹窗告知用户。
        }
    }
}
