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
            var configService = new TaskConfigService(paths);
            _processRunner = new ProcessRunner(_logService, uiContext);
            var executablePath = GetExecutablePath();
            var startupTaskService = new StartupTaskService(executablePath, _logService);

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

        var menu = new Forms.ContextMenuStrip
        {
            AutoSize = true,
            BackColor = Drawing.Color.FromArgb(0xFA, 0xFA, 0xFA),
            DropShadowEnabled = true,
            ForeColor = Drawing.Color.FromArgb(0x0F, 0x0F, 0x0F),
            Font = new Drawing.Font("Microsoft YaHei UI", 9f, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point),
            MinimumSize = new Drawing.Size(282, 0),
            Padding = new Forms.Padding(6),
            Renderer = new TrayMenuRenderer(),
            ShowCheckMargin = false,
            ShowImageMargin = true
        };

        menu.Items.Add(CreateTrayMenuItem("打开主界面", (_, _) => OpenMainWindow()));
        menu.Items.Add(CreateTrayMenuItem("运行所有启用任务", (_, _) => _viewModel.RunAllEnabledCommand.Execute(null)));
        menu.Items.Add(CreateTraySeparator());
        menu.Items.Add(CreateTrayMenuItem("停止所有运行任务", (_, _) => _viewModel.StopAllCommand.Execute(null)));
        _autostartMenuItem = CreateTrayMenuItem(
            BuildTrayAutostartMenuText(_viewModel.IsAdministratorAutostartEnabled),
            (_, _) => _viewModel.ToggleAutostartCommand.Execute(null));
        menu.Items.Add(_autostartMenuItem);
        menu.Items.Add(CreateTraySeparator());
        menu.Items.Add(CreateTrayMenuItem("退出", (_, _) => ExitApplication()));
        menu.Opening += (_, _) => UpdateTrayAutostartMenuText();
        menu.Opened += (_, _) => ApplyTrayMenuRegion(menu);
        menu.SizeChanged += (_, _) => ApplyTrayMenuRegion(menu);
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

        var enabled = _viewModel.IsAdministratorAutostartEnabled;
        _autostartMenuItem.Text = BuildTrayAutostartMenuText(enabled);
    }

    private static Forms.ToolStripMenuItem CreateTrayMenuItem(
        string text,
        EventHandler onClick)
    {
        var item = new Forms.ToolStripMenuItem(text)
        {
            AutoSize = false,
            ForeColor = Drawing.Color.FromArgb(0x0F, 0x0F, 0x0F),
            Margin = Forms.Padding.Empty,
            Padding = new Forms.Padding(28, 3, 18, 3),
            Size = new Drawing.Size(282, 27)
        };
        item.Click += onClick;
        return item;
    }

    private static Forms.ToolStripSeparator CreateTraySeparator()
    {
        return new Forms.ToolStripSeparator
        {
            Margin = new Forms.Padding(10, 0, 10, 0)
        };
    }

    private static void ApplyTrayMenuRegion(Forms.ContextMenuStrip menu)
    {
        if (menu.Width <= 0 || menu.Height <= 0)
        {
            return;
        }

        using var path = CreateRoundedPath(new Drawing.Rectangle(0, 0, menu.Width, menu.Height), 5);
        var previousRegion = menu.Region;
        menu.Region = new Drawing.Region(path);
        previousRegion?.Dispose();
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedPath(Drawing.Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;
        var arc = new Drawing.Rectangle(bounds.X, bounds.Y, diameter, diameter);

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private sealed class TrayMenuRenderer : Forms.ToolStripProfessionalRenderer
    {
        public TrayMenuRenderer() : base(new TrayMenuColorTable())
        {
            RoundedEdges = true;
        }

        protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(Drawing.Color.FromArgb(0xFA, 0xFA, 0xFA));
        }

        protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected)
            {
                return;
            }

            var bounds = new Drawing.Rectangle(7, 1, Math.Max(1, e.Item.Width - 14), Math.Max(1, e.Item.Height - 2));
            using var path = CreateRoundedPath(bounds, 4);
            using var brush = new Drawing.SolidBrush(Drawing.Color.FromArgb(0xEE, 0xEE, 0xEE));
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }

        protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is not Forms.ToolStripMenuItem)
            {
                base.OnRenderItemText(e);
                return;
            }

            var bounds = new Drawing.Rectangle(44, 0, Math.Max(1, e.Item.Width - 56), e.Item.Height);
            Forms.TextRenderer.DrawText(
                e.Graphics,
                e.Text,
                e.TextFont,
                bounds,
                e.TextColor,
                Forms.TextFormatFlags.Left
                | Forms.TextFormatFlags.VerticalCenter
                | Forms.TextFormatFlags.SingleLine
                | Forms.TextFormatFlags.NoPrefix
                | Forms.TextFormatFlags.NoPadding
                | Forms.TextFormatFlags.PreserveGraphicsClipping);
        }

        protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
        {
            var bounds = new Drawing.Rectangle(0, 0, Math.Max(1, e.ToolStrip.Width - 1), Math.Max(1, e.ToolStrip.Height - 1));
            using var path = CreateRoundedPath(bounds, 5);
            using var pen = new Drawing.Pen(Drawing.Color.FromArgb(0xD8, 0xD8, 0xD8));
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawPath(pen, path);
        }

        protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Drawing.Pen(Drawing.Color.FromArgb(0xDA, 0xDA, 0xDA));
            var y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }
    }

    private sealed class TrayMenuColorTable : Forms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Hover = Drawing.Color.FromArgb(0xEE, 0xEE, 0xEE);

        public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.FromArgb(0xFA, 0xFA, 0xFA);
        public override Drawing.Color ImageMarginGradientBegin => Drawing.Color.FromArgb(0xFA, 0xFA, 0xFA);
        public override Drawing.Color ImageMarginGradientMiddle => Drawing.Color.FromArgb(0xFA, 0xFA, 0xFA);
        public override Drawing.Color ImageMarginGradientEnd => Drawing.Color.FromArgb(0xFA, 0xFA, 0xFA);
        public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(0xD8, 0xD8, 0xD8);
        public override Drawing.Color MenuItemBorder => Hover;
        public override Drawing.Color MenuItemSelected => Hover;
        public override Drawing.Color MenuItemSelectedGradientBegin => Hover;
        public override Drawing.Color MenuItemSelectedGradientEnd => Hover;
        public override Drawing.Color MenuItemPressedGradientBegin => Hover;
        public override Drawing.Color MenuItemPressedGradientMiddle => Hover;
        public override Drawing.Color MenuItemPressedGradientEnd => Hover;
        public override Drawing.Color SeparatorDark => Drawing.Color.FromArgb(0xDA, 0xDA, 0xDA);
        public override Drawing.Color SeparatorLight => Drawing.Color.FromArgb(0xDA, 0xDA, 0xDA);
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

    public static bool ShouldRunEnabledTasks(IReadOnlyList<string> startupArguments) =>
        !ShouldShowMainWindow(startupArguments);

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void DisposeTrayIcon()
    {
        var trayMenu = _trayIcon?.ContextMenuStrip;
        try
        {
            _trayIcon?.Dispose();
            trayMenu?.Dispose();
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
