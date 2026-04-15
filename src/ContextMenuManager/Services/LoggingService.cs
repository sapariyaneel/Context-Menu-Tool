using System.IO;

namespace ContextMenuManager.Services;

public interface ILoggingService
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogDebug(string message);
}

public class LoggingService : ILoggingService
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public LoggingService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ContextMenuManager",
            "Logs"
        );
        
        Directory.CreateDirectory(appDataPath);
        _logFilePath = Path.Combine(appDataPath, $"app_{DateTime.Now:yyyyMMdd}.log");
    }

    public void LogInfo(string message) => WriteLog("INFO", message);
    public void LogWarning(string message) => WriteLog("WARN", message);
    public void LogError(string message) => WriteLog("ERROR", message);
    public void LogDebug(string message) => WriteLog("DEBUG", message);

    private void WriteLog(string level, string message)
    {
        var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        
        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
                // Silently fail if we can't write to log
            }
        }

        System.Diagnostics.Debug.WriteLine(logEntry);
    }
}
