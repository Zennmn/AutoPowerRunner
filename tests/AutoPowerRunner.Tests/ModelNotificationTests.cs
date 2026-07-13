using System.ComponentModel;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Tests;

public sealed class ModelNotificationTests
{
    [Fact]
    public void ManagedTask_IsEnabled_RaisesPropertyChanged()
    {
        var task = new ManagedTask();
        var changedProperties = new List<string?>();
        var notifyingTask = Assert.IsAssignableFrom<INotifyPropertyChanged>(task);
        notifyingTask.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        task.IsEnabled = !task.IsEnabled;

        Assert.Contains(nameof(ManagedTask.IsEnabled), changedProperties);
    }

    [Fact]
    public void TaskRuntimeResult_Status_RaisesStatusAndSummaryPropertyChanged()
    {
        var result = new TaskRuntimeResult();
        var changedProperties = new List<string?>();
        var notifyingResult = Assert.IsAssignableFrom<INotifyPropertyChanged>(result);
        notifyingResult.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        result.Status = TaskRuntimeStatus.Running;

        Assert.Contains(nameof(TaskRuntimeResult.Status), changedProperties);
        Assert.Contains(nameof(TaskRuntimeResult.Summary), changedProperties);
    }

    [Fact]
    public void TaskRuntimeResult_Summary_UsesChineseDisplayText()
    {
        var result = new TaskRuntimeResult
        {
            Status = TaskRuntimeStatus.Running,
            StartedAt = new DateTimeOffset(2026, 7, 5, 19, 30, 0, TimeSpan.FromHours(8))
        };

        Assert.Equal("运行中，自 2026-07-05 19:30:00 起", result.Summary);

        result.Status = TaskRuntimeStatus.Exited;
        result.ExitCode = 0;
        Assert.Equal("已退出，退出码 0", result.Summary);

        result.Status = TaskRuntimeStatus.FailedToStart;
        result.Error = "文件不存在";
        Assert.Equal("启动失败：文件不存在", result.Summary);
    }
}
