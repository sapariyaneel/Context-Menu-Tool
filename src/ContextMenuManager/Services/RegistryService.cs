using Microsoft.Win32;
using ContextMenuManager.Models;

namespace ContextMenuManager.Services;

public interface IRegistryService
{
    List<ContextMenuEntry> GetEntries(ContextMenuType type);
    bool CreateEntry(ContextMenuEntry entry);
    bool UpdateEntry(ContextMenuEntry original, ContextMenuEntry updated);
    bool DeleteEntry(ContextMenuEntry entry);
    bool ToggleEntry(ContextMenuEntry entry);
    bool IsValidKeyPath(string path);
}

public class RegistryService : IRegistryService
{
    private readonly ILoggingService _logger;

    private static readonly Dictionary<ContextMenuType, string> RegistryPaths = new()
    {
        { ContextMenuType.File, @"*\shell" },
        { ContextMenuType.Folder, @"Directory\shell" },
        { ContextMenuType.Background, @"Directory\Background\shell" }
    };

    private const string DisabledSuffix = ".disabled";

    public RegistryService(ILoggingService logger)
    {
        _logger = logger;
    }

    public List<ContextMenuEntry> GetEntries(ContextMenuType type)
    {
        var entries = new List<ContextMenuEntry>();
        var basePath = RegistryPaths[type];

        try
        {
            using var shellKey = Registry.ClassesRoot.OpenSubKey(basePath);
            if (shellKey == null)
            {
                _logger.LogInfo($"Registry key not found: {basePath}");
                return entries;
            }

            foreach (var subKeyName in shellKey.GetSubKeyNames())
            {
                try
                {
                    if (subKeyName.EndsWith(DisabledSuffix))
                    {
                        var entryName = subKeyName.Substring(0, subKeyName.Length - DisabledSuffix.Length);
                        var entry = ReadEntry(shellKey, entryName, subKeyName, type, false);
                        if (entry != null)
                            entries.Add(entry);
                    }
                    else
                    {
                        var entry = ReadEntry(shellKey, subKeyName, subKeyName, type, true);
                        if (entry != null)
                            entries.Add(entry);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error reading subkey '{subKeyName}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error accessing registry path '{basePath}': {ex.Message}");
        }

        return entries.OrderBy(e => e.DisplayName).ToList();
    }

    private ContextMenuEntry? ReadEntry(RegistryKey parentKey, string keyName, string fullKeyName, ContextMenuType type, bool isEnabled)
    {
        try
        {
            using var entryKey = parentKey.OpenSubKey(fullKeyName);
            if (entryKey == null) return null;

            var displayName = entryKey.GetValue(null) as string ?? keyName;
            var iconPath = entryKey.GetValue("Icon") as string ?? string.Empty;

            string command = string.Empty;
            using (var commandKey = entryKey.OpenSubKey("command"))
            {
                command = commandKey?.GetValue(null) as string ?? string.Empty;
            }

            var registryPath = $@"{RegistryPaths[type]}\{fullKeyName}";

            return new ContextMenuEntry
            {
                Name = keyName,
                DisplayName = displayName,
                Command = command,
                IconPath = iconPath,
                Type = type,
                IsEnabled = isEnabled,
                RegistryPath = registryPath
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error reading entry '{keyName}': {ex.Message}");
            return null;
        }
    }

    public bool CreateEntry(ContextMenuEntry entry)
    {
        try
        {
            var basePath = RegistryPaths[entry.Type];
            var keyName = GetUniqueKeyName(basePath, entry.Name);

            using var shellKey = Registry.ClassesRoot.CreateSubKey($@"{basePath}\{keyName}");
            if (shellKey == null)
            {
                _logger.LogError($"Failed to create shell key for '{keyName}'");
                return false;
            }

            shellKey.SetValue(null, entry.DisplayName);
            if (!string.IsNullOrWhiteSpace(entry.IconPath))
            {
                shellKey.SetValue("Icon", entry.IconPath);
            }

            using var commandKey = shellKey.CreateSubKey("command");
            if (commandKey == null)
            {
                _logger.LogError($"Failed to create command key for '{keyName}'");
                return false;
            }

            commandKey.SetValue(null, entry.Command);

            if (!entry.IsEnabled)
            {
                DisableEntryInternal(basePath, keyName);
            }

            _logger.LogInfo($"Created context menu entry: {keyName}");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError("Access denied. Administrator privileges required.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating entry: {ex.Message}");
            return false;
        }
    }

    public bool UpdateEntry(ContextMenuEntry original, ContextMenuEntry updated)
    {
        try
        {
            var basePath = RegistryPaths[updated.Type];
            
            if (original.Type != updated.Type || original.Name != updated.Name)
            {
                if (!DeleteEntry(original))
                    return false;
                return CreateEntry(updated);
            }

            using var shellKey = Registry.ClassesRoot.OpenSubKey($@"{basePath}\{original.Name}", true);
            if (shellKey == null)
            {
                _logger.LogError($"Entry '{original.Name}' not found");
                return false;
            }

            shellKey.SetValue(null, updated.DisplayName);
            if (!string.IsNullOrWhiteSpace(updated.IconPath))
            {
                shellKey.SetValue("Icon", updated.IconPath);
            }
            else
            {
                try { shellKey.DeleteValue("Icon", false); } catch { }
            }

            using var commandKey = shellKey.OpenSubKey("command", true);
            if (commandKey != null)
            {
                var currentCommand = commandKey.GetValue(null) as string;
                if (currentCommand != updated.Command)
                {
                    commandKey.SetValue(null, updated.Command);
                }
            }

            _logger.LogInfo($"Updated context menu entry: {updated.Name}");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError("Access denied. Administrator privileges required.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error updating entry: {ex.Message}");
            return false;
        }
    }

    public bool DeleteEntry(ContextMenuEntry entry)
    {
        try
        {
            var basePath = RegistryPaths[entry.Type];
            var keyName = entry.IsEnabled ? entry.Name : entry.Name + DisabledSuffix;

            using var shellKey = Registry.ClassesRoot.OpenSubKey(basePath, true);
            if (shellKey == null)
            {
                _logger.LogError($"Registry path '{basePath}' not found");
                return false;
            }

            shellKey.DeleteSubKeyTree(keyName, false);
            _logger.LogInfo($"Deleted context menu entry: {keyName}");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError("Access denied. Administrator privileges required.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting entry: {ex.Message}");
            return false;
        }
    }

    public bool ToggleEntry(ContextMenuEntry entry)
    {
        try
        {
            var basePath = RegistryPaths[entry.Type];

            if (entry.IsEnabled)
            {
                DisableEntryInternal(basePath, entry.Name);
            }
            else
            {
                EnableEntryInternal(basePath, entry.Name);
            }

            _logger.LogInfo($"Toggled context menu entry: {entry.Name} to {(entry.IsEnabled ? "disabled" : "enabled")}");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogError("Access denied. Administrator privileges required.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error toggling entry: {ex.Message}");
            return false;
        }
    }

    private void DisableEntryInternal(string basePath, string keyName)
    {
        var disabledName = keyName + DisabledSuffix;
        
        using var shellKey = Registry.ClassesRoot.OpenSubKey(basePath, true);
        if (shellKey == null) return;

        if (shellKey.GetSubKeyNames().Contains(disabledName))
        {
            shellKey.DeleteSubKeyTree(disabledName, false);
        }

        RegistryKey? sourceKey = null;
        try
        {
            sourceKey = shellKey.OpenSubKey(keyName, true);
            if (sourceKey != null)
            {
                using var destKey = shellKey.CreateSubKey(disabledName);
                if (destKey != null)
                {
                    CopyKeyValues(sourceKey, destKey);
                }
            }
            shellKey.DeleteSubKeyTree(keyName, false);
        }
        finally
        {
            sourceKey?.Dispose();
        }
    }

    private void EnableEntryInternal(string basePath, string keyName)
    {
        var disabledName = keyName + DisabledSuffix;
        
        using var shellKey = Registry.ClassesRoot.OpenSubKey(basePath, true);
        if (shellKey == null) return;

        if (shellKey.GetSubKeyNames().Contains(keyName))
        {
            shellKey.DeleteSubKeyTree(keyName, false);
        }

        RegistryKey? sourceKey = null;
        try
        {
            sourceKey = shellKey.OpenSubKey(disabledName, true);
            if (sourceKey != null)
            {
                using var destKey = shellKey.CreateSubKey(keyName);
                if (destKey != null)
                {
                    CopyKeyValues(sourceKey, destKey);
                }
            }
            shellKey.DeleteSubKeyTree(disabledName, false);
        }
        finally
        {
            sourceKey?.Dispose();
        }
    }

    private void CopyKeyValues(RegistryKey source, RegistryKey dest)
    {
        foreach (var valueName in source.GetValueNames())
        {
            var value = source.GetValue(valueName);
            var valueKind = source.GetValueKind(valueName);
            dest.SetValue(valueName, value!, valueKind);
        }

        foreach (var subKeyName in source.GetSubKeyNames())
        {
            using var sourceSubKey = source.OpenSubKey(subKeyName);
            if (sourceSubKey != null)
            {
                using var destSubKey = dest.CreateSubKey(subKeyName);
                if (destSubKey != null)
                {
                    CopyKeyValues(sourceSubKey, destSubKey);
                }
            }
        }
    }

    private string GetUniqueKeyName(string basePath, string proposedName)
    {
        var name = proposedName;
        var counter = 1;

        using var shellKey = Registry.ClassesRoot.OpenSubKey(basePath);
        if (shellKey == null) return name;

        var existingNames = shellKey.GetSubKeyNames()
            .Select(n => n.Replace(DisabledSuffix, ""))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        while (existingNames.Contains(name))
        {
            name = $"{proposedName}_{counter++}";
        }

        return name;
    }

    public bool IsValidKeyPath(string path)
    {
        try
        {
            using var key = Registry.ClassesRoot.OpenSubKey(path);
            return key != null;
        }
        catch
        {
            return false;
        }
    }
}
