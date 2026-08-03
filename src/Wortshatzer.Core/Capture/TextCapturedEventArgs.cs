namespace Wortshatzer.Core.Capture;

public enum TextCaptureSource
{
    ClipboardMonitor,
    GlobalShortcut,
    Ocr
}

public sealed class TextCapturedEventArgs : EventArgs
{
    public string Text { get; }

    public TextCaptureSource Source { get; }

    public TextCapturedEventArgs(
        string text,
        TextCaptureSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (!Enum.IsDefined(source))
        {
            throw new ArgumentOutOfRangeException(nameof(source));
        }

        Text = text;
        Source = source;
    }
}
