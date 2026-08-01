namespace Wortshatzer.Core.Capture;

public sealed class TextCapturedEventArgs : EventArgs
{
    public string Text { get; }

    public TextCapturedEventArgs(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Text = text;
    }
}
