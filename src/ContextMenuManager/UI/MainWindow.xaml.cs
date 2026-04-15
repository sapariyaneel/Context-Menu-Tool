using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ContextMenuManager.Models;
using ContextMenuManager.Services;
using Microsoft.Win32;

namespace ContextMenuManager.UI;

public partial class MainWindow : Window
{
    private readonly IContextMenuService _contextMenuService;
    private readonly IBackupService _backupService;
    private readonly ILoggingService _logger;

    private ContextMenuType _currentType = ContextMenuType.File;
    private List<ContextMenuEntry> _allEntries = new();
    private List<ContextMenuEntry> _filteredEntries = new();

    public MainWindow()
    {
        InitializeComponent();

        _contextMenuService = AppServices.GetService<IContextMenuService>();
        _backupService = AppServices.GetService<IBackupService>();
        _logger = AppServices.GetService<ILoggingService>();

        _logger.LogInfo("MainWindow initializing...");
        
        UpdateNavButtons();
    }

    private void UpdateNavButtons()
    {
        FileBtn.Foreground = _currentType == ContextMenuType.File ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,255,255)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(170,170,170));
        FolderBtn.Foreground = _currentType == ContextMenuType.Folder ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,255,255)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(170,170,170));
        BackgroundBtn.Foreground = _currentType == ContextMenuType.Background ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255,255,255)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(170,170,170));

        FileBtn.Background = _currentType == ContextMenuType.File ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45,45,45)) : System.Windows.Media.Brushes.Transparent;
        FolderBtn.Background = _currentType == ContextMenuType.Folder ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45,45,45)) : System.Windows.Media.Brushes.Transparent;
        BackgroundBtn.Background = _currentType == ContextMenuType.Background ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(45,45,45)) : System.Windows.Media.Brushes.Transparent;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadEntriesAsync();
    }

    private async Task LoadEntriesAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        StatusTextBlock.Text = "Loading...";

        try
        {
            _allEntries = await _contextMenuService.GetEntriesAsync(_currentType);
            ApplyFilter();
            StatusTextBlock.Text = $"Found {_allEntries.Count} entries";
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show("Access denied. Please restart as administrator.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusTextBlock.Text = "Access denied";
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error loading entries: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusTextBlock.Text = "Error loading entries";
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyFilter()
    {
        var searchText = SearchBox.Text?.ToLower() ?? "";
        
        _filteredEntries = string.IsNullOrWhiteSpace(searchText)
            ? _allEntries.ToList()
            : _allEntries.Where(e => 
                e.DisplayName.ToLower().Contains(searchText) || 
                e.Command.ToLower().Contains(searchText)).ToList();

        if (!ShowDisabledCheck.IsChecked == true)
        {
            _filteredEntries = _filteredEntries.Where(e => e.IsEnabled).ToList();
        }

        EntriesList.ItemsSource = _filteredEntries;
    }

    private void ShowDisabled_Changed(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double click - no action for now
        }
        else
        {
            DragMove();
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void FileButton_Click(object sender, RoutedEventArgs e)
    {
        _currentType = ContextMenuType.File;
        TitleText.Text = "File Context Menu";
        UpdateNavButtons();
        _ = LoadEntriesAsync();
    }

    private void FolderButton_Click(object sender, RoutedEventArgs e)
    {
        _currentType = ContextMenuType.Folder;
        TitleText.Text = "Folder Context Menu";
        UpdateNavButtons();
        _ = LoadEntriesAsync();
    }

    private void BackgroundButton_Click(object sender, RoutedEventArgs e)
    {
        _currentType = ContextMenuType.Background;
        TitleText.Text = "Directory Background Context Menu";
        UpdateNavButtons();
        _ = LoadEntriesAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void AddNew_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new EntryDialog(_currentType);
        dialog.Owner = this;
        
        if (dialog.ShowDialog() == true && dialog.Entry != null)
        {
            _ = SaveEntryAsync(dialog.Entry);
        }
    }

    private async Task SaveEntryAsync(ContextMenuEntry entry)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        StatusTextBlock.Text = "Saving...";

        try
        {
            var success = await _contextMenuService.AddEntryAsync(entry);
            if (success)
            {
                StatusTextBlock.Text = "Entry added successfully";
                await LoadEntriesAsync();
            }
            else
            {
                StatusTextBlock.Text = "Failed to add entry";
            }
        }
        catch (UnauthorizedAccessException)
        {
            MessageBox.Show("Access denied.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error saving entry: {ex.Message}");
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ContextMenuEntry entry)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            StatusTextBlock.Text = entry.IsEnabled ? "Disabling..." : "Enabling...";

            try
            {
                var success = await _contextMenuService.ToggleEntryAsync(entry);
                if (success)
                {
                    await LoadEntriesAsync();
                    StatusTextBlock.Text = entry.IsEnabled 
                        ? $"'{entry.DisplayName}' disabled" 
                        : $"'{entry.DisplayName}' enabled";
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Access denied.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error toggling entry: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ContextMenuEntry entry)
        {
            var result = MessageBox.Show(
                $"Delete '{entry.DisplayName}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                StatusTextBlock.Text = "Deleting...";

                try
                {
                    var success = await _contextMenuService.DeleteEntryAsync(entry);
                    if (success)
                    {
                        StatusTextBlock.Text = "Entry deleted";
                        await LoadEntriesAsync();
                    }
                    else
                    {
                        StatusTextBlock.Text = "Failed to delete";
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    MessageBox.Show("Access denied.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error deleting entry: {ex.Message}");
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = $"context_menu_backup_{DateTime.Now:yyyyMMdd}"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            StatusTextBlock.Text = "Exporting...";

            try
            {
                var success = await _backupService.ExportToJsonAsync(dialog.FileName, _allEntries);
                StatusTextBlock.Text = success ? "Exported successfully" : "Export failed";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error exporting: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON Files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            StatusTextBlock.Text = "Importing...";

            try
            {
                var (success, entries, error) = await _backupService.ImportFromJsonAsync(dialog.FileName);
                
                if (success)
                {
                    var confirm = MessageBox.Show(
                        $"Import {entries.Count} entries?",
                        "Confirm Import",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (confirm == MessageBoxResult.Yes)
                    {
                        foreach (var entry in entries.Where(e => e.Type == _currentType))
                        {
                            await _contextMenuService.AddEntryAsync(entry);
                        }
                        await LoadEntriesAsync();
                        StatusTextBlock.Text = $"Imported {entries.Count} entries";
                    }
                }
                else
                {
                    MessageBox.Show($"Import failed: {error}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    StatusTextBlock.Text = "Import failed";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error importing: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }
    }
}
