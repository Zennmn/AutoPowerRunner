using System.Security.Principal;
using System.Text;

namespace AutoElevateLauncher;

public sealed class ScheduledTaskService
{
    public const string ManagerTaskName = "AutoElevateLauncher-Manager";

    private readonly IProcessRunner _processRunner;

    public ScheduledTaskService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<ProcessCommandResult> CreateOrUpdateManagerSelfStartTaskAsync(string appExePath, CancellationToken cancellationToken = default)
    {
        var userId = WindowsIdentity.GetCurrent().Name;
        var xml = TaskXmlBuilder.BuildManagerSelfStartTaskXml(appExePath, userId);
        var xmlPath = Path.Combine(Path.GetTempPath(), ManagerTaskName + ".xml");
        await File.WriteAllTextAsync(xmlPath, xml, Encoding.Unicode, cancellationToken);

        try
        {
            return await _processRunner.RunAsync("schtasks.exe", $"/Create /TN \"{ManagerTaskName}\" /XML \"{xmlPath}\" /F", null, cancellationToken);
        }
        finally
        {
            TryDelete(xmlPath);
        }
    }

    public Task<ProcessCommandResult> DeleteManagerSelfStartTaskAsync(CancellationToken cancellationToken = default)
    {
        return _processRunner.RunAsync("schtasks.exe", $"/Delete /TN \"{ManagerTaskName}\" /F", null, cancellationToken);
    }

    public Task<ProcessCommandResult> EnableManagerSelfStartElevatedAsync(string appExePath, CancellationToken cancellationToken = default)
    {
        return _processRunner.RunElevatedAsync(appExePath, "--enable-manager-startup", cancellationToken);
    }

    public Task<ProcessCommandResult> DisableManagerSelfStartElevatedAsync(string appExePath, CancellationToken cancellationToken = default)
    {
        return _processRunner.RunElevatedAsync(appExePath, "--disable-manager-startup", cancellationToken);
    }

    public static void TryDelete(string path)
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
        }
    }
}
