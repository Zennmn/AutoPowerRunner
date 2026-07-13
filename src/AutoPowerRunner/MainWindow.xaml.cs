using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AutoPowerRunner.Models;
using AutoPowerRunner.ViewModels;
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
        ApplyResponsiveLayout(Width);
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width);
    }

    private void ApplyResponsiveLayout(double windowWidth)
    {
        var compactHeader = windowWidth < 1120;
        HeaderRow.Height = new GridLength(compactHeader ? 108 : 68);
        System.Windows.Controls.Grid.SetRow(HeaderStatusPanel, compactHeader ? 1 : 0);
        System.Windows.Controls.Grid.SetColumn(HeaderStatusPanel, compactHeader ? 0 : 1);
        System.Windows.Controls.Grid.SetColumnSpan(HeaderStatusPanel, compactHeader ? 2 : 1);
        HeaderStatusPanel.Margin = compactHeader ? new Thickness(0, 0, 0, 8) : new Thickness(0);

        TaskListColumn.Width = new GridLength(windowWidth switch
        {
            < 1020 => 320,
            < 1200 => 360,
            < 1360 => 400,
            _ => 430
        });
        WorkspaceGapColumn.Width = new GridLength(windowWidth < 1020 ? 12 : 18);

        var stackDetails = windowWidth < 1180;
        DetailPrimaryColumn.Width = new GridLength(1, GridUnitType.Star);
        DetailGapColumn.Width = new GridLength(stackDetails ? 0 : 16);
        DetailSecondaryColumn.Width = stackDetails
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);

        System.Windows.Controls.Grid.SetRow(DetailPrimaryPanel, 0);
        System.Windows.Controls.Grid.SetColumn(DetailPrimaryPanel, 0);
        System.Windows.Controls.Grid.SetRow(DetailSecondaryPanel, stackDetails ? 1 : 0);
        System.Windows.Controls.Grid.SetColumn(DetailSecondaryPanel, stackDetails ? 0 : 2);
        DetailSecondaryPanel.Margin = stackDetails ? new Thickness(0, 16, 0, 0) : new Thickness(0);
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

    private void AddTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { ContextMenu: { } menu } button)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    private void TaskItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBoxItem item)
        {
            item.IsSelected = true;
        }
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
