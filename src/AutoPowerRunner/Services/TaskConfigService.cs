using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class TaskConfigService : ITaskConfigService
{
    private readonly string _configDirectory;
    private readonly string _configFile;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public TaskConfigService(string configDirectory)
    {
        _configDirectory = configDirectory;
        _configFile = Path.Combine(configDirectory, "config.json");
    }

    public TaskConfigService(AppPaths paths)
        : this(paths.ConfigDirectory)
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
            var bytes = await File.ReadAllBytesAsync(_configFile, cancellationToken);
            var tasks = JsonSerializer.Deserialize<List<ManagedTask>>(bytes, _jsonOptions) ?? [];
            var normalized = NormalizeTasks(tasks);
            return normalized;
        }
        catch (JsonException)
        {
            BackupCorruptConfig();
            return [];
        }
    }

    public async Task SaveAsync(IReadOnlyCollection<ManagedTask> tasks, CancellationToken cancellationToken = default)
    {
        var snapshot = tasks.Select(task => task.Clone()).ToArray();
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_configDirectory);
            var tempFile = $"{_configFile}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var stream = File.Create(tempFile))
                {
                    await JsonSerializer.SerializeAsync(stream, snapshot, _jsonOptions, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }

                if (File.Exists(_configFile))
                {
                    File.Replace(tempFile, _configFile, null);
                }
                else
                {
                    File.Move(tempFile, _configFile);
                }
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private static List<ManagedTask> NormalizeTasks(IEnumerable<ManagedTask> tasks)
    {
        var normalized = new List<ManagedTask>();
        var ids = new HashSet<Guid>();
        var index = 0;
        foreach (var task in tasks)
        {
            if (task is null) continue;
            if (task.Id == Guid.Empty || ids.Contains(task.Id))
            {
                task.Id = CreateDeterministicId(task, index);
                while (ids.Contains(task.Id)) task.Id = CreateDeterministicId(task, ++index);
            }
            ids.Add(task.Id);
            task.Name ??= "";
            task.Path ??= "";
            task.Arguments ??= "";
            task.WorkingDirectory ??= "";
            if (!Enum.IsDefined(task.Type)) task.Type = ManagedTaskType.PowerShellScript;
            if (!Enum.IsDefined(task.RunMode)) task.RunMode = ManagedTaskRunMode.RunOnce;
            task.LastResult ??= new TaskRuntimeResult();
            if (!Enum.IsDefined(task.LastResult.Status)) task.LastResult.Status = TaskRuntimeStatus.NotRunning;
            normalized.Add(task);
            index++;
        }

        return normalized;
    }

    private static Guid CreateDeterministicId(ManagedTask task, int index)
    {
        var material = $"{index}|{task.Name}|{task.Type}|{task.Path}|{task.Arguments}|{task.WorkingDirectory}";
        return new Guid(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(material)).AsSpan(0, 16));
    }

    private void BackupCorruptConfig()
    {
        var backupPath = Path.Combine(_configDirectory, $"config.corrupt.{DateTimeOffset.Now:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.json");
        File.Move(_configFile, backupPath);
    }

}
