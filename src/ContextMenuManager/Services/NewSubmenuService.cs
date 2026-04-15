using Microsoft.Win32;

namespace ContextMenuManager.Services;

public class NewSubmenuService
{
    private readonly ILoggingService _logger;

    private const string NewSubmenuName = "New";

    private static readonly List<(string DisplayName, string Extension)> FileTypes = new()
    {
        ("Markdown Document", ".md"),
        ("JavaScript File", ".js"),
        ("TypeScript File", ".ts"),
        ("React JSX File", ".jsx"),
        ("React TSX File", ".tsx"),
        ("PowerShell Script", ".ps1"),
        ("HTML File", ".html"),
        ("CSS File", ".css"),
        ("JSON File", ".json"),
        ("Text File", ".txt"),
        ("Python File", ".py"),
        ("C# File", ".cs"),
        ("XML File", ".xml"),
        ("YAML File", ".yaml"),
        ("SQL File", ".sql"),
        ("Batch File", ".bat"),
    };

    private static readonly string[] RegistryPaths = new[]
    {
        @"Directory\shell",
        @"Directory\Background\shell",
        @"*\shell",
    };

    public NewSubmenuService(ILoggingService logger)
    {
        _logger = logger;
    }

    public bool IsSubmenuEnabled()
    {
        try
        {
            using var shellKey = Registry.ClassesRoot.OpenSubKey(@"Directory\shell\" + NewSubmenuName);
            return shellKey != null;
        }
        catch
        {
            return false;
        }
    }

    public bool EnableSubmenu()
    {
        try
        {
            foreach (var basePath in RegistryPaths)
            {
                CreateSubmenuForPath(basePath);
            }

            _logger.LogInfo("New submenu enabled for all contexts");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError("Access denied. Administrator privileges required.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error enabling New submenu: {ex.Message}");
            return false;
        }
    }

    private void CreateSubmenuForPath(string basePath)
    {
        var fullPath = basePath + @"\" + NewSubmenuName;

        using var shellKey = Registry.ClassesRoot.CreateSubKey(fullPath);
        if (shellKey == null)
        {
            _logger.LogError($"Failed to create key: {fullPath}");
            return;
        }

        shellKey.SetValue("MUIVerb", "New");
        shellKey.SetValue("Icon", "shell32.dll,127");
        shellKey.SetValue("SubCommands", "");

        using var subKey = shellKey.CreateSubKey("shell");
        if (subKey == null) return;

        int position = 0;
        foreach (var (displayName, extension) in FileTypes)
        {
            var safeName = extension.TrimStart('.').ToLowerInvariant();

            using var fileKey = subKey.CreateSubKey(safeName);
            if (fileKey == null) continue;

            fileKey.SetValue(null, displayName);
            fileKey.SetValue("Icon", GetIconForExtension(extension));
            fileKey.SetValue("Position", $"{(char)('0' + position)}");

            using var commandKey = fileKey.CreateSubKey("command");
            if (commandKey != null)
            {
                var exePath = AppDomain.CurrentDomain.BaseDirectory + "Context Menu Tool.exe";
                var command = $"\"{exePath}\" --create-file \"%V\" \"{extension}\"";
                commandKey.SetValue(null, command);
            }
            position++;
        }
    }

    public bool DisableSubmenu()
    {
        try
        {
            foreach (var basePath in RegistryPaths)
            {
                DeleteSubmenuForPath(basePath);
            }

            DeleteShellNewEntries();

            _logger.LogInfo("New submenu disabled");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError("Access denied. Administrator privileges required.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error disabling New submenu: {ex.Message}");
            return false;
        }
    }

    private void DeleteSubmenuForPath(string basePath)
    {
        try
        {
            using var shellKey = Registry.ClassesRoot.OpenSubKey(basePath, true);
            if (shellKey != null)
            {
                var subKeys = shellKey.GetSubKeyNames();
                if (subKeys.Contains(NewSubmenuName))
                {
                    shellKey.DeleteSubKeyTree(NewSubmenuName, false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error deleting submenu for {basePath}: {ex.Message}");
        }
    }

    private void DeleteShellNewEntries()
    {
        foreach (var (_, extension) in FileTypes)
        {
            var extKey = extension.ToLowerInvariant();
            try
            {
                using var extRootKey = Registry.ClassesRoot.OpenSubKey(extKey, true);
                if (extRootKey != null)
                {
                    var subKeys = extRootKey.GetSubKeyNames();
                    if (subKeys.Contains("ShellNew"))
                    {
                        extRootKey.DeleteSubKeyTree("ShellNew", false);
                    }
                }
            }
            catch { }
        }
    }

    private string GetIconForExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".md" => "wordpad.exe,0",
            ".js" or ".ts" or ".jsx" or ".tsx" => "jscript.dll,0",
            ".ps1" => "powershell.exe,0",
            ".html" or ".htm" => "hh.exe,0",
            ".css" => "cssfile.dll,0",
            ".json" => "jsfile.dll,0",
            ".txt" => "notepad.exe,0",
            ".py" => "py.exe,0",
            ".cs" => "csc.exe,0",
            ".xml" => "xmlfile.dll,0",
            ".yaml" or ".yml" => "shell32.dll,0",
            ".sql" => "sqlw.exe,0",
            ".bat" or ".cmd" => "cmd.exe,0",
            _ => "shell32.dll,0"
        };
    }
}
