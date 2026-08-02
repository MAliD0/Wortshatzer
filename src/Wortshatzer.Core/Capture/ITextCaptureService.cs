namespace Wortshatzer.Core.Capture;

public interface ITextCaptureService : IDisposable
{
    event EventHandler<TextCapturedEventArgs>? TextCaptured;

    bool IsRunning { get; }

    void Start();

    void Stop();

    Task<bool> CaptureCurrentAsync(
        TextCaptureSource source,
        CancellationToken cancellationToken = default);
}
