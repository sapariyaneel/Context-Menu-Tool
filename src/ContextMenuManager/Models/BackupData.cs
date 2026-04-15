using System.Text.Json.Serialization;

namespace ContextMenuManager.Models;

public class BackupData
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("exportDate")]
    public DateTime ExportDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("entries")]
    public List<BackupEntry> Entries { get; set; } = new();
}

public class BackupEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("command")]
    public string Command { get; set; } = string.Empty;

    [JsonPropertyName("iconPath")]
    public string IconPath { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;
}
