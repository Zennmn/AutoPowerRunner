using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using AutoPowerRunner.Services;
using AutoPowerRunner.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace AutoPowerRunner;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "AutoPowerRunner.SingleInstance";
    private const string ActivationEventName = "AutoPowerRunner.ActivateExisting";
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _activationRegistration;
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ToolStripMenuItem? _autostartMenuItem;
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private ProcessRunner? _processRunner;
    private LogService? _logService;
    private bool _ownsMutex;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var createdNew);
            if (!createdNew)
            {
                SignalExistingInstance();
                Shutdown();
                return;
            }

            _ownsMutex = true;
            RegisterActivationEvent();

            var uiContext = GetOrCreateUiContext();
            var paths = AppPaths.ForCurrentUser();
            _logService = new LogService(paths);
            var silentStartup = !ShouldShowMainWindow(e.Args);
            var authorizedConfigHash = GetAuthorizedConfigHash(e.Args);
            if (silentStartup && authorizedConfigHash is null)
            {
                throw new SecurityException("管理员静默启动缺少配置授权哈希。请手动打开应用并重新授权管理员自启。");
            }

            var configService = new TaskConfigService(paths, silentStartup ? authorizedConfigHash : null);
            _processRunner = new ProcessRunner(_logService, uiContext);
            var executablePath = GetExecutablePath();
            var startupTaskService = new StartupTaskService(executablePath, _logService, configFile: paths.ConfigFile);

            _viewModel = new MainViewModel(
                configService,
                _processRunner,
                startupTaskService,
                _logService,
                uiContext,
                IsRunningAsAdministrator());
            _mainWindow = new MainWindow(_viewModel);

            var showMainWindow = !silentStartup;

            await _mainWindow.InitializeAsync();
            CreateTrayIcon();
            if (ShouldRunEnabledTasks(e.Args))
            {
                _viewModel.RunAllEnabled();
            }
            if (showMainWindow)
            {
                _mainWindow.Show();
            }
        }
        catch (Exception ex)
        {
            LogStartupFailure(ex);
            System.Windows.MessageBox.Show(
                $"自启管家无法启动。{Environment.NewLine}{ex.Message}",
                "自启管家",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            DisposeTrayIcon();
            StopRunningProcesses();
            DisposeActivationEvent();
            ReleaseMutex();
            DisposeMutex();
        }
        finally
        {
            base.OnExit(e);
        }
    }

    private void CreateTrayIcon()
    {
        if (_viewModel is null || _mainWindow is null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开窗口", null, (_, _) => OpenMainWindow());
        menu.Items.Add("运行所有启用任务", null, (_, _) => _viewModel.RunAllEnabledCommand.Execute(null));
        menu.Items.Add("停止所有运行任务", null, (_, _) => _viewModel.StopAllCommand.Execute(null));
        _autostartMenuItem = new Forms.ToolStripMenuItem(
            BuildTrayAutostartMenuText(_viewModel.IsAdministratorAutostartEnabled),
            image: null,
            (_, _) => _viewModel.ToggleAutostartCommand.Execute(null));
        menu.Items.Add(_autostartMenuItem);
        menu.Items.Add("退出", null, (_, _) => ExitApplication());
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        _trayIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = CreateTrayIconImage(),
            Text = "自启管家",
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => OpenMainWindow();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsAdministratorAutostartEnabled) or nameof(MainViewModel.ToggleAutostartText))
        {
            UpdateTrayAutostartMenuText();
        }
    }

    private void UpdateTrayAutostartMenuText()
    {
        if (_viewModel is null || _autostartMenuItem is null)
        {
            return;
        }

        _autostartMenuItem.Text = BuildTrayAutostartMenuText(_viewModel.IsAdministratorAutostartEnabled);
    }

    private static Drawing.Icon CreateTrayIconImage()
    {
        var bitmap = new Drawing.Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.Clear(Drawing.Color.Transparent);

            using var blueBrush = new Drawing.SolidBrush(Drawing.Color.FromArgb(0x0D, 0x73, 0xE8));
            using var whiteBrush = new Drawing.SolidBrush(Drawing.Color.White);
            using var bluePen = new Drawing.Pen(Drawing.Color.FromArgb(0x0D, 0x73, 0xE8), 2.3f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round
            };

            var outer = new Drawing.PointF[]
            {
                new(16, 2),
                new(28, 7),
                new(28, 16),
                new(26, 23),
                new(16, 30),
                new(6, 23),
                new(4, 16),
                new(4, 7)
            };
            var inner = new Drawing.PointF[]
            {
                new(16, 6),
                new(24, 10),
                new(24, 16),
                new(22, 21),
                new(16, 25),
                new(10, 21),
                new(8, 16),
                new(8, 10)
            };

            graphics.FillPolygon(blueBrush, outer);
            graphics.FillPolygon(whiteBrush, inner);
            graphics.DrawLines(bluePen, new Drawing.PointF[]
            {
                new(10, 16),
                new(14, 20),
                new(23, 11)
            });
        }

        var iconHandle = bitmap.GetHicon();
        try
        {
            using var icon = Drawing.Icon.FromHandle(iconHandle);
            return (Drawing.Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
            bitmap.Dispose();
        }
    }

    private void OpenMainWindow()
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
    }

    private void ExitApplication()
    {
        if (_mainWindow is not null)
        {
            _mainWindow.AllowClose();
            _mainWindow.Close();
        }

        Shutdown();
    }

    private SynchronizationContext GetOrCreateUiContext()
    {
        var uiContext = SynchronizationContext.Current;
        if (uiContext is not null)
        {
            return uiContext;
        }

        uiContext = new DispatcherSynchronizationContext(Dispatcher);
        SynchronizationContext.SetSynchronizationContext(uiContext);
        return uiContext;
    }

    private static string GetExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }

        var mainModulePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrWhiteSpace(mainModulePath))
        {
            return mainModulePath;
        }

        return Path.Combine(AppContext.BaseDirectory, "AutoPowerRunner.exe");
    }

    public static bool ShouldShowMainWindow(IEnumerable<string> startupArguments)
    {
        return !startupArguments.Any(argument =>
            string.Equals(argument, StartupTaskService.SilentStartupArgument, StringComparison.OrdinalIgnoreCase));
    }

    public static string BuildTrayAutostartMenuText(bool isAdministratorAutostartEnabled)
    {
        return isAdministratorAutostartEnabled ? "关闭管理员自启" : "开启管理员自启";
    }

    public static string? GetAuthorizedConfigHash(IReadOnlyList<string> startupArguments)
    {
        for (var index = 0; index < startupArguments.Count - 1; index++)
        {
            if (string.Equals(startupArguments[index], StartupTaskService.AuthorizedConfigHashArgument, StringComparison.OrdinalIgnoreCase))
            {
                return startupArguments[index + 1];
            }
        }

        return null;
    }

    public static bool ShouldRunEnabledTasks(IReadOnlyList<string> startupArguments) =>
        !ShouldShowMainWindow(startupArguments)
        && GetAuthorizedConfigHash(startupArguments) is not null;

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void DisposeTrayIcon()
    {
        try
        {
            _trayIcon?.Dispose();
        }
        catch (Exception ex)
        {
            LogCleanupFailure("Could not dispose tray icon.", ex);
        }
        finally
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            _autostartMenuItem = null;
            _trayIcon = null;
        }
    }

    private void StopRunningProcesses()
    {
        try
        {
            _processRunner?.StopAll();
        }
        catch (Exception ex)
        {
            LogCleanupFailure("Could not stop running processes.", ex);
        }
    }

    private void ReleaseMutex()
    {
        try
        {
            if (_ownsMutex)
            {
                _singleInstanceMutex?.ReleaseMutex();
            }
        }
        catch (Exception ex)
        {
            LogCleanupFailure("Could not release single-instance mutex.", ex);
        }
        finally
        {
            _ownsMutex = false;
        }
    }

    private void DisposeMutex()
    {
        try
        {
            _singleInstanceMutex?.Dispose();
        }
        catch (Exception ex)
        {
            LogCleanupFailure("Could not dispose single-instance mutex.", ex);
        }
        finally
        {
            _singleInstanceMutex = null;
        }
    }

    private void RegisterActivationEvent()
    {
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, _) => Dispatcher.BeginInvoke(OpenMainWindow),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
            activationEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }

    private void DisposeActivationEvent()
    {
        _activationRegistration?.Unregister(null);
        _activationRegistration = null;
        _activationEvent?.Dispose();
        _activationEvent = null;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCleanupFailure("Unhandled UI exception.", e.Exception);
        System.Windows.MessageBox.Show($"发生未处理错误。{Environment.NewLine}{e.Exception.Message}", "自启管家", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void LogStartupFailure(Exception exception)
    {
        if (_logService is null)
        {
            try
            {
                _logService = new LogService(AppPaths.ForCurrentUser());
            }
            catch
            {
            }
        }

        LogCleanupFailure("Application startup failed.", exception);
    }

    private void LogCleanupFailure(string message, Exception exception)
    {
        try
        {
            _logService?.Error(message, exception);
        }
        catch
        {
        }
    }
}
