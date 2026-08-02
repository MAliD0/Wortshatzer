using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Wortshatzer.Core.Ocr;

namespace Wortshatzer.Services;

public sealed class WindowsSnippingToolCaptureService :
    IScreenRegionCaptureService
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;

    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyShift = 0x10;
    private const ushort VirtualKeyMenu = 0x12;
    private const ushort VirtualKeyEscape = 0x1B;
    private const ushort VirtualKeyS = 0x53;
    private const ushort VirtualKeyO = 0x4F;
    private const ushort VirtualKeyLeftWindows = 0x5B;
    private const ushort VirtualKeyRightWindows = 0x5C;

    private static readonly TimeSpan CaptureTimeout =
        TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(100);

    private readonly IClipboard _clipboard;

    public WindowsSnippingToolCaptureService(
        IClipboard clipboard)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        _clipboard = clipboard;
    }

    public async Task<OcrImage?> CaptureRegionAsync(
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new OcrException(
                "Windows Snipping Tool capture is available only on Windows.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var initialClipboardSequence =
            GetClipboardSequenceNumber();

        await WaitForShortcutReleaseAsync(
            cancellationToken);

        LaunchWindowsSnippingTool();

        return await WaitForCapturedImageAsync(
            initialClipboardSequence,
            cancellationToken);
    }

    private async Task<OcrImage?> WaitForCapturedImageAsync(
        uint initialClipboardSequence,
        CancellationToken cancellationToken)
    {
        var deadline =
            DateTimeOffset.UtcNow + CaptureTimeout;
        var clipboardChanged = false;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsKeyPressed(VirtualKeyEscape))
            {
                return null;
            }

            clipboardChanged |=
                GetClipboardSequenceNumber()
                    != initialClipboardSequence;

            if (clipboardChanged)
            {
                var capturedImage =
                    await TryReadClipboardImageAsync();

                if (capturedImage is not null)
                {
                    return capturedImage;
                }
            }

            await Task.Delay(
                PollInterval,
                cancellationToken);
        }

        return null;
    }

    private async Task<OcrImage?> TryReadClipboardImageAsync()
    {
        try
        {
            using var bitmap =
                await _clipboard.TryGetBitmapAsync();

            if (bitmap is null)
            {
                return null;
            }

            await using var stream = new MemoryStream();
            bitmap.Save(
                stream,
                new PngBitmapEncoderOptions());

            return new OcrImage(
                stream.ToArray(),
                "image/png");
        }
        catch
        {
            // The Snipping Tool can update the clipboard in stages.
            // Retry until the image is available or capture times out.
            return null;
        }
    }

    private static async Task WaitForShortcutReleaseAsync(
        CancellationToken cancellationToken)
    {
        var deadline =
            DateTimeOffset.UtcNow
                + TimeSpan.FromSeconds(2);

        while (DateTimeOffset.UtcNow < deadline
            && IsAnyShortcutKeyPressed())
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(50),
                cancellationToken);
        }
    }

    private static bool IsAnyShortcutKeyPressed()
    {
        return IsKeyPressed(VirtualKeyControl)
            || IsKeyPressed(VirtualKeyShift)
            || IsKeyPressed(VirtualKeyMenu)
            || IsKeyPressed(VirtualKeyO)
            || IsKeyPressed(VirtualKeyLeftWindows)
            || IsKeyPressed(VirtualKeyRightWindows);
    }

    private static bool IsKeyPressed(ushort virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000)
            != 0;
    }

    private static void LaunchWindowsSnippingTool()
    {
        Input[] inputs =
        [
            CreateKeyboardInput(
                VirtualKeyLeftWindows,
                isKeyUp: false),
            CreateKeyboardInput(
                VirtualKeyShift,
                isKeyUp: false),
            CreateKeyboardInput(
                VirtualKeyS,
                isKeyUp: false),
            CreateKeyboardInput(
                VirtualKeyS,
                isKeyUp: true),
            CreateKeyboardInput(
                VirtualKeyShift,
                isKeyUp: true),
            CreateKeyboardInput(
                VirtualKeyLeftWindows,
                isKeyUp: true)
        ];

        var sent = SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<Input>());

        if (sent == inputs.Length)
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();

        throw new OcrException(
            "Windows Snipping Tool could not be opened.",
            new Win32Exception(error));
    }

    private static Input CreateKeyboardInput(
        ushort virtualKey,
        bool isKeyUp)
    {
        return new Input
        {
            Type = InputKeyboard,
            Value = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = isKeyUp
                        ? KeyEventKeyUp
                        : 0
                }
            }
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Value;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;

        [FieldOffset(0)]
        public HardwareInput Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HardwareInput
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint inputCount,
        [In] Input[] inputs,
        int inputSize);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(
        int virtualKey);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
