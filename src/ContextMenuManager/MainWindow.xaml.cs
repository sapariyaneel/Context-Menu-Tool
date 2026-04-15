using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using ContextMenuManager.Models;
using ContextMenuManager.Services;

namespace ContextMenuManager;

public class BoolToBrushConverter : IValueConverter
{
    public SolidColorBrush TrueBrush { get; set; } = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 125, 70));
    public SolidColorBrush FalseBrush { get; set; } = new SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102));
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? TrueBrush : FalseBrush;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Inverse { get; set; }
    
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var result = value is bool b && b;
        if (Inverse) result = !result;
        return result ? Visibility.Visible : Visibility.Collapsed;
    }
    
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public partial class MainWindow : Window
{
    private readonly IRegistryService _registryService;
    private readonly IInstalledAppsService _installedAppsService;
    private readonly ILoggingService _logger;
    private readonly NewSubmenuService _newSubmenuService;
    private readonly OpenInCmdService _openInCmdService;

    private List<AppDisplayItem> _allApps = new();
    private List<AppDisplayItem> _filteredApps = new();
    private string _selectedContextType = "Everywhere";
    private bool _isListView = false;
    private Dictionary<string, string> _addedApps = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow()
    {
        InitializeComponent();

        _logger = App.Services.GetRequiredService<ILoggingService>();
        _registryService = App.Services.GetRequiredService<IRegistryService>();
        _installedAppsService = App.Services.GetRequiredService<IInstalledAppsService>();
        _newSubmenuService = App.Services.GetRequiredService<NewSubmenuService>();
        _openInCmdService = App.Services.GetRequiredService<OpenInCmdService>();

        NewMenuToggle.IsChecked = _newSubmenuService.IsSubmenuEnabled();
        OpenInCmdToggle.IsChecked = _openInCmdService.IsEnabled();

        LoadApps();
        
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => LoadIconsAsync());
    }

    private void NewMenuToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (NewMenuToggle.IsChecked == true)
            {
                if (_newSubmenuService.EnableSubmenu())
                {
                    ShowDialog("Success", "New menu has been added to the context menu!\nRight-click on any folder to see it.", DialogType.Success);
                }
                else
                {
                    NewMenuToggle.IsChecked = false;
                    ShowDialog("Error", "Failed to add New menu.", DialogType.Error);
                }
            }
            else
            {
                if (_newSubmenuService.DisableSubmenu())
                {
                    ShowDialog("Success", "New menu has been removed from the context menu.", DialogType.Success);
                }
                else
                {
                    NewMenuToggle.IsChecked = true;
                    ShowDialog("Error", "Failed to remove New menu.", DialogType.Error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error toggling New menu: {ex.Message}");
            ShowDialog("Error", "Administrator privileges required.", DialogType.Error);
            NewMenuToggle.IsChecked = !NewMenuToggle.IsChecked;
        }
    }

    private void OpenInCmdToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (OpenInCmdToggle.IsChecked == true)
            {
                if (_openInCmdService.Enable())
                {
                    ShowDialog("Success", "Command Prompt submenu has been added!\nRight-click on any folder or desktop to see it.", DialogType.Success);
                }
                else
                {
                    OpenInCmdToggle.IsChecked = false;
                    ShowDialog("Error", "Failed to add Command Prompt.", DialogType.Error);
                }
            }
            else
            {
                if (_openInCmdService.Disable())
                {
                    ShowDialog("Success", "Command Prompt has been removed from the context menu.", DialogType.Success);
                }
                else
                {
                    OpenInCmdToggle.IsChecked = true;
                    ShowDialog("Error", "Failed to remove Command Prompt.", DialogType.Error);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error toggling Command Prompt: {ex.Message}");
            ShowDialog("Error", "Administrator privileges required.", DialogType.Error);
            OpenInCmdToggle.IsChecked = !OpenInCmdToggle.IsChecked;
        }
    }

    private void LoadIconsAsync()
    {
        foreach (var app in _allApps)
        {
            app.LoadIcon();
        }
        
        Dispatcher.Invoke(() =>
        {
            RefreshViews();
        });
    }

    private void LoadApps()
    {
        try
        {
            CheckAddedApps();
            
            var apps = _installedAppsService.GetInstalledApps();
            _allApps = apps
                .Select(app => 
                {
                    var item = new AppDisplayItem(app.DisplayName, app.ExecutablePath);
                    if (_addedApps.TryGetValue(app.ExecutablePath, out var locations))
                    {
                        item.MarkAsAdded(locations);
                    }
                    return item;
                })
                .ToList();
            
            RefreshViews();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading apps: {ex.Message}");
        }
    }

    private void CheckAddedApps()
    {
        _addedApps.Clear();
        
        var types = new[] { ContextMenuType.File, ContextMenuType.Folder, ContextMenuType.Background };
        
        foreach (var type in types)
        {
            var entries = _registryService.GetEntries(type);
            
            foreach (var entry in entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Command))
                {
                    var exePath = ExtractExeFromCommand(entry.Command);
                    if (!string.IsNullOrWhiteSpace(exePath))
                    {
                        var locationName = type.ToString();
                        if (_addedApps.TryGetValue(exePath, out var existing))
                        {
                            if (!existing.Contains(locationName))
                            {
                                _addedApps[exePath] = existing + ", " + locationName;
                            }
                        }
                        else
                        {
                            _addedApps[exePath] = locationName;
                        }
                    }
                }
            }
        }
    }

    private void UpdateAppStatuses()
    {
        foreach (var app in _allApps)
        {
            if (_addedApps.TryGetValue(app.ExecutablePath, out var locations))
            {
                app.MarkAsAdded(locations);
            }
            else
            {
                app.IsAdded = false;
                app.AddedLocations = string.Empty;
            }
        }
    }

    private string? ExtractExeFromCommand(string command)
    {
        try
        {
            command = command.Trim();
            
            if (command.StartsWith("\"") || command.StartsWith("\"\""))
            {
                int startQuoteLen = command.StartsWith("\"\"") ? 2 : 1;
                var remaining = command.Substring(startQuoteLen);
                var endQuote = remaining.IndexOf('"');
                if (endQuote > 0)
                {
                    return remaining.Substring(0, endQuote).Trim();
                }
            }
            
            var spaceIndex = command.IndexOf(' ');
            if (spaceIndex > 0)
            {
                return command.Substring(0, spaceIndex);
            }
            
            return command;
        }
        catch
        {
            return null;
        }
    }

    private void RefreshViews()
    {
        var searchText = SearchBox.Text;
        List<AppDisplayItem> displayApps;
        
        if (string.IsNullOrWhiteSpace(searchText))
        {
            displayApps = new List<AppDisplayItem>(_allApps);
        }
        else
        {
            displayApps = _allApps.Where(a => 
                a.DisplayName.ToLowerInvariant().Contains(searchText.ToLowerInvariant()) ||
                a.ExecutablePath.ToLowerInvariant().Contains(searchText.ToLowerInvariant())
            ).ToList();
        }
        
        AppsListBox.ItemsSource = displayApps;
        AppsGridBox.ItemsSource = displayApps;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text;
        Placeholder.Visibility = string.IsNullOrEmpty(searchText) ? Visibility.Visible : Visibility.Collapsed;
        
        if (string.IsNullOrWhiteSpace(searchText))
        {
            _filteredApps = _allApps.ToList();
        }
        else
        {
            _filteredApps = _allApps
                .Where(a => a.DisplayName.ToLowerInvariant().Contains(searchText.ToLowerInvariant()) ||
                           a.ExecutablePath.ToLowerInvariant().Contains(searchText.ToLowerInvariant()))
                .ToList();
        }
        
        RefreshViews();
    }

    private void ViewToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _isListView = tag == "List";
            ListViewPanel.Visibility = _isListView ? Visibility.Visible : Visibility.Collapsed;
            GridViewPanel.Visibility = _isListView ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        CheckAddedApps();
        UpdateAppStatuses();
        RefreshViews();
    }

    private void ContextType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _selectedContextType = tag;
        }
    }

    private bool _isAdvancedMode = false;

    private void ModeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _isAdvancedMode = tag == "Advanced";
            
            SearchPanel.Visibility = _isAdvancedMode ? Visibility.Collapsed : Visibility.Visible;
            ListViewPanel.Visibility = _isAdvancedMode ? Visibility.Collapsed : (_isListView ? Visibility.Visible : Visibility.Collapsed);
            GridViewPanel.Visibility = _isAdvancedMode ? Visibility.Collapsed : (!_isListView ? Visibility.Visible : Visibility.Collapsed);
            AdvancedPanel.Visibility = _isAdvancedMode ? Visibility.Visible : Visibility.Collapsed;
            
            if (_isAdvancedMode)
            {
                HeaderText.Text = "Manually add context menu entry";
                SelectionCount.Text = "";
                AddBtn.Visibility = Visibility.Collapsed;
                RemoveBtn.Visibility = Visibility.Collapsed;
                AdvAddBtn.Visibility = Visibility.Visible;
            }
            else
            {
                HeaderText.Text = "Select apps to add to context menu";
                SelectionCount.Text = "Use Ctrl+Click to select multiple";
                AddBtn.Visibility = Visibility.Visible;
                RemoveBtn.Visibility = Visibility.Visible;
                AdvAddBtn.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void AdvAddEntry_Click(object sender, RoutedEventArgs e)
    {
        var displayName = AdvDisplayName.Text.Trim();
        
        if (string.IsNullOrWhiteSpace(displayName))
        {
            ShowDialog("Error", "Display Name is required.", DialogType.Error);
            return;
        }

        try
        {
            var types = _selectedContextType == "Everywhere" 
                ? new[] { ContextMenuType.File, ContextMenuType.Folder, ContextMenuType.Background }
                : new[] { (ContextMenuType)Enum.Parse(typeof(ContextMenuType), _selectedContextType) };

            var command = AdvCommand.Text.Trim();
            var iconPath = AdvIconPath.Text.Trim();

            string exePathForMatching = command;
            if (string.IsNullOrWhiteSpace(exePathForMatching) && !string.IsNullOrWhiteSpace(iconPath))
            {
                exePathForMatching = iconPath;
            }

            foreach (var type in types)
            {
                var entryName = SanitizeKeyName(displayName);
                var uniqueName = GetUniqueEntryName(entryName, type);

                string actualCommand;
                if (!string.IsNullOrWhiteSpace(command))
                {
                    actualCommand = $"\"{command}\" \"%1\"";
                }
                else if (!string.IsNullOrWhiteSpace(iconPath))
                {
                    actualCommand = $"\"{iconPath}\" \"%1\"";
                }
                else
                {
                    actualCommand = "explorer.exe \"%V\"";
                }

                var entry = new ContextMenuEntry
                {
                    Name = uniqueName,
                    DisplayName = displayName,
                    Command = actualCommand,
                    IconPath = string.IsNullOrWhiteSpace(iconPath) ? command : iconPath,
                    Type = type,
                    IsEnabled = true
                };

                _registryService.CreateEntry(entry);
            }

            var locationText = _selectedContextType == "Everywhere" ? "File, Folder, and Background" : _selectedContextType;
            ShowDialog("Success", $"'{displayName}' added to {locationText} context menu!", DialogType.Success);

            AdvDisplayName.Text = "";
            AdvCommand.Text = "";
            AdvIconPath.Text = "";

            CheckAddedApps();
            UpdateAppStatuses();
            RefreshViews();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error adding entry: {ex.Message}");
            ShowDialog("Error", $"Error adding entry: {ex.Message}", DialogType.Error);
        }
    }

    private List<AppDisplayItem> GetSelectedApps()
    {
        var listBox = _isListView ? AppsListBox : AppsGridBox;
        return listBox.SelectedItems.Cast<AppDisplayItem>().ToList();
    }

    private void AppsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedApps = GetSelectedApps();
        var count = selectedApps.Count;
        
        if (count > 0)
        {
            SelectionCount.Text = $"{count} app{(count == 1 ? "" : "s")} selected • Ctrl+Click for more";
        }
        else
        {
            SelectionCount.Text = "Use Ctrl+Click to select multiple";
        }
        
        AddBtn.IsEnabled = count > 0;
        RemoveBtn.IsEnabled = count > 0 && selectedApps.Any(a => a.IsAdded);
        
        if (count == 1 && selectedApps[0].IsAdded)
        {
            AddBtn.Content = "Add More";
        }
        else
        {
            AddBtn.Content = "Add Selected";
        }
    }

    private void AddSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedApps = GetSelectedApps();
        if (selectedApps.Count == 0) return;

        var addedCount = 0;
        var skippedCount = 0;

        try
        {
            var types = _selectedContextType == "Everywhere" 
                ? new[] { ContextMenuType.File, ContextMenuType.Folder, ContextMenuType.Background }
                : new[] { (ContextMenuType)Enum.Parse(typeof(ContextMenuType), _selectedContextType) };

            foreach (var selectedApp in selectedApps)
            {
                var entryName = Path.GetFileNameWithoutExtension(selectedApp.ExecutablePath);
                entryName = SanitizeKeyName(entryName);

                var addedLocations = new List<string>();

                foreach (var type in types)
                {
                    if (selectedApp.IsAdded && selectedApp.AddedLocations.Contains(type.ToString()))
                    {
                        continue;
                    }

                    var uniqueName = GetUniqueEntryName(entryName, type);
                    
                    var entry = new ContextMenuEntry
                    {
                        Name = uniqueName,
                        DisplayName = selectedApp.DisplayName,
                        Command = $"\"{selectedApp.ExecutablePath}\" \"%1\"",
                        IconPath = selectedApp.ExecutablePath,
                        Type = type,
                        IsEnabled = true
                    };

                    _registryService.CreateEntry(entry);
                    addedLocations.Add(type.ToString());
                }

                if (addedLocations.Count > 0)
                {
                    addedCount++;
                }
                else
                {
                    skippedCount++;
                }
            }

            CheckAddedApps();
            foreach (var app in _filteredApps)
            {
                if (_addedApps.TryGetValue(app.ExecutablePath, out var locations))
                {
                    app.MarkAsAdded(locations);
                }
            }
            RefreshViews();

            var message = addedCount > 0 
                ? $"Successfully added {addedCount} app{(addedCount == 1 ? "" : "s")} to context menu."
                : "Selected apps are already added to the chosen location.";
            
            ShowDialog("Done", message, DialogType.Success);

            AppsListBox.SelectedItem = null;
            AppsGridBox.SelectedItem = null;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error adding apps: {ex.Message}");
            ShowDialog("Error", $"Error adding apps: {ex.Message}", DialogType.Error);
        }
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedApps = GetSelectedApps();
        if (selectedApps.Count == 0) return;

        var appsToRemove = selectedApps.Where(a => a.IsAdded).ToList();
        if (appsToRemove.Count == 0) return;

        var appNames = string.Join(", ", appsToRemove.Select(a => a.DisplayName));
        
        ShowConfirmDialog("Remove from Context Menu", 
            $"Remove {appsToRemove.Count} app{(appsToRemove.Count == 1 ? "" : "s")} from context menu?\n\n{appNames}", 
            () =>
        {
            try
            {
                var removedCount = 0;

                foreach (var app in appsToRemove)
                {
                    var types = new[] { ContextMenuType.File, ContextMenuType.Folder, ContextMenuType.Background };
                    
                    foreach (var type in types)
                    {
                        var entries = _registryService.GetEntries(type);
                        var matchingEntries = entries.Where(en => 
                            ExtractExeFromCommand(en.Command)?.Equals(app.ExecutablePath, StringComparison.OrdinalIgnoreCase) == true
                        ).ToList();

                        foreach (var entry in matchingEntries)
                        {
                            if (_registryService.DeleteEntry(entry))
                            {
                                removedCount++;
                            }
                        }
                    }
                }

                CheckAddedApps();
                foreach (var app in _filteredApps)
                {
                    if (_addedApps.TryGetValue(app.ExecutablePath, out var locations))
                    {
                        app.MarkAsAdded(locations);
                    }
                    else
                    {
                        app.IsAdded = false;
                        app.AddedLocations = string.Empty;
                    }
                }
                RefreshViews();

                var entryWord = removedCount == 1 ? "entry" : "entries";
                ShowDialog("Done", $"Removed {removedCount} context menu {entryWord}.", DialogType.Success);

                AppsListBox.SelectedItem = null;
                AppsGridBox.SelectedItem = null;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error removing apps: {ex.Message}");
                ShowDialog("Error", $"Error removing apps: {ex.Message}", DialogType.Error);
            }
        });
    }

    private string GetUniqueEntryName(string baseName, ContextMenuType type)
    {
        var name = baseName;
        var counter = 1;
        var existingEntries = _registryService.GetEntries(type);
        var existingNames = existingEntries.Select(en => en.Name.ToLowerInvariant()).ToHashSet();

        while (existingNames.Contains(name.ToLowerInvariant()))
        {
            name = $"{baseName}_{counter++}";
        }

        return name;
    }

    private string SanitizeKeyName(string name)
    {
        var invalidChars = new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
        foreach (var c in invalidChars)
        {
            name = name.Replace(c, '_');
        }
        return name;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeIcon.Text = "☐";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeIcon.Text = "❐";
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Developer_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/sapariyaneel",
                UseShellExecute = true
            });
        }
        catch { }
    }
    
    private void DevLink_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBlock tb)
        {
            tb.TextDecorations = System.Windows.TextDecorations.Underline;
        }
    }
    
    private void DevLink_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBlock tb)
        {
            tb.TextDecorations = null;
        }
    }

    private enum DialogType { Info, Success, Error, Warning, Confirm }

    private void ShowDialog(string title, string message, DialogType type)
    {
        DialogTitle.Text = title;
        DialogMessage.Text = message;

        DialogOkBtn.Visibility = Visibility.Visible;
        DialogCancelBtn.Visibility = Visibility.Collapsed;
        DialogYesBtn.Visibility = Visibility.Collapsed;
        DialogNoBtn.Visibility = Visibility.Collapsed;

        switch (type)
        {
            case DialogType.Success:
                DialogIconBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 125, 70));
                DialogIcon.Text = "✓";
                break;
            case DialogType.Error:
                DialogIconBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 81, 73));
                DialogIcon.Text = "✕";
                break;
            case DialogType.Warning:
                DialogIconBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 185, 0));
                DialogIcon.Text = "⚠";
                break;
            case DialogType.Confirm:
                DialogIconBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                DialogIcon.Text = "?";
                DialogOkBtn.Visibility = Visibility.Collapsed;
                DialogYesBtn.Visibility = Visibility.Visible;
                DialogNoBtn.Visibility = Visibility.Visible;
                break;
            default:
                DialogIconBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
                DialogIcon.Text = "i";
                break;
        }

        DialogOverlay.Visibility = Visibility.Visible;
    }

    private Action? _confirmCallback;

    private void ShowConfirmDialog(string title, string message, Action onConfirm)
    {
        _confirmCallback = onConfirm;
        DialogTitle.Text = title;
        DialogMessage.Text = message;

        DialogOkBtn.Visibility = Visibility.Collapsed;
        DialogCancelBtn.Visibility = Visibility.Collapsed;
        DialogYesBtn.Visibility = Visibility.Visible;
        DialogNoBtn.Visibility = Visibility.Visible;

        DialogIconBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 185, 0));
        DialogIcon.Text = "?";

        DialogOverlay.Visibility = Visibility.Visible;
    }

    private void HideDialog()
    {
        DialogOverlay.Visibility = Visibility.Collapsed;
    }

    private void DialogOk_Click(object sender, RoutedEventArgs e)
    {
        HideDialog();
    }

    private void DialogClose_Click(object sender, RoutedEventArgs e)
    {
        HideDialog();
    }

    private void DialogCancel_Click(object sender, RoutedEventArgs e)
    {
        HideDialog();
    }

    private void DialogYes_Click(object sender, RoutedEventArgs e)
    {
        HideDialog();
        _confirmCallback?.Invoke();
        _confirmCallback = null;
    }

    private void DialogNo_Click(object sender, RoutedEventArgs e)
    {
        HideDialog();
    }
}

