using System.IO;
using System.Windows;
using AutoPowerRunner.Models;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace AutoPowerRunner;

public partial class TaskEditorWindow : Window
{
    private readonly ManagedTask _task;

    public TaskEditorWindow(ManagedTask? task = null)
    {
        InitializeComponent();

        _task = task?.Clone() ?? new ManagedTask();
        Result = _task.Clone();

        LoadFields(_task);
    }

    public ManagedTask Result { get; private set; }

    private void LoadFields(ManagedTask task)
    {
        NameBox.Text = task.Name;
        TypeValue.Text = task.Type == ManagedTaskType.Executable ? "EXE 程序" : "PowerShell 脚本";
        PathBox.Text = task.Path;
        ArgumentsBox.Text = task.Arguments;
        WorkingDirectoryBox.Text = task.WorkingDirectory;
        RunModeValue.Text = task.RunMode == ManagedTaskRunMode.LongRunning
            ? "长期运行（失败自动重启）"
            : "运行一次";
        EnabledBox.IsChecked = task.IsEnabled;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = GetFileFilter()
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        PathBox.Text = dialog.FileName;
        WorkingDirectoryBox.Text = Path.GetDirectoryName(dialog.FileName) ?? "";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, "请填写任务名称。", "任务", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(PathBox.Text))
        {
            MessageBox.Show(this, "请选择文件路径。", "任务", MessageBoxButton.OK, MessageBoxImage.Warning);
            PathBox.Focus();
            return;
        }

        var selectedType = _task.Type;
        var path = PathBox.Text.Trim();
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "所选文件不存在。", "任务", MessageBoxButton.OK, MessageBoxImage.Warning);
            PathBox.Focus();
            return;
        }

        var expectedExtension = selectedType == ManagedTaskType.Executable ? ".exe" : ".ps1";
        if (!string.Equals(Path.GetExtension(path), expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, $"任务类型与文件不匹配，请选择 {expectedExtension} 文件。", "任务", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var workingDirectory = WorkingDirectoryBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(workingDirectory) && !Directory.Exists(workingDirectory))
        {
            MessageBox.Show(this, "工作目录不存在。", "任务", MessageBoxButton.OK, MessageBoxImage.Warning);
            WorkingDirectoryBox.Focus();
            return;
        }

        Result = new ManagedTask
        {
            Id = _task.Id,
            Name = NameBox.Text.Trim(),
            Type = selectedType,
            Path = path,
            Arguments = ArgumentsBox.Text.Trim(),
            WorkingDirectory = workingDirectory,
            RunMode = _task.RunMode,
            IsEnabled = EnabledBox.IsChecked == true,
            LastResult = _task.LastResult
        };

        DialogResult = true;
    }

    private string GetFileFilter()
    {
        return _task.Type == ManagedTaskType.Executable
            ? "EXE 程序 (*.exe)|*.exe|所有文件 (*.*)|*.*"
            : "PowerShell 脚本 (*.ps1)|*.ps1|所有文件 (*.*)|*.*";
    }
}
