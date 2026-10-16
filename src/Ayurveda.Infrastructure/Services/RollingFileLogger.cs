using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Ayurveda.Infrastructure.Services;

public class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly ConcurrentDictionary<string, RollingFileLogger> _loggers = new();
    private static readonly object _fileLock = new();

    public RollingFileLoggerProvider(string logDirectory)
    {
        _logDirectory = logDirectory;
        Directory.CreateDirectory(_logDirectory);
        CleanOldLogs();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new RollingFileLogger(name, _logDirectory, _fileLock));
    }

    private void CleanOldLogs()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var dir = new DirectoryInfo(_logDirectory);
            if (dir.Exists)
            {
                foreach (var file in dir.GetFiles("app_*.log"))
                {
                    if (file.CreationTimeUtc < cutoff || file.LastWriteTimeUtc < cutoff)
                    {
                        try { file.Delete(); } catch { }
                    }
                }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}

public class RollingFileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly string _logDirectory;
    private readonly object _fileLock;

    public RollingFileLogger(string categoryName, string logDirectory, object fileLock)
    {
        _categoryName = categoryName;
        _logDirectory = logDirectory;
        _fileLock = fileLock;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null) return;

        var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {message}";
        if (exception != null)
        {
            line += Environment.NewLine + exception;
        }

        try
        {
            var fileName = $"app_{DateTime.UtcNow:yyyyMMdd}.log";
            var filePath = Path.Combine(_logDirectory, fileName);

            lock (_fileLock)
            {
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Logging failure should never crash the app
        }
    }
}
