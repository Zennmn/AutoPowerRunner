namespace AutoElevateLauncher;

public sealed class ManagerContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private MainForm? _mainForm;

    public ManagerContext()
    {
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
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        return menu;
    }

    private void ShowManager()
    {
        if (_mainForm is null || _mainForm.IsDisposed)
        {
            _mainForm = new MainForm(new ConfigStore(), new ScheduledTaskService(new ProcessRunner()));
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