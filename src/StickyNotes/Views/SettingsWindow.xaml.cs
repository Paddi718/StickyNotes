using System.Windows;

namespace StickyNotes.Views;

/// <summary>
/// Code-behind for the settings window. Only sets the DataContext.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(ViewModels.SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
