namespace AutoPowerRunner.Models;

public enum ManagedTaskType
{
    PowerShellScript = 0,
    Executable = 1
}

public enum ManagedTaskRunMode
{
    RunOnce = 0,
    LongRunning = 1
}

public enum TaskRuntimeStatus
{
    NotRunning = 0,
    Running = 1,
    Exited = 2,
    FailedToStart = 3,
    Succeeded = 4,
    Failed = 5,
    Stopped = 6,
    Restarting = 7
}
