namespace Wortshatzer.Core.Shortcuts;

public interface IGlobalShortcutService : IDisposable
{
    event EventHandler<GlobalShortcutPressedEventArgs>? ShortcutPressed;

    bool IsRunning { get; }

    IReadOnlyList<GlobalShortcutAction> Start(
        IEnumerable<GlobalShortcutRegistration> registrations);

    void Stop();
}

public sealed class GlobalShortcutPressedEventArgs : EventArgs
{
    public GlobalShortcutAction Action { get; }

    public GlobalShortcutPressedEventArgs(
        GlobalShortcutAction action)
    {
        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        Action = action;
    }
}
