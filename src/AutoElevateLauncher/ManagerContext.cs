namespace AutoElevateLauncher;

public sealed class ManagerContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ConfigStore _configStore;
    private readonly ScheduledTaskService _taskService;
    private readonly StartupConfig _config;
    private readonly StartupOrchestrator _startupOrchestrator;
    private readonly IStartupItemLauncher _itemLauncher;
    private readonly ToolStripMenuItem _startAtLoginMenuItem;
    private MainForm? _mainForm;

    public ManagerContext()
    {
        _configStore = new ConfigStore();
        _taskService = new ScheduledTaskService(new ProcessRunner());
        _config = _configStore.Load();

        _itemLauncher = new ItemRunner(_configStore);
        _startupOrchestrator = new StartupOrchestrator(_itemLauncher);

        _startAtLoginMenuItem = new ToolStripMenuItem("启用管理员开机自启") { CheckOnClick = true };
        _startAtLoginMenuItem.Checked = _config.StartManagerAtLogin;
        _startAtLoginMenuItem.Click += async void (_, _) => await ToggleStartAtLoginAsync();

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "管理员自启动器",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => ShowManager();

        _ = RunEnabledItemsAtStartupAsync();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开管理器", null, (_, _) => ShowManager());
        menu.Items.Add(_startAtLoginMenuItem);
        menu.Items.Add("立即运行所有启用项", null, async (_, _) => await RunEnabledItemsNowAsync());
        menu.Items.Add("退出", null, (_, _) => ExitThread());
        return menu;
    }

    private async Task ToggleStartAtLoginAsync()
    {
        if (_startAtLoginMenuItem.Checked)
        {
            var result = WindowsPrivilege.IsCurrentProcessAdministrator()
                ? await _taskService.CreateOrUpdateManagerSelfStartTaskAsync(Application.ExecutablePath)
                : await _taskService.EnableManagerSelfStartElevatedAsync(Application.ExecutablePath);
            if (result.Succeeded)
            {
                _config.StartManagerAtLogin = true;
                _configStore.Save(_config);
            }
            else
            {
                _startAtLoginMenuItem.Checked = false;
                MessageBox.Show(result.StandardError + Environment.NewLine + result.StandardOutput, "启用管理员开机自启失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        else
        {
            var result = WindowsPrivilege.IsCurrentProcessAdministrator()
                ? await _taskService.DeleteManagerSelfStartTaskAsync()
                : await _taskService.DisableManagerSelfStartElevatedAsync(Application.ExecutablePath);
            if (result.Succeeded)
            {
                _config.StartManagerAtLogin = false;
                _configStore.Save(_config);
            }
            else
            {
                _startAtLoginMenuItem.Checked = true;
                MessageBox.Show(result.StandardError + Environment.NewLine + result.StandardOutput, "关闭管理员开机自启失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async Task RunEnabledItemsAtStartupAsync()
    {
        await _startupOrchestrator.RunEnabledItemsOnceAsync(_config);
    }

    private async Task RunEnabledItemsNowAsync()
    {
        await _startupOrchestrator.RunEnabledItemsAsync(_config);
    }

    private void ShowManager()
    {
        if (_mainForm is null || _mainForm.IsDisposed)
        {
            _mainForm = new MainForm(_config, _configStore, _taskService, _itemLauncher, _startupOrchestrator);
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