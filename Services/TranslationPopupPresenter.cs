using Avalonia;
using Avalonia.Threading;
using Wortshatzer.Core.Dictionary;
using Wortshatzer.Core.Words;
using Wortshatzer.ViewModels;
using Wortshatzer.Views;

namespace Wortshatzer.Services;

public sealed class TranslationPopupPresenter : IDisposable
{
    private const double CompactHeight = 300;
    private const double DictionaryHeight = 440;

    private static readonly TimeSpan DisplayDuration =
        TimeSpan.FromSeconds(5);

    private readonly TranslationPopupViewModel _viewModel;
    private TranslationPopupWindow? _window;
    private CancellationTokenSource? _dismissCancellation;
    private bool _isAlwaysVisible;
    private bool _isDisposed;

    public event Action? AlwaysVisibleDisableRequested;

    public TranslationPopupPresenter(
        Func<
            string,
            CancellationToken,
            Task<WordTranslation>> translateAsync)
    {
        ArgumentNullException.ThrowIfNull(translateAsync);

        _viewModel =
            new TranslationPopupViewModel(translateAsync);
    }

    public void SetAlwaysVisible(bool isAlwaysVisible)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _isAlwaysVisible = isAlwaysVisible;
        CancelDismissTimer();

        if (!isAlwaysVisible)
        {
            _window?.Hide();
            return;
        }

        var window = EnsureWindow();
        window.Height = _viewModel.HasDictionaryDetails
            ? DictionaryHeight
            : CompactHeight;

        if (!window.IsVisible)
        {
            window.Show();
        }

        PositionNearWorkingAreaCorner(window);
    }

    public void Show(WordTranslation translation)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(translation);

        var window = EnsureWindow();
        _viewModel.ApplyTranslation(translation);
        window.Height = CompactHeight;

        if (!window.IsVisible)
        {
            window.Show();
        }

        PositionNearWorkingAreaCorner(window);
        RestartDismissTimer(window);
    }

    public void ShowDictionary(
        DictionaryLookupResult result)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(result);

        if (_window is null
            || !_window.IsVisible
            || !string.Equals(
                _viewModel.SourceText,
                result.Query,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _viewModel.ApplyDictionaryResult(result);

        if (!_viewModel.HasDictionaryDetails)
        {
            return;
        }

        _window.Height = DictionaryHeight;
        PositionNearWorkingAreaCorner(_window);
        RestartDismissTimer(_window);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        CancelDismissTimer();

        if (_window is not null)
        {
            _window.DismissRequested -=
                OnDismissRequested;
            _window.Close();
        }

        _window = null;
        _isDisposed = true;
    }

    private TranslationPopupWindow EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        _window = new TranslationPopupWindow
        {
            DataContext = _viewModel
        };
        _window.DismissRequested += OnDismissRequested;

        return _window;
    }

    private void OnDismissRequested()
    {
        if (_isAlwaysVisible)
        {
            AlwaysVisibleDisableRequested?.Invoke();
        }

        SetAlwaysVisible(false);
    }

    private void RestartDismissTimer(
        TranslationPopupWindow window)
    {
        CancelDismissTimer();

        if (_isAlwaysVisible)
        {
            return;
        }

        _dismissCancellation = new CancellationTokenSource();

        _ = HideAfterDelayAsync(
            window,
            _dismissCancellation.Token);
    }

    private void CancelDismissTimer()
    {
        _dismissCancellation?.Cancel();
        _dismissCancellation?.Dispose();
        _dismissCancellation = null;
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
