using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using AutoPowerRunner.Models;

namespace AutoPowerRunner.Services;

public sealed class TaskConfigService : ITaskConfigService
{
    private readonly string _configDirectory;
    private readonly string _configFile;
    private readonly string? _expectedHash;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public TaskConfigService(string configDirectory, string? expectedHash = null)
    {
        _configDirectory = configDirectory;
        _configFile = Path.Combine(configDirectory, "config.json");
        _expectedHash = NormalizeHash(expectedHash);
    }

    public TaskConfigService(AppPaths paths, string? expectedHash = null)
        : this(paths.ConfigDirectory, expectedHash)
    {
    }

    public async Task<List<ManagedTask>> LoadAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_configDirectory);
        if (!File.Exists(_configFile))
        {
            VerifyAuthorizedTasks([]);
            return [];
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(_configFile, cancellationToken);
            var tasks = JsonSerializer.Deserialize<List<ManagedTask>>(bytes, _jsonOptions) ?? [];
            var normalized = NormalizeTasks(tasks);
            VerifyAuthorizedTasks(normalized);
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

    public static string ComputeConfigHash(string configFile)
    {
        if (!File.Exists(configFile)) return ComputeAuthorizationHash([]);
        var tasks = JsonSerializer.Deserialize<List<ManagedTask>>(File.ReadAllBytes(configFile)) ?? [];
        return ComputeAuthorizationHash(NormalizeTasks(tasks));
    }

    public static string ComputeAuthorizationHash(IEnumerable<ManagedTask> tasks)
    {
        var definitions = tasks.Select(task => new AuthorizedTaskDefinition(
            task.Id,
            task.Name,
            task.Type,
            task.Path,
            task.Arguments,
            task.WorkingDirectory,
            task.RunMode,
            task.IsEnabled));
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(definitions)));
    }

    private void VerifyAuthorizedTasks(IEnumerable<ManagedTask> tasks)
    {
        if (_expectedHash is null) return;
        var actual = ComputeAuthorizationHash(tasks);
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(_expectedHash),
                Convert.FromHexString(actual)))
        {
            throw new SecurityException("任务配置自管理员授权后已被修改。为安全起见，本次不会运行任何任务；请在界面中重新授权管理员自启。");
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

    private static string? NormalizeHash(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return null;
        var normalized = hash.Trim().ToUpperInvariant();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new SecurityException("管理员授权配置哈希无效。");
        }

        return normalized;
    }

    private void BackupCorruptConfig()
    {
        var backupPath = Path.Combine(_configDirectory, $"config.corrupt.{DateTimeOffset.Now:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}.json");
        File.Move(_configFile, backupPath);
    }

    private sealed record AuthorizedTaskDefinition(
        Guid Id,
        string Name,
        ManagedTaskType Type,
        string Path,
        string Arguments,
        string WorkingDirectory,
        ManagedTaskRunMode RunMode,
        bool IsEnabled);
}
