using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

namespace Primple.Core.Services;

/// <summary>
/// Represents a log entry with timestamp and severity
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = "";
    public string Source { get; set; } = "";
    
    public override string ToString() => $"[{Timestamp:HH:mm:ss}] [{Level}] [{Source}] {Message}";
}

/// <summary>
/// Logging service for debug mode
/// </summary>
public interface ILogService : INotifyPropertyChanged
{
    ObservableCollection<LogEntry> Logs { get; }
    bool IsEnabled { get; set; }
    
    void Log(string message, string source = "App", string level = "INFO");
    void LogError(string message, string source = "App");
    void LogWarning(string message, string source = "App");
    void LogDebug(string message, string source = "App");
    void Clear();
    void Export(string filePath);
}

public class LogService : ILogService
{
    private readonly ObservableCollection<LogEntry> _logs = new();
    private bool _isEnabled = false;
    private readonly object _lockObj = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LogEntry> Logs => _logs;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            }
        }
    }

    public void Log(string message, string source = "App", string level = "INFO")
    {
        if (!_isEnabled && level != "ERROR") return;

        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Message = message,
            Source = source
        };

        lock (_lockObj)
        {
            // Keep max 1000 entries to prevent memory issues
            while (_logs.Count >= 1000)
            {
                _logs.RemoveAt(0);
            }
            _logs.Add(entry);
        }
    }

    public void LogError(string message, string source = "App")
    {
        Log(message, source, "ERROR");
    }

    public void LogWarning(string message, string source = "App")
    {
        Log(message, source, "WARN");
    }

    public void LogDebug(string message, string source = "App")
    {
        Log(message, source, "DEBUG");
    }

    public void Clear()
    {
        lock (_lockObj)
        {
            _logs.Clear();
        }
    }

    public void Export(string filePath)
    {
        try
        {
            var lines = new List<string>();
            lock (_lockObj)
            {
                foreach (var log in _logs)
                {
                    lines.Add(log.ToString());
                }
            }
            File.WriteAllLines(filePath, lines);
        }
        catch
        {
            // Silently fail
        }
    }
}
