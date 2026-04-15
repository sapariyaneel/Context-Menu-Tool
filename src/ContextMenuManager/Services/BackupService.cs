using System.IO;
using System.Text.Json;
using ContextMenuManager.Models;

namespace ContextMenuManager.Services;

public interface IBackupService
{
    Task<bool> ExportToJsonAsync(string filePath, IEnumerable<ContextMenuEntry> entries);
    Task<(bool Success, List<ContextMenuEntry> Entries, string? Error)> ImportFromJsonAsync(string filePath);
    Task<bool> CreateBackupAsync(string backupFolder);
}

public class BackupService : IBackupService
{
    private readonly ILoggingService _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public BackupService(ILoggingService logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<bool> ExportToJsonAsync(string filePath, IEnumerable<ContextMenuEntry> entries)
    {
        try
        {
            var backupData = new BackupData
            {
                Version = "1.0",
                ExportDate = DateTime.UtcNow,
                Entries = entries.Select(e => new BackupEntry
                {
                    Name = e.Name,
                    DisplayName = e.DisplayName,
                    Command = e.Command,
                    IconPath = e.IconPath,
                    Type = e.Type.ToString(),
                    IsEnabled = e.IsEnabled
                }).ToList()
            };

            var json = JsonSerializer.Serialize(backupData, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            
            _logger.LogInfo($"Exported {entries.Count()} entries to {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error exporting to JSON: {ex.Message}");
            return false;
        }
    }

    public async Task<(bool Success, List<ContextMenuEntry> Entries, string? Error)> ImportFromJsonAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return (false, new List<ContextMenuEntry>(), "File not found");
            }

            var json = await File.ReadAllTextAsync(filePath);
            var backupData = JsonSerializer.Deserialize<BackupData>(json, _jsonOptions);

            if (backupData == null)
            {
                return (false, new List<ContextMenuEntry>(), "Invalid JSON format");
            }

            var entries = new List<ContextMenuEntry>();
            foreach (var backupEntry in backupData.Entries)
            {
                if (!Enum.TryParse<ContextMenuType>(backupEntry.Type, out var type))
                {
                    _logger.LogWarning($"Unknown context menu type: {backupEntry.Type}");
                    continue;
                }

                entries.Add(new ContextMenuEntry
                {
                    Name = backupEntry.Name,
                    DisplayName = backupEntry.DisplayName,
                    Command = backupEntry.Command,
                    IconPath = backupEntry.IconPath,
                    Type = type,
                    IsEnabled = backupEntry.IsEnabled
                });
            }

            _logger.LogInfo($"Imported {entries.Count} entries from {filePath}");
            return (true, entries, null);
        }
        catch (JsonException ex)
        {
            _logger.LogError($"JSON parsing error: {ex.Message}");
            return (false, new List<ContextMenuEntry>(), $"Invalid JSON format: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error importing from JSON: {ex.Message}");
            return (false, new List<ContextMenuEntry>(), ex.Message);
        }
    }

    public async Task<bool> CreateBackupAsync(string backupFolder)
    {
        try
        {
            Directory.CreateDirectory(backupFolder);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFile = Path.Combine(backupFolder, $"backup_{timestamp}.json");

            var registryService = AppServices.GetService<IRegistryService>();
            var allEntries = new List<ContextMenuEntry>();

            foreach (ContextMenuType type in Enum.GetValues<ContextMenuType>())
            {
                var entries = registryService.GetEntries(type);
                allEntries.AddRange(entries);
            }

            return await ExportToJsonAsync(backupFile, allEntries);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error creating backup: {ex.Message}");
            return false;
        }
    }
}
