using System.IO;

namespace AutoPowerRunner.Services;

public sealed class AppPaths
{
    public AppPaths(string appDataRoot, string localAppDataRoot)
    {
        ConfigDirectory = Path.Combine(appDataRoot, "AutoPowerRunner");
        ConfigFile = Path.Combine(ConfigDirectory, "config.json");
        LogDirectory = Path.Combine(localAppDataRoot, "AutoPowerRunner", "logs");
        LogFile = Path.Combine(LogDirectory, "app.log");
    }

    public string ConfigDirectory { get; }
    public string ConfigFile { get; }
    public string LogDirectory { get; }
    public string LogFile { get; }

    public static AppPaths ForCurrentUser()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPaths(appData, localAppData);
    }
}
