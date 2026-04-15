using System.IO;
using ContextMenuManager.Models;

namespace ContextMenuManager.Services;

public interface IContextMenuService
{
    Task<List<ContextMenuEntry>> GetEntriesAsync(ContextMenuType type);
    Task<bool> AddEntryAsync(ContextMenuEntry entry);
    Task<bool> UpdateEntryAsync(ContextMenuEntry original, ContextMenuEntry updated);
    Task<bool> DeleteEntryAsync(ContextMenuEntry entry);
    Task<bool> ToggleEntryAsync(ContextMenuEntry entry);
    bool ValidateCommand(string command);
    bool ValidateIconPath(string iconPath);
}

public class ContextMenuService : IContextMenuService
{
    private readonly IRegistryService _registryService;
    private readonly ILoggingService _logger;

    public ContextMenuService(IRegistryService registryService, ILoggingService logger)
    {
        _registryService = registryService;
        _logger = logger;
    }

    public Task<List<ContextMenuEntry>> GetEntriesAsync(ContextMenuType type)
    {
        return Task.Run(() =>
        {
            _logger.LogInfo($"Loading context menu entries for type: {type}");
            var entries = _registryService.GetEntries(type);
            _logger.LogInfo($"Found {entries.Count} entries");
            return entries;
        });
    }

    public Task<bool> AddEntryAsync(ContextMenuEntry entry)
    {
        return Task.Run(() =>
        {
            if (!ValidateEntry(entry))
            {
                return false;
            }

            _logger.LogInfo($"Adding context menu entry: {entry.Name}");
            return _registryService.CreateEntry(entry);
        });
    }

    public Task<bool> UpdateEntryAsync(ContextMenuEntry original, ContextMenuEntry updated)
    {
        return Task.Run(() =>
        {
            if (!ValidateEntry(updated))
            {
                return false;
            }

            _logger.LogInfo($"Updating context menu entry: {original.Name} -> {updated.Name}");
            return _registryService.UpdateEntry(original, updated);
        });
    }

    public Task<bool> DeleteEntryAsync(ContextMenuEntry entry)
    {
        return Task.Run(() =>
        {
            _logger.LogInfo($"Deleting context menu entry: {entry.Name}");
            return _registryService.DeleteEntry(entry);
        });
    }

    public Task<bool> ToggleEntryAsync(ContextMenuEntry entry)
    {
        return Task.Run(() =>
        {
            _logger.LogInfo($"Toggling context menu entry: {entry.Name}");
            return _registryService.ToggleEntry(entry);
        });
    }

    public bool ValidateCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            _logger.LogWarning("Command is empty");
            return false;
        }

        var commandPart = command.Trim().Split(' ')[0].Trim('"');
        
        if (File.Exists(commandPart))
        {
            return true;
        }

        if (commandPart.StartsWith("%") || commandPart.StartsWith("$"))
        {
            return true;
        }

        var systemPaths = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? Array.Empty<string>();
        foreach (var path in systemPaths)
        {
            var fullPath = Path.Combine(path.Trim(), commandPart);
            if (File.Exists(fullPath))
            {
                return true;
            }
        }

        _logger.LogWarning($"Command validation failed: {commandPart} not found");
        return false;
    }

    public bool ValidateIconPath(string iconPath)
    {
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return true;
        }

        var iconPart = iconPath.Split(',')[0].Trim('"');
        
        if (File.Exists(iconPart))
        {
            return true;
        }

        var systemPaths = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? Array.Empty<string>();
        foreach (var path in systemPaths)
        {
            var fullPath = Path.Combine(path.Trim(), iconPart);
            if (File.Exists(fullPath))
            {
                return true;
            }
        }

        _logger.LogWarning($"Icon path validation failed: {iconPart} not found");
        return false;
    }

    private bool ValidateEntry(ContextMenuEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            _logger.LogWarning("Entry name is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.DisplayName))
        {
            _logger.LogWarning("Entry display name is required");
            return false;
        }

        if (string.IsNullOrWhiteSpace(entry.Command))
        {
            _logger.LogWarning("Entry command is required");
            return false;
        }

        if (entry.Name.Contains(@"\") || entry.Name.Contains("/") || entry.Name.Contains(";"))
        {
            _logger.LogWarning($"Entry name contains invalid characters: {entry.Name}");
            return false;
        }

        return true;
    }
}
