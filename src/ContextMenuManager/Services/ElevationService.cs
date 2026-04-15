using System.Security.Principal;
using System.Diagnostics;

namespace ContextMenuManager.Services;

public interface IElevationService
{
    bool IsRunningAsAdmin();
    bool RestartAsAdmin();
}

public class ElevationService : IElevationService
{
    private readonly ILoggingService _logger;

    public ElevationService(ILoggingService logger)
    {
        _logger = logger;
    }

    public bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            _logger.LogInfo($"Admin check: {(isAdmin ? "Running as admin" : "Not running as admin")}");
            return isAdmin;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error checking admin status: {ex.Message}");
            return false;
        }
    }

    public bool RestartAsAdmin()
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName,
                Verb = "runas",
                Arguments = "--elevated"
            };

            try
            {
                Process.Start(processInfo);
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                _logger.LogWarning("User declined UAC prompt");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error restarting as admin: {ex.Message}");
            return false;
        }
    }
}