public class AppDisplayItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string DisplayName { get; }
    public string ExecutablePath { get; }
    public BitmapSource? Icon { get; private set; }
    
    private bool _isAdded;
    public bool IsAdded
    {
        get => _isAdded;
        set
        {
            if (_isAdded != value)
            {
                _isAdded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAdded)));
            }
        }
    }
    
    private string _addedLocations = string.Empty;
    public string AddedLocations
    {
        get => _addedLocations;
        set
        {
            if (_addedLocations != value)
            {
                _addedLocations = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddedLocations)));
            }
        }
    }

    public AppDisplayItem(string displayName, string executablePath)
    {
        DisplayName = displayName;
        ExecutablePath = executablePath;
    }

    public void MarkAsAdded(string locations)
    {
        IsAdded = true;
        AddedLocations = locations;
    }

    public void LoadIcon()
    {
        if (Icon != null || !File.Exists(ExecutablePath)) return;

        try
        {
            using var ico = System.Drawing.Icon.ExtractAssociatedIcon(ExecutablePath);
            if (ico == null) return;

            using var bitmap = ico.ToBitmap();
            using var memory = new MemoryStream();
            bitmap.Save(memory, ImageFormat.Png);
            memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            Icon = bitmapImage;
        }
        catch
        {
            Icon = null;
        }
    }
}
