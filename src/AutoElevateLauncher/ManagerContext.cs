namespace AutoElevateLauncher;

public sealed class ManagerContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ConfigStore _configStore;
    private readonly ScheduledTaskService _taskService;
    private readonly StartupConfig _config;
    private readonly ToolStripMenuItem _startAtLoginMenuItem;
    private MainForm? _mainForm;

    public ManagerContext()
    {
        _configStore = new ConfigStore();
        _taskService = new ScheduledTaskService(new ProcessRunner());
        _config = _configStore.Load();

        _startAtLoginMenuItem = new ToolStripMenuItem("Start tray app at login") { CheckOnClick = true };
        _startAtLoginMenuItem.Checked = _config.StartManagerAtLogin;
        _startAtLoginMenuItem.Click += async void (_, _) => await ToggleStartAtLoginAsync();

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Auto Elevate Launcher",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => ShowManager();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open manager", null, (_, _) => ShowManager());
        menu.Items.Add(_startAtLoginMenuItem);
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private async Task ToggleStartAtLoginAsync()
    {
        if (_startAtLoginMenuItem.Checked)
        {
            var result = await _taskService.CreateOrUpdateManagerSelfStartTaskAsync(Application.ExecutablePath);
            if (result.Succeeded)
            {
                _config.StartManagerAtLogin = true;
                _configStore.Save(_config);
            }
            else
            {
                _startAtLoginMenuItem.Checked = false;
                MessageBox.Show(result.StandardError + Environment.NewLine + result.StandardOutput, "Failed to enable tray app self-start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            var result = await _taskService.DeleteManagerSelfStartTaskAsync();
            if (result.Succeeded)
            {
                _config.StartManagerAtLogin = false;
                _configStore.Save(_config);
            }
            else
            {
                _startAtLoginMenuItem.Checked = true;
                MessageBox.Show(result.StandardError + Environment.NewLine + result.StandardOutput, "Failed to disable tray app self-start", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void ShowManager()
    {
        if (_mainForm is null || _mainForm.IsDisposed)
        {
            _mainForm = new MainForm(_config, _configStore, _taskService);
        }

        _mainForm.Show();
        _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.Activate();
    }

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.ExitThreadCore();
    }
}