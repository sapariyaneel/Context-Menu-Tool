using CommunityToolkit.Mvvm.ComponentModel;

namespace ContextMenuManager.Models;

public partial class ContextMenuEntry : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _command = string.Empty;

    [ObservableProperty]
    private string _iconPath = string.Empty;

    [ObservableProperty]
    private ContextMenuType _type;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private string _registryPath = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isLoading;

    public string CommandPreview => Command.Length > 50 
        ? Command.Substring(0, 47) + "..." 
        : Command;

    public string IconDisplay => string.IsNullOrEmpty(IconPath) ? "No Icon" : IconPath;

    public ContextMenuEntry Clone()
    {
        return new ContextMenuEntry
        {
            Name = Name,
            DisplayName = DisplayName,
            Command = Command,
            IconPath = IconPath,
            Type = Type,
            IsEnabled = IsEnabled,
            RegistryPath = RegistryPath
        };
    }
}
