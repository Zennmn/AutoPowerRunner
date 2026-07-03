using System.Security.Principal;
using System.Text;

namespace AutoElevateLauncher;

public sealed class ScheduledTaskService
{
    private readonly IProcessRunner _processRunner;

    public ScheduledTaskService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<ProcessCommandResult> CreateOrUpdateStartupItemTaskAsync(StartupItem item, string appExePath, CancellationToken cancellationToken = default)
    {
        item.EnsureTaskName();
        var userId = WindowsIdentity.GetCurrent().Name;
        var xml = TaskXmlBuilder.BuildStartupItemTaskXml(item, appExePath, userId);
        var xmlPath = Path.Combine(Path.GetTempPath(), item.TaskName + ".xml");
        await File.WriteAllTextAsync(xmlPath, xml, Encoding.Unicode, cancellationToken);

        try
        {
            return await _processRunner.RunAsync("schtasks.exe", $"/Create /TN \"{item.TaskName}\" /XML \"{xmlPath}\" /F", null, cancellationToken);
        }
        finally
        {
            TryDelete(xmlPath);
        }
    }

    public Task<ProcessCommandResult> DeleteTaskAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        item.EnsureTaskName();
        return _processRunner.RunAsync("schtasks.exe", $"/Delete /TN \"{item.TaskName}\" /F", null, cancellationToken);
    }

    public Task<ProcessCommandResult> RunTaskAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        item.EnsureTaskName();
        return _processRunner.RunAsync("schtasks.exe", $"/Run /TN \"{item.TaskName}\"", null, cancellationToken);
    }

    public Task<ProcessCommandResult> StopTaskAsync(StartupItem item, CancellationToken cancellationToken = default)
    {
        item.EnsureTaskName();
        return _processRunner.RunAsync("schtasks.exe", $"/End /TN \"{item.TaskName}\"", null, cancellationToken);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary task XML cleanup failure should not hide the schtasks result.
        }
    }
}