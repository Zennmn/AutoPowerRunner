using System.Text.Json.Serialization;

namespace AutoPowerRunner.Models;

public sealed class TaskRuntimeResult
{
    public TaskRuntimeStatus Status { get; set; } = TaskRuntimeStatus.NotRunning;
    public int? ExitCode { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? ExitedAt { get; set; }
    public string? Error { get; set; }

    [JsonIgnore]
    public string Summary
    {
        get
        {
            return Status switch
            {
                TaskRuntimeStatus.NotRunning => "Not running",
                TaskRuntimeStatus.Running => StartedAt is null ? "Running" : $"Running since {StartedAt:yyyy-MM-dd HH:mm:ss}",
                TaskRuntimeStatus.Exited => ExitCode is null ? "Exited" : $"Exited with code {ExitCode}",
                TaskRuntimeStatus.FailedToStart => string.IsNullOrWhiteSpace(Error) ? "Failed to start" : $"Failed: {Error}",
                _ => Status.ToString()
            };
        }
    }
}
