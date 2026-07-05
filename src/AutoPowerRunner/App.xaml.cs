using System.Reflection;
using System.Threading;
using System.Windows;
using AutoPowerRunner.Services;
using AutoPowerRunner.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace AutoPowerRunner;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private Forms.NotifyIcon? _trayIcon;
    private MainViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private ProcessRunner? _processRunner;
    private LogService? _logService;
    private bool _ownsMutex;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, "AutoPowerRunner.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        _ownsMutex = true;

        try
        {
            var uiContext = SynchronizationContext.Current;
            var paths = AppPaths.ForCurrentUser();
            _logService = new LogService(paths);
            var configService = new TaskConfigService(paths);
            _processRunner = new ProcessRunner(_logService, uiContext);
            var executablePath = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            var startupTaskService = new StartupTaskService(executablePath, _logService);

            _viewModel = new MainViewModel(
                configService,
                _processRunner,
                startupTaskService,
                _logService,
                uiContext);
            _mainWindow = new MainWindow(_viewModel);

            await _mainWindow.InitializeAsync();
            CreateTrayIcon();
            _viewModel.RunAllEnabled();
            _mainWindow.Show();
        }
        catch (Exception ex)
        {
            _logService?.Error("Application startup failed.", ex);
            System.Windows.MessageBox.Show(
                $"Auto Power Runner could not start.{Environment.NewLine}{ex.Message}",
                "Auto Power Runner",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _processRunner?.StopAll();
        if (_ownsMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        if (_viewModel is null || _mainWindow is null)
        {
            return;
        }

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open window", null, (_, _) => OpenMainWindow());
        menu.Items.Add("Run all enabled tasks", null, (_, _) => _viewModel.RunAllEnabled());
        menu.Items.Add("Stop all running tasks", null, (_, _) => _viewModel.StopAll());
        menu.Items.Add("Toggle administrator autostart", null, (_, _) => _viewModel.ToggleAutostartCommand.Execute(null));
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        _trayIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = Drawing.SystemIcons.Application,
            Text = "Auto Power Runner",
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => OpenMainWindow();
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
}
