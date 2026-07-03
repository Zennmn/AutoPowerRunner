using System.Text.Json;

namespace AutoElevateLauncher;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public StartupConfig Load()
    {
        if (!File.Exists(AppPaths.ConfigFile))
        {
            return new StartupConfig();
        }

        string json;
        try
        {
            json = File.ReadAllText(AppPaths.ConfigFile);
        }
        catch (IOException)
        {
            // Config file is unreadable; start with an empty config rather than crashing the app.
            return new StartupConfig();
        }

        try
        {
            return JsonSerializer.Deserialize<StartupConfig>(json, JsonOptions) ?? new StartupConfig();
        }
        catch (JsonException)
        {
            // Corrupted config: back up the bad file (so the invalid content is preserved) and
            // start fresh so the app keeps running instead of crashing on every launch.
            BackUpCorruptedConfig();
            return new StartupConfig();
        }
    }

    private static void BackUpCorruptedConfig()
    {
        try
        {
            var directory = AppPaths.AppDataDirectory;
            var baseName = $"config.json.bad-{DateTimeOffset.Now:yyyyMMdd-HHmmss}";
            var path = Path.Combine(directory, baseName);
            var counter = 1;
            while (File.Exists(path))
            {
                path = Path.Combine(directory, $"{baseName}-{counter}");
                counter++;
            }
            File.Move(AppPaths.ConfigFile, path);
        }
        catch
        {
            // Best-effort backup; if moving fails we still return an empty config to keep the app alive.
        }
    }

    public void Save(StartupConfig config)
    {
        Directory.CreateDirectory(AppPaths.AppDataDirectory);
        Directory.CreateDirectory(AppPaths.LogsDirectory);

        foreach (var item in config.Items)
        {
            item.EnsureTaskName();
        }

        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(AppPaths.ConfigFile, json);
    }
}