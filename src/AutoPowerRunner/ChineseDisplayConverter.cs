using System.Globalization;
using System.Windows.Data;
using AutoPowerRunner.Models;

namespace AutoPowerRunner;

public sealed class ChineseDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        return value switch
        {
            TaskRuntimeResult result => ConvertTaskRuntimeResult(result),
            ManagedTaskType.PowerShellScript => "PowerShell 脚本",
            ManagedTaskType.Executable => "EXE 程序",
            ManagedTaskRunMode.RunOnce => "运行一次",
            ManagedTaskRunMode.LongRunning => "长期运行（失败自动重启）",
            TaskRuntimeStatus.NotRunning => "未运行",
            TaskRuntimeStatus.Running => "运行中",
            TaskRuntimeStatus.Exited => "已退出",
            TaskRuntimeStatus.Succeeded => "已成功",
            TaskRuntimeStatus.Failed => "运行失败",
            TaskRuntimeStatus.Stopped => "已停止",
            TaskRuntimeStatus.FailedToStart => "启动失败",
            TaskRuntimeStatus.Restarting => "正在重启",
            true => "是",
            false => "否",
            null => "",
            _ => value.ToString() ?? ""
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture) =>
        throw new NotSupportedException();

    private static string ConvertTaskRuntimeResult(TaskRuntimeResult result) => result.Status switch
    {
        TaskRuntimeStatus.NotRunning => "未运行",
        TaskRuntimeStatus.Running => "运行中",
        TaskRuntimeStatus.Exited => result.ExitCode == 0 ? "已成功" : "已退出",
        TaskRuntimeStatus.Succeeded => "已成功",
        TaskRuntimeStatus.Failed => "运行失败",
        TaskRuntimeStatus.Stopped => "已停止",
        TaskRuntimeStatus.FailedToStart => "启动失败",
        TaskRuntimeStatus.Restarting => "正在重启",
        _ => result.Status.ToString()
    };
}
