using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Wortshatzer.Views;

public partial class ScraperSettingsWindow : Window
{
    public ScraperSettingsWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_OnClick(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        Close();
    }
}
