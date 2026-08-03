using Avalonia.Input.Platform;
using Avalonia.Threading;
using Wortshatzer.Core.Capture;

namespace Wortshatzer.Services;

public sealed class ClipboardCaptureService : ITextCaptureService
{
    private readonly IClipboard _clipboard;
    private readonly DispatcherTimer _timer;
    private string? _lastClipboardText;
    private bool _hasBaseline;
    private bool _isReading;
    private bool _isDisposed;

    public event EventHandler<TextCapturedEventArgs>? TextCaptured;

    public bool IsRunning => _timer.IsEnabled;

    public ClipboardCaptureService(
        IClipboard clipboard,
        TimeSpan? pollingInterval = null)
    {
        ArgumentNullException.ThrowIfNull(clipboard);

        _clipboard = clipboard;
        _timer = new DispatcherTimer
        {
            Interval = pollingInterval ?? TimeSpan.FromMilliseconds(650)
        };
        _timer.Tick += OnTimerTick;
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (IsRunning)
        {
            return;
        }

        _hasBaseline = false;
        _timer.Start();
    }

    public void Stop()
    {
        if (_isDisposed)
        {
            return;
        }

        _timer.Stop();
        _hasBaseline = false;
    }

    public async Task<bool> CaptureCurrentAsync(
        TextCaptureSource source,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        if (_isReading)
        {
            return false;
        }

        _isReading = true;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clipboardText = await _clipboard.TryGetTextAsync();
            cancellationToken.ThrowIfCancellationRequested();

            _lastClipboardText = clipboardText;
            _hasBaseline = true;

            return PublishCapturedText(clipboardText, source);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
        finally
        {
            _isReading = false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _isDisposed = true;
    }

    private async void OnTimerTick(object? sender, EventArgs eventArgs)
    {
        if (_isReading)
        {
            return;
        }

        _isReading = true;

        try
        {
            var clipboardText = await _clipboard.TryGetTextAsync();

            if (!_hasBaseline)
            {
                _lastClipboardText = clipboardText;
                _hasBaseline = true;
                return;
            }

            if (string.Equals(
                    clipboardText,
                    _lastClipboardText,
                    StringComparison.Ordinal))
            {
                return;
            }

            _lastClipboardText = clipboardText;
            PublishCapturedText(
                clipboardText,
                TextCaptureSource.ClipboardMonitor);
        }
        catch
        {
            // Clipboard access can fail temporarily when another application
            // owns it. The next polling interval tries again.
        }
        finally
        {
            _isReading = false;
        }
    }

    private bool PublishCapturedText(
        string? capturedText,
        TextCaptureSource source)
    {
        if (!CapturedTextNormalizer.TryNormalize(
                capturedText,
                out var normalizedText))
        {
            return false;
        }

        TextCaptured?.Invoke(
            this,
            new TextCapturedEventArgs(normalizedText, source));

        return true;
    }
}
