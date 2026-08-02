using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Wortshatzer.Views;

public partial class ScreenRegionSelectionWindow : Window
{
    private readonly Screen _screen;
    private readonly TaskCompletionSource<PixelRect?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Point? _dragStart;

    public ScreenRegionSelectionWindow(Screen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);

        _screen = screen;
        InitializeComponent();

        Position = screen.Bounds.Position;
        Width = screen.Bounds.Width / screen.Scaling;
        Height = screen.Bounds.Height / screen.Scaling;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        KeyDown += OnKeyDown;
        Closed += (_, _) => _completion.TrySetResult(null);
    }

    public async Task<PixelRect?> SelectAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var registration = cancellationToken.Register(
            () => Dispatcher.UIThread.Post(Cancel));

        Show();
        Activate();

        return await _completion.Task;
    }

    private void OnPointerPressed(
        object? sender,
        PointerPressedEventArgs eventArgs)
    {
        var point = eventArgs.GetCurrentPoint(this);

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragStart = Clamp(eventArgs.GetPosition(this));
        eventArgs.Pointer.Capture(this);

        Canvas.SetLeft(SelectionBorder, _dragStart.Value.X);
        Canvas.SetTop(SelectionBorder, _dragStart.Value.Y);
        SelectionBorder.Width = 0;
        SelectionBorder.Height = 0;
        SelectionBorder.IsVisible = true;
        eventArgs.Handled = true;
    }

    private void OnPointerMoved(
        object? sender,
        PointerEventArgs eventArgs)
    {
        if (_dragStart is null)
        {
            return;
        }

        UpdateSelection(Clamp(eventArgs.GetPosition(this)));
        eventArgs.Handled = true;
    }

    private void OnPointerReleased(
        object? sender,
        PointerReleasedEventArgs eventArgs)
    {
        if (_dragStart is null)
        {
            return;
        }

        var end = Clamp(eventArgs.GetPosition(this));
        UpdateSelection(end);
        eventArgs.Pointer.Capture(null);

        var start = _dragStart.Value;
        _dragStart = null;

        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);

        if (width < 8 || height < 8)
        {
            SelectionBorder.IsVisible = false;
            return;
        }

        var scaling = _screen.Scaling;
        var region = new PixelRect(
            _screen.Bounds.X + (int)Math.Round(left * scaling),
            _screen.Bounds.Y + (int)Math.Round(top * scaling),
            Math.Max(1, (int)Math.Round(width * scaling)),
            Math.Max(1, (int)Math.Round(height * scaling)));

        Complete(region);
        eventArgs.Handled = true;
    }

    private void OnKeyDown(
        object? sender,
        KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Escape)
        {
            return;
        }

        Cancel();
        eventArgs.Handled = true;
    }

    private void UpdateSelection(Point end)
    {
        var start = _dragStart!.Value;
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);

        Canvas.SetLeft(SelectionBorder, left);
        Canvas.SetTop(SelectionBorder, top);
        SelectionBorder.Width = Math.Abs(end.X - start.X);
        SelectionBorder.Height = Math.Abs(end.Y - start.Y);
    }

    private Point Clamp(Point point)
    {
        return new Point(
            Math.Clamp(point.X, 0, Width),
            Math.Clamp(point.Y, 0, Height));
    }

    private void Cancel()
    {
        Complete(null);
    }

    private void Complete(PixelRect? region)
    {
        if (!_completion.TrySetResult(region))
        {
            return;
        }

        Close();
    }
}
