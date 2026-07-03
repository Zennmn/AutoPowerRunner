using System.Diagnostics;

namespace AutoElevateLauncher;

public sealed class MainForm : Form
{
    private readonly ConfigStore _configStore;
    private readonly ScheduledTaskService _taskService;
    private StartupConfig _config;
    private readonly ListBox _items = new() { Dock = DockStyle.Fill, DisplayMember = nameof(StartupItem.Name) };
    private readonly TextBox _name = new() { Dock = DockStyle.Top };
    private readonly TextBox _path = new() { Dock = DockStyle.Top };
    private readonly TextBox _arguments = new() { Dock = DockStyle.Top };
    private readonly TextBox _workingDirectory = new() { Dock = DockStyle.Top };
    private readonly CheckBox _enabled = new() { Text = "Enabled", Dock = DockStyle.Top };
    private readonly Label _status = new() { Dock = DockStyle.Top, AutoSize = true };

    public MainForm(ConfigStore configStore, ScheduledTaskService taskService)
    {
        _configStore = configStore;
        _taskService = taskService;
        _config = _configStore.Load();

        Text = "Auto Elevate Launcher";
        Width = 1000;
        Height = 650;
        BuildLayout();
        RefreshList();
    }

    private void BuildLayout()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 360 };
        Controls.Add(split);

        var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72 };
        var addScript = new Button { Text = "Add script", Width = 100 };
        var addProgram = new Button { Text = "Add program", Width = 110 };
        var delete = new Button { Text = "Delete", Width = 90 };
        addScript.Click += (_, _) => AddItem(StartupItemType.PowerShellScript);
        addProgram.Click += (_, _) => AddItem(StartupItemType.Executable);
        delete.Click += async (_, _) => await DeleteSelectedAsync();
        leftButtons.Controls.AddRange([addScript, addProgram, delete]);

        split.Panel1.Controls.Add(_items);
        split.Panel1.Controls.Add(leftButtons);
        _items.SelectedIndexChanged += (_, _) => LoadSelectedIntoDetails();

        var details = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 14, Padding = new Padding(12) };
        split.Panel2.Controls.Add(details);
        AddLabeled(details, "Name", _name);
        AddLabeled(details, "Path", _path);
        AddLabeled(details, "Arguments", _arguments);
        AddLabeled(details, "Working directory", _workingDirectory);
        details.Controls.Add(_enabled);
        details.Controls.Add(_status);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 };
        var save = new Button { Text = "Save", Width = 90 };
        var run = new Button { Text = "Run now", Width = 90 };
        var stop = new Button { Text = "Stop", Width = 90 };
        var logs = new Button { Text = "Open logs", Width = 100 };
        save.Click += async (_, _) => await SaveSelectedAsync();
        run.Click += async (_, _) => await RunSelectedAsync();
        stop.Click += async (_, _) => await StopSelectedAsync();
        logs.Click += (_, _) => OpenSelectedLogs();
        actions.Controls.AddRange([save, run, stop, logs]);
        details.Controls.Add(actions);
    }

    private static void AddLabeled(Control parent, string label, Control control)
    {
        parent.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, AutoSize = true });
        parent.Controls.Add(control);
    }

    private void AddItem(StartupItemType type)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = type == StartupItemType.PowerShellScript ? "PowerShell scripts (*.ps1)|*.ps1" : "Programs (*.exe)|*.exe"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var item = new StartupItem
        {
            Name = Path.GetFileNameWithoutExtension(dialog.FileName),
            Type = type,
            Path = dialog.FileName,
            WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? string.Empty,
            Enabled = true
        };
        item.EnsureTaskName();
        _config.Items.Add(item);
        _configStore.Save(_config);
        RefreshList();
        _items.SelectedItem = item;
    }

    private StartupItem? SelectedItem => _items.SelectedItem as StartupItem;

    private void RefreshList()
    {
        _items.DataSource = null;
        _items.DataSource = _config.Items;
    }

    private void LoadSelectedIntoDetails()
    {
        var item = SelectedItem;
        if (item is null) return;
        _name.Text = item.Name;
        _path.Text = item.Path;
        _arguments.Text = item.Arguments;
        _workingDirectory.Text = item.WorkingDirectory;
        _enabled.Checked = item.Enabled;
        _status.Text = $"Status: {item.LastStatus}; Task: {item.TaskSyncStatus}; Exit: {item.LastExitCode?.ToString() ?? ""}";
    }

    private async Task SaveSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        item.Name = _name.Text.Trim();
        item.Path = _path.Text.Trim();
        item.Arguments = _arguments.Text;
        item.WorkingDirectory = _workingDirectory.Text.Trim();
        item.Enabled = _enabled.Checked;

        var validation = StartupItemValidator.Validate(item);
        if (!validation.IsValid)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, validation.Errors), "Validation failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = await _taskService.CreateOrUpdateStartupItemTaskAsync(item, Application.ExecutablePath);
        item.TaskSyncStatus = result.Succeeded ? TaskSyncStatus.Synchronized : TaskSyncStatus.Failed;
        item.LastTaskError = result.Succeeded ? string.Empty : result.StandardError + result.StandardOutput;
        _configStore.Save(_config);
        RefreshList();
        LoadSelectedIntoDetails();
    }

    private async Task DeleteSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        await _taskService.DeleteTaskAsync(item);
        _config.Items.Remove(item);
        _configStore.Save(_config);
        RefreshList();
    }

    private async Task RunSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        await _taskService.RunTaskAsync(item);
    }

    private async Task StopSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        await _taskService.StopTaskAsync(item);
    }

    private void OpenSelectedLogs()
    {
        var item = SelectedItem;
        if (item is null) return;
        var directory = AppPaths.GetItemLogDirectory(item.Id);
        Directory.CreateDirectory(directory);
        Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
    }
}