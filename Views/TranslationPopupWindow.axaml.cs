using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Wortshatzer.Views;

public partial class TranslationPopupWindow : Window
{
    public event Action? DismissRequested;

    public event Action? DragStarted;

    public TranslationPopupWindow()
    {
        InitializeComponent();
    }

    public void FocusInput()
    {
        Activate();
        InputTextBox.Focus();
        InputTextBox.SelectAll();
    }

    private void DragArea_OnPointerPressed(
        object? sender,
        PointerPressedEventArgs eventArgs)
    {
        var point = eventArgs.GetCurrentPoint(this);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        DragStarted?.Invoke();
        BeginMoveDrag(eventArgs);
        eventArgs.Handled = true;
    }

    private void DismissButton_OnClick(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        DismissRequested?.Invoke();
    }
}
