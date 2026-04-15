using System.Windows;
using System.Windows.Input;
using ContextMenuManager.Models;

namespace ContextMenuManager.UI;

public partial class EntryDialog : Window
{
    public ContextMenuEntry? Entry { get; private set; }

    public EntryDialog(ContextMenuType type)
    {
        InitializeComponent();
        
        Entry = new ContextMenuEntry
        {
            Type = type,
            IsEnabled = true
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayNameBox.Text))
        {
            MessageBox.Show("Display Name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Entry ??= new ContextMenuEntry();
        Entry.Name = NameBox.Text.Trim();
        Entry.DisplayName = DisplayNameBox.Text.Trim();
        Entry.Command = CommandBox.Text?.Trim() ?? "";
        Entry.IconPath = IconPathBox.Text?.Trim() ?? "";

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
