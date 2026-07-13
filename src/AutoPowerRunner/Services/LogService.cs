using System.Collections.Concurrent;
using System.IO;

namespace AutoPowerRunner.Services;

public sealed class LogService : ILogService
{
    private const long MaxLogBytes = 5 * 1024 * 1024;
    private const int RetainedLogFiles = 3;
    private static readonly ConcurrentDictionary<string, object> PathLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly string _logFile;
    private readonly object _gate;

    public LogService(AppPaths paths)
    {
        _logFile = paths.LogFile;
        _gate = PathLocks.GetOrAdd(Path.GetFullPath(_logFile), static _ => new object());
    }

    public string LogFile => _logFile;

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        var detail = exception is null
            ? message
            : $"{message} {exception.GetType().Name}: {exception.Message}{Environment.NewLine}{exception}";
        Write("ERROR", detail);
    }

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{level}] {message}{Environment.NewLine}";
        lock (_gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_logFile)!);
            RotateIfNeeded();
            File.AppendAllText(_logFile, line);
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_logFile) || new FileInfo(_logFile).Length < MaxLogBytes) return;
        var oldest = $"{_logFile}.{RetainedLogFiles}";
        if (File.Exists(oldest)) File.Delete(oldest);
        for (var index = RetainedLogFiles - 1; index >= 1; index--)
        {
            var source = $"{_logFile}.{index}";
            if (File.Exists(source)) File.Move(source, $"{_logFile}.{index + 1}", overwrite: true);
        }
        File.Move(_logFile, $"{_logFile}.1", overwrite: true);
    }
}
