using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AutoPowerRunner.Models;
using AutoPowerRunner.ViewModels;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace AutoPowerRunner;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    public async Task InitializeAsync()
    {
        await _viewModel.LoadAsync();
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    private async void ImportPowerShellScript_Click(object sender, RoutedEventArgs e)
    {
        await ImportTaskFromFileAsync(ManagedTaskType.PowerShellScript);
    }

    private async void ImportExecutable_Click(object sender, RoutedEventArgs e)
    {
        await ImportTaskFromFileAsync(ManagedTaskType.Executable);
    }

    private async Task ImportTaskFromFileAsync(ManagedTaskType type)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Filter = GetFileFilter(type),
            Title = type == ManagedTaskType.Executable ? "选择要新增的 EXE 程序" : "选择要新增的 PowerShell 脚本"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await _viewModel.ImportTaskAsync(type, dialog.FileName);
        }
        catch (Exception ex)
        {
            ShowError("无法导入任务。", ex);
        }
    }

    private async void AddTask_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TaskEditorWindow { Owner = this };
        if (editor.ShowDialog() == true)
        {
            try
            {
                await _viewModel.AddOrUpdateTaskAsync(editor.Result);
            }
            catch (Exception ex)
            {
                ShowError("无法保存任务。", ex);
            }
        }
    }

    private async void EditTask_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTask is null)
        {
            return;
        }

        var editor = new TaskEditorWindow(_viewModel.SelectedTask) { Owner = this };
        if (editor.ShowDialog() == true)
        {
            try
            {
                await _viewModel.AddOrUpdateTaskAsync(editor.Result);
            }
            catch (Exception ex)
            {
                ShowError("无法保存任务。", ex);
            }
        }
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(_viewModel.LogFile))
        {
            MessageBox.Show(this, "日志文件还不存在。", "打开日志", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_viewModel.LogFile)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowError("无法打开日志文件。", ex);
        }
    }

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTask is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            FileName = _viewModel.SelectedTask.Path,
            Filter = GetFileFilter(_viewModel.SelectedTask.Type)
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _viewModel.SelectedTask.Path = dialog.FileName;
        _viewModel.SelectedTask.WorkingDirectory = Path.GetDirectoryName(dialog.FileName) ?? "";
    }

    private void BrowseWorkingDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTask is null)
        {
            return;
        }

        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "选择工作目录",
            SelectedPath = Directory.Exists(_viewModel.SelectedTask.WorkingDirectory)
                ? _viewModel.SelectedTask.WorkingDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        _viewModel.SelectedTask.WorkingDirectory = dialog.SelectedPath;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void ShowError(string message, Exception exception)
    {
        MessageBox.Show(this, $"{message}{Environment.NewLine}{exception.Message}", "自启管家", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string GetFileFilter(ManagedTaskType type)
    {
        return type == ManagedTaskType.Executable
            ? "EXE 程序 (*.exe)|*.exe|所有文件 (*.*)|*.*"
            : "PowerShell 脚本 (*.ps1)|*.ps1|所有文件 (*.*)|*.*";
    }
}
