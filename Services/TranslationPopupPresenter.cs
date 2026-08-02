using Avalonia;
using Avalonia.Threading;
using Wortshatzer.Core.Dictionary;
using Wortshatzer.Core.Words;
using Wortshatzer.ViewModels;
using Wortshatzer.Views;

namespace Wortshatzer.Services;

public sealed class TranslationPopupPresenter : IDisposable
{
    private static readonly TimeSpan DisplayDuration =
        TimeSpan.FromSeconds(5);

    private TranslationPopupWindow? _window;
    private CancellationTokenSource? _dismissCancellation;
    private bool _isDisposed;

    public void Show(WordTranslation translation)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(translation);

        _window ??= new TranslationPopupWindow();
        _window.Height = 190;
        _window.DataContext =
            new TranslationPopupViewModel(translation);

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        PositionNearWorkingAreaCorner(_window);
        RestartDismissTimer(_window);
    }

    public void ShowDictionary(
        DictionaryLookupResult result)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(result);

        if (_window is null
            || !_window.IsVisible
            || _window.DataContext
                is not TranslationPopupViewModel viewModel
            || !string.Equals(
                viewModel.SourceText,
                result.Query,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        viewModel.ApplyDictionaryResult(result);

        if (!viewModel.HasDictionaryDetails)
        {
            return;
        }

        _window.Height = 330;
        PositionNearWorkingAreaCorner(_window);
        RestartDismissTimer(_window);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _dismissCancellation?.Cancel();
        _dismissCancellation?.Dispose();
        _window?.Close();
        _window = null;
        _isDisposed = true;
    }

    private void RestartDismissTimer(
        TranslationPopupWindow window)
    {
        _dismissCancellation?.Cancel();
        _dismissCancellation?.Dispose();
        _dismissCancellation = new CancellationTokenSource();

        _ = HideAfterDelayAsync(
            window,
            _dismissCancellation.Token);
    }

    private static void PositionNearWorkingAreaCorner(
        TranslationPopupWindow window)
    {
        var screen = window.Screens.Primary;

        if (screen is null)
        {
            return;
        }

        const int margin = 24;
        var workingArea = screen.WorkingArea;
        var scaling = window.DesktopScaling;
        var width = (int)Math.Ceiling(window.Width * scaling);
        var height = (int)Math.Ceiling(window.Height * scaling);

        window.Position = new PixelPoint(
            workingArea.X + workingArea.Width - width - margin,
            workingArea.Y + workingArea.Height - height - margin);
    }

    private static async Task HideAfterDelayAsync(
        TranslationPopupWindow window,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(DisplayDuration, cancellationToken);

            Dispatcher.UIThread.Post(() =>
            {
                if (window.IsVisible)
                {
                    window.Hide();
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
    }
}
