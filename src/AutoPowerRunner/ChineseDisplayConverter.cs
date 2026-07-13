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
            ManagedTaskRunMode.LongRunning => "长期运行",
            TaskRuntimeStatus.NotRunning => "未运行",
            TaskRuntimeStatus.Running => "运行中",
            TaskRuntimeStatus.Exited => "已退出",
            TaskRuntimeStatus.FailedToStart => "启动失败",
            true => "是",
            false => "否",
            null => "",
            _ => value.ToString() ?? ""
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotSupportedException();
    }

    private static string ConvertTaskRuntimeResult(TaskRuntimeResult result)
    {
        return result.Status switch
        {
            TaskRuntimeStatus.NotRunning => "未运行",
            TaskRuntimeStatus.Running => "运行中",
            TaskRuntimeStatus.Exited => result.ExitCode == 0 ? "已成功" : "已退出",
            TaskRuntimeStatus.FailedToStart => "启动失败",
            _ => result.Status.ToString()
        };
    }
}
