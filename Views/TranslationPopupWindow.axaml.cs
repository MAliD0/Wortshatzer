using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Wortshatzer.Views;

public partial class TranslationPopupWindow : Window
{
    public TranslationPopupWindow()
    {
        InitializeComponent();
    }

    private void DismissButton_OnClick(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        Hide();
    }
}
