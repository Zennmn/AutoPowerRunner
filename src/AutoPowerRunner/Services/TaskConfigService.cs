using System.IO;
using System.Text.Json;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class TaskConfigService
{
    private readonly string _configDirectory;
    private readonly string _configFile;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public TaskConfigService(string configDirectory)
    {
        _configDirectory = configDirectory;
        _configFile = Path.Combine(configDirectory, "config.json");
    }

    public TaskConfigService(AppPaths paths) : this(paths.ConfigDirectory)
    {
    }

    public async Task<List<ManagedTask>> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_configDirectory);

        if (!File.Exists(_configFile))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_configFile);
            var tasks = await JsonSerializer.DeserializeAsync<List<ManagedTask>>(stream, _jsonOptions, cancellationToken);
            return tasks ?? [];
        }
        catch (JsonException)
        {
            BackupCorruptConfig();
            return [];
        }
    }

    public async Task SaveAsync(IReadOnlyCollection<ManagedTask> tasks, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_configDirectory);
        var tempFile = _configFile + ".tmp";
        await using (var stream = File.Create(tempFile))
        {
            await JsonSerializer.SerializeAsync(stream, tasks, _jsonOptions, cancellationToken);
        }

        if (File.Exists(_configFile))
        {
            File.Delete(_configFile);
        }

        File.Move(tempFile, _configFile);
    }

    private void BackupCorruptConfig()
    {
        var stamp = DateTimeOffset.Now.ToString("yyyyMMddHHmmss");
        var backupPath = Path.Combine(_configDirectory, $"config.corrupt.{stamp}.json");
        File.Move(_configFile, backupPath, overwrite: true);
    }
}
