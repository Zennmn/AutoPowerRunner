using AutoPowerRunner;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Tests;

public sealed class ChineseDisplayConverterTests
{
    [Theory]
    [InlineData(ManagedTaskType.PowerShellScript, "PowerShell 脚本")]
    [InlineData(ManagedTaskType.Executable, "EXE 程序")]
    [InlineData(ManagedTaskRunMode.RunOnce, "运行一次")]
    [InlineData(ManagedTaskRunMode.LongRunning, "长期运行")]
    [InlineData(TaskRuntimeStatus.NotRunning, "未运行")]
    [InlineData(TaskRuntimeStatus.Running, "运行中")]
    [InlineData(TaskRuntimeStatus.Exited, "已退出")]
    [InlineData(TaskRuntimeStatus.FailedToStart, "启动失败")]
    [InlineData(true, "是")]
    [InlineData(false, "否")]
    public void Convert_ReturnsChineseDisplayText(object value, string expected)
    {
        var converter = new ChineseDisplayConverter();

        var result = converter.Convert(value, typeof(string), null, null);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(TaskRuntimeStatus.Running, null, "运行中")]
    [InlineData(TaskRuntimeStatus.Exited, 0, "已成功")]
    [InlineData(TaskRuntimeStatus.Exited, 2, "已退出")]
    [InlineData(TaskRuntimeStatus.FailedToStart, null, "启动失败")]
    public void Convert_ReturnsShortChineseTaskResultStatus(TaskRuntimeStatus status, int? exitCode, string expected)
    {
        var converter = new ChineseDisplayConverter();
        var result = new TaskRuntimeResult { Status = status, ExitCode = exitCode };

        var converted = converter.Convert(result, typeof(string), null, null);

        Assert.Equal(expected, converted);
    }
}
