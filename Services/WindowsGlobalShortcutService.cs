using System.ComponentModel;
using System.Runtime.InteropServices;
using Wortshatzer.Core.Shortcuts;

namespace Wortshatzer.Services;

public sealed class WindowsGlobalShortcutService :
    IGlobalShortcutService
{
    private const uint WindowMessageHotKey = 0x0312;
    private const uint WindowMessageQuit = 0x0012;
    private const uint ModifierNoRepeat = 0x4000;

    private readonly object _sync = new();
    private Thread? _messageThread;
    private uint _messageThreadId;
    private IReadOnlyList<GlobalShortcutAction> _startupFailures = [];
    private bool _isDisposed;

    public event EventHandler<GlobalShortcutPressedEventArgs>?
        ShortcutPressed;

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _messageThread is not null;
            }
        }
    }

    public IReadOnlyList<GlobalShortcutAction> Start(
        IEnumerable<GlobalShortcutRegistration> registrations)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(registrations);

        var requested = registrations.ToArray();

        if (requested.Length == 0)
        {
            return [];
        }

        if (requested
            .GroupBy(registration => registration.Action)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Each shortcut action can only be registered once.",
                nameof(registrations));
        }

        if (requested
            .GroupBy(registration => registration.Gesture)
            .Any(group => group.Count() > 1))
        {
            throw new ArgumentException(
                "Each shortcut gesture can only be registered once.",
                nameof(registrations));
        }

        if (!OperatingSystem.IsWindows())
        {
            return requested
                .Select(registration => registration.Action)
                .ToArray();
        }

        lock (_sync)
        {
            if (_messageThread is not null)
            {
                throw new InvalidOperationException(
                    "Global shortcut monitoring is already running.");
            }

            using var started = new ManualResetEventSlim();
            _startupFailures = [];

            _messageThread = new Thread(
                () => RunMessageLoop(requested, started))
            {
                IsBackground = true,
                Name = "Wortshatzer global shortcuts"
            };
            _messageThread.SetApartmentState(ApartmentState.STA);
            _messageThread.Start();

            if (!started.Wait(TimeSpan.FromSeconds(5)))
            {
                _messageThread = null;
                throw new TimeoutException(
                    "The global shortcut service did not start in time.");
            }

            return _startupFailures;
        }
    }

    public void Stop()
    {
        Thread? messageThread;
        uint messageThreadId;

        lock (_sync)
        {
            messageThread = _messageThread;
            messageThreadId = _messageThreadId;
        }

        if (messageThread is null)
        {
            return;
        }

        if (messageThreadId != 0)
        {
            PostThreadMessage(
                messageThreadId,
                WindowMessageQuit,
                UIntPtr.Zero,
                IntPtr.Zero);
        }

        messageThread.Join(TimeSpan.FromSeconds(2));

        lock (_sync)
        {
            _messageThread = null;
            _messageThreadId = 0;
            _startupFailures = [];
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Stop();
        _isDisposed = true;
    }

    private void RunMessageLoop(
        IReadOnlyList<GlobalShortcutRegistration> registrations,
        ManualResetEventSlim started)
    {
        var registeredActions =
            new Dictionary<int, GlobalShortcutAction>();
        var failures = new List<GlobalShortcutAction>();

        try
        {
            _messageThreadId = GetCurrentThreadId();

            for (var index = 0; index < registrations.Count; index++)
            {
                var registration = registrations[index];
                var identifier = index + 1;

                if (RegisterHotKey(
                        IntPtr.Zero,
                        identifier,
                        ToNativeModifiers(registration.Gesture.Modifiers),
                        ToVirtualKey(registration.Gesture.Key)))
                {
                    registeredActions.Add(
                        identifier,
                        registration.Action);
                }
                else
                {
                    failures.Add(registration.Action);
                }
            }

            _startupFailures = failures.ToArray();
            started.Set();

            while (GetMessage(
                       out var message,
                       IntPtr.Zero,
                       0,
                       0) > 0)
            {
                if (message.Message != WindowMessageHotKey)
                {
                    continue;
                }

                var identifier = unchecked(
                    (int)message.WParam.ToUInt64());

                if (registeredActions.TryGetValue(
                        identifier,
                        out var action))
                {
                    ShortcutPressed?.Invoke(
                        this,
                        new GlobalShortcutPressedEventArgs(action));
                }
            }
        }
        finally
        {
            foreach (var identifier in registeredActions.Keys)
            {
                UnregisterHotKey(IntPtr.Zero, identifier);
            }

            started.Set();
        }
    }

    private static uint ToNativeModifiers(
        ShortcutModifiers modifiers)
    {
        return (uint)modifiers | ModifierNoRepeat;
    }

    private static uint ToVirtualKey(ShortcutKey key)
    {
        if (key is >= ShortcutKey.A and <= ShortcutKey.Z)
        {
            return (uint)(
                'A' + ((int)key - (int)ShortcutKey.A));
        }

        if (key is >= ShortcutKey.F1 and <= ShortcutKey.F12)
        {
            return 0x70u
                + (uint)((int)key - (int)ShortcutKey.F1);
        }

        throw new InvalidEnumArgumentException(
            nameof(key),
            (int)key,
            typeof(ShortcutKey));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr WindowHandle;
        public uint Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public NativePoint Point;
        public uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        IntPtr windowHandle,
        int identifier,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(
        IntPtr windowHandle,
        int identifier);

    [DllImport("user32.dll")]
    private static extern int GetMessage(
        out NativeMessage message,
        IntPtr windowHandle,
        uint messageFilterMinimum,
        uint messageFilterMaximum);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostThreadMessage(
        uint threadId,
        uint message,
        UIntPtr wParam,
        IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
