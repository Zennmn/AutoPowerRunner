using System.Diagnostics;
using System.IO;
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

        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, "AutoPowerRunner.SingleInstance", out var createdNew);
            if (!createdNew)
            {
                Shutdown();
                return;
            }

            _ownsMutex = true;

            var uiContext = GetOrCreateUiContext();
            var paths = AppPaths.ForCurrentUser();
            _logService = new LogService(paths);
            var configService = new TaskConfigService(paths);
            _processRunner = new ProcessRunner(_logService, uiContext);
            var executablePath = GetExecutablePath();
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
            LogStartupFailure(ex);
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
        try
        {
            DisposeTrayIcon();
            StopRunningProcesses();
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
