using System.Diagnostics;

namespace AutoElevateLauncher;

public sealed class MainForm : Form
{
    private readonly ConfigStore _configStore;
    private readonly ScheduledTaskService _taskService;
    private readonly IStartupItemLauncher _itemLauncher;
    private readonly StartupOrchestrator _startupOrchestrator;
    private StartupConfig _config;
    private readonly DataGridView _items = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        RowHeadersVisible = false
    };
    private readonly TextBox _name = new() { Dock = DockStyle.Top };
    private readonly TextBox _path = new() { Dock = DockStyle.Top };
    private readonly TextBox _arguments = new() { Dock = DockStyle.Top };
    private readonly TextBox _workingDirectory = new() { Dock = DockStyle.Top };
    private readonly CheckBox _enabled = new() { Text = "启用此项目", Dock = DockStyle.Top };
    private readonly Label _status = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Label _permissionStatus = new() { Dock = DockStyle.Top, AutoSize = true };
    private readonly Label _selfStartStatus = new() { Dock = DockStyle.Top, AutoSize = true };

    public MainForm(StartupConfig config, ConfigStore configStore, ScheduledTaskService taskService, IStartupItemLauncher itemLauncher, StartupOrchestrator startupOrchestrator)
    {
        _configStore = configStore;
        _taskService = taskService;
        _itemLauncher = itemLauncher;
        _startupOrchestrator = startupOrchestrator;
        _config = config;

        Text = "管理员自启动器";
        Width = 1000;
        Height = 650;
        BuildLayout();
        RefreshStatusLabels();
        RefreshList();
    }

    private void BuildLayout()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 380 };
        Controls.Add(split);

        _items.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "名称", DataPropertyName = nameof(StartupItem.Name), Width = 150 });
        _items.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "类型", DataPropertyName = nameof(StartupItem.Type), Width = 90 });
        _items.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "启用", DataPropertyName = nameof(StartupItem.Enabled), Width = 60 });
        _items.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "最近状态", DataPropertyName = nameof(StartupItem.LastStatus), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

        var leftButtons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 72 };
        var addScript = new Button { Text = "新增脚本", Width = 100 };
        var addProgram = new Button { Text = "新增程序", Width = 100 };
        var delete = new Button { Text = "删除", Width = 80 };
        addScript.Click += (_, _) => AddItem(StartupItemType.PowerShellScript);
        addProgram.Click += (_, _) => AddItem(StartupItemType.Executable);
        delete.Click += async (_, _) => await DeleteSelectedAsync();
        leftButtons.Controls.AddRange([addScript, addProgram, delete]);

        split.Panel1.Controls.Add(_items);
        split.Panel1.Controls.Add(leftButtons);
        _items.SelectionChanged += (_, _) => LoadSelectedIntoDetails();

        var details = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 14, Padding = new Padding(12) };
        split.Panel2.Controls.Add(details);
        details.Controls.Add(_permissionStatus);
        details.Controls.Add(_selfStartStatus);
        AddLabeled(details, "名称", _name);
        AddLabeled(details, "路径", _path);
        AddLabeled(details, "参数", _arguments);
        AddLabeled(details, "工作目录", _workingDirectory);
        details.Controls.Add(_enabled);
        details.Controls.Add(_status);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42 };
        var save = new Button { Text = "保存", Width = 80 };
        var run = new Button { Text = "立即运行", Width = 90 };
        var stop = new Button { Text = "停止", Width = 80 };
        var logs = new Button { Text = "打开日志", Width = 90 };
        save.Click += async (_, _) => await SaveSelectedAsync();
        run.Click += async (_, _) => await RunSelectedAsync();
        stop.Click += (_, _) => StopSelected();
        logs.Click += (_, _) => OpenSelectedLogs();
        actions.Controls.AddRange([save, run, stop, logs]);
        details.Controls.Add(actions);
    }

    private void RefreshStatusLabels()
    {
        _permissionStatus.Text = WindowsPrivilege.IsCurrentProcessAdministrator() ? "当前权限：管理员" : "当前权限：普通用户";
        _selfStartStatus.Text = _config.StartManagerAtLogin ? "管理员开机自启：已启用" : "管理员开机自启：未启用";
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
            Filter = type == StartupItemType.PowerShellScript ? "PowerShell 脚本 (*.ps1)|*.ps1" : "可执行程序 (*.exe)|*.exe"
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
        _items.CurrentCell = _items.Rows[_items.Rows.Count - 1].Cells[0];
    }

    private StartupItem? SelectedItem => _items.SelectedRows.Count == 0 ? null : _items.SelectedRows[0].DataBoundItem as StartupItem;

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
        _status.Text = $"状态：{item.LastStatus}；退出码：{item.LastExitCode?.ToString() ?? ""}；最近启动：{item.LastRunStartedAt}";
    }

    private async Task SaveSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        string? selectedId = item.Id;
        item.Name = _name.Text.Trim();
        item.Path = _path.Text.Trim();
        item.Arguments = _arguments.Text;
        item.WorkingDirectory = _workingDirectory.Text.Trim();
        item.Enabled = _enabled.Checked;

        var validation = StartupItemValidator.Validate(item);
        if (!validation.IsValid)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, validation.Errors), "验证失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _configStore.Save(_config);
        RefreshList();
        RestoreSelection(selectedId);
        LoadSelectedIntoDetails();
    }

    private void RestoreSelection(string? selectedId)
    {
        if (selectedId is null) return;
        var match = _config.Items.FirstOrDefault(x => x.Id == selectedId);
        if (match is null) return;
        foreach (DataGridViewRow row in _items.Rows)
        {
            if (row.DataBoundItem == match)
            {
                row.Selected = true;
                break;
            }
        }
    }

    private async Task DeleteSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        var confirmed = MessageBox.Show(this, $"确定删除\u201c{item.Name}\u201d吗？", "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirmed != DialogResult.Yes) return;
        _config.Items.Remove(item);
        _configStore.Save(_config);
        RefreshList();
    }

    private async Task RunSelectedAsync()
    {
        var item = SelectedItem;
        if (item is null) return;
        await _itemLauncher.RunAsync(_config, item);
        RefreshList();
        LoadSelectedIntoDetails();
    }

    private void StopSelected()
    {
        MessageBox.Show(this, "当前版本不跟踪已启动进程，无法可靠停止。请在任务管理器或目标程序中结束。", "无法停止", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
