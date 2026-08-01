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

            if (TryNormalizeCapturedText(clipboardText, out var normalizedText))
            {
                TextCaptured?.Invoke(
                    this,
                    new TextCapturedEventArgs(normalizedText));
            }
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

    private static bool TryNormalizeCapturedText(
        string? clipboardText,
        out string normalizedText)
    {
        normalizedText = string.Empty;

        if (string.IsNullOrWhiteSpace(clipboardText))
        {
            return false;
        }

        var candidate = clipboardText.Trim();

        if (candidate.Length > 64
            || candidate.Contains('\r')
            || candidate.Contains('\n'))
        {
            return false;
        }

        candidate = candidate.Trim(
            ' ', '\t', '.', ',', ';', ':', '!', '?',
            '"', '\'', '“', '”', '„',
            '(', ')', '[', ']', '{', '}');

        if (string.IsNullOrWhiteSpace(candidate)
            || !candidate.Any(char.IsLetter))
        {
            return false;
        }

        var parts = candidate.Split(
            [' ', '\t'],
            StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);

        if (parts.Length is < 1 or > 3)
        {
            return false;
        }

        normalizedText = string.Join(' ', parts);
        return true;
    }
}
