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

        TypeBox.ItemsSource = Enum.GetValues<ManagedTaskType>();
        RunModeBox.ItemsSource = Enum.GetValues<ManagedTaskRunMode>();

        LoadFields(_task);
    }

    public ManagedTask Result { get; private set; }

    private void LoadFields(ManagedTask task)
    {
        NameBox.Text = task.Name;
        TypeBox.SelectedItem = task.Type;
        PathBox.Text = task.Path;
        ArgumentsBox.Text = task.Arguments;
        WorkingDirectoryBox.Text = task.WorkingDirectory;
        RunModeBox.SelectedItem = task.RunMode;
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

        Result = new ManagedTask
        {
            Id = _task.Id,
            Name = NameBox.Text.Trim(),
            Type = TypeBox.SelectedItem is ManagedTaskType type ? type : ManagedTaskType.PowerShellScript,
            Path = PathBox.Text.Trim(),
            Arguments = ArgumentsBox.Text.Trim(),
            WorkingDirectory = WorkingDirectoryBox.Text.Trim(),
            RunMode = RunModeBox.SelectedItem is ManagedTaskRunMode runMode ? runMode : ManagedTaskRunMode.RunOnce,
            IsEnabled = EnabledBox.IsChecked == true,
            LastResult = _task.LastResult
        };

        DialogResult = true;
    }

    private string GetFileFilter()
    {
        return TypeBox.SelectedItem is ManagedTaskType.Executable
            ? "EXE 程序 (*.exe)|*.exe|所有文件 (*.*)|*.*"
            : "PowerShell 脚本 (*.ps1)|*.ps1|所有文件 (*.*)|*.*";
    }
}
