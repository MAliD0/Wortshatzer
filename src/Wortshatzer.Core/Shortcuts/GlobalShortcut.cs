namespace Wortshatzer.Core.Shortcuts;

[Flags]
public enum ShortcutModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

public enum ShortcutKey
{
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12
}

public enum GlobalShortcutAction
{
    CaptureClipboard,
    CaptureOcrRegion,
    SaveLatestTranslation
}

public sealed record GlobalShortcutGesture
{
    private const ShortcutModifiers SupportedModifiers =
        ShortcutModifiers.Alt
        | ShortcutModifiers.Control
        | ShortcutModifiers.Shift
        | ShortcutModifiers.Windows;

    public ShortcutModifiers Modifiers { get; }

    public ShortcutKey Key { get; }

    public GlobalShortcutGesture(
        ShortcutModifiers modifiers,
        ShortcutKey key)
    {
        if ((modifiers & ~SupportedModifiers) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modifiers),
                "The shortcut contains unsupported modifiers.");
        }

        if (!Enum.IsDefined(key))
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }

        Modifiers = modifiers;
        Key = key;
    }

    public override string ToString()
    {
        var parts = new List<string>();

        if (Modifiers.HasFlag(ShortcutModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(ShortcutModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(ShortcutModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(ShortcutModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(Key.ToString());
        return string.Join(" + ", parts);
    }
}

public sealed record GlobalShortcutRegistration
{
    public GlobalShortcutAction Action { get; }

    public GlobalShortcutGesture Gesture { get; }

    public GlobalShortcutRegistration(
        GlobalShortcutAction action,
        GlobalShortcutGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        Action = action;
        Gesture = gesture;
    }
}
