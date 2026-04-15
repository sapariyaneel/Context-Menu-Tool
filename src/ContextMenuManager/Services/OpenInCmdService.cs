using Microsoft.Win32;

namespace ContextMenuManager.Services;

public class OpenInCmdService
{
    private readonly ILoggingService _logger;

    private const string CmdMenuName = "CommandPrompt";

    private static readonly string[] RegistryPaths = new[]
    {
        @"Directory\shell",
        @"Directory\Background\shell",
        @"Drive\shell",
    };

    public OpenInCmdService(ILoggingService logger)
    {
        _logger = logger;
    }

    public bool IsEnabled()
    {
        try
        {
            using var shellKey = Registry.ClassesRoot.OpenSubKey(@"Directory\shell\" + CmdMenuName);
            return shellKey != null;
        }
        catch
        {
            return false;
        }
    }

    public bool Enable()
    {
        try
        {
            foreach (var basePath in RegistryPaths)
            {
                CreateMenuForPath(basePath);
            }

            _logger.LogInfo("Command Prompt submenu enabled for all contexts");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError("Access denied. Administrator privileges required.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error enabling Command Prompt: {ex.Message}");
            return false;
        }
    }

    private void CreateMenuForPath(string basePath)
    {
        var fullPath = basePath + @"\" + CmdMenuName;

        using var shellKey = Registry.ClassesRoot.CreateSubKey(fullPath);
        if (shellKey == null)
        {
            _logger.LogError($"Failed to create key: {fullPath}");
            return;
        }

        shellKey.SetValue("MUIVerb", "Command Prompt");
        shellKey.SetValue("Icon", "cmd.exe,0");
        shellKey.SetValue("SubCommands", "");

        using var subKey = shellKey.CreateSubKey("shell");
        if (subKey == null) return;

        var exePath = AppDomain.CurrentDomain.BaseDirectory + "Context Menu Tool.exe";

        using var openHereKey = subKey.CreateSubKey("openhere");
        if (openHereKey != null)
        {
            openHereKey.SetValue(null, "Open Here");
            openHereKey.SetValue("Icon", "cmd.exe,0");
            using var cmd = openHereKey.CreateSubKey("command");
            cmd?.SetValue(null, $"\"{exePath}\" --open-cmd %V");
        }

        using var adminKey = subKey.CreateSubKey("openhereadmin");
        if (adminKey != null)
        {
            adminKey.SetValue(null, "Open Here as Administrator");
            adminKey.SetValue("Icon", "cmd.exe,0");
            using var cmd = adminKey.CreateSubKey("command");
            cmd?.SetValue(null, $"\"{exePath}\" --elevate-cmd %V");
        }
    }

    public bool Disable()
    {
        try
        {
            foreach (var basePath in RegistryPaths)
            {
                DeleteMenuForPath(basePath);
            }

            _logger.LogInfo("Command Prompt disabled");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError("Access denied. Administrator privileges required.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error disabling Command Prompt: {ex.Message}");
            return false;
        }
    }

    private void DeleteMenuForPath(string basePath)
    {
        try
        {
            using var shellKey = Registry.ClassesRoot.OpenSubKey(basePath, true);
            if (shellKey != null)
            {
                var subKeys = shellKey.GetSubKeyNames();
                if (subKeys.Contains(CmdMenuName))
                {
                    shellKey.DeleteSubKeyTree(CmdMenuName, false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error deleting Command Prompt for {basePath}: {ex.Message}");
        }
    }
}
