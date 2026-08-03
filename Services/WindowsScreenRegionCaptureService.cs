using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Wortshatzer.Core.Ocr;
using Wortshatzer.Views;

namespace Wortshatzer.Services;

public sealed class WindowsScreenRegionCaptureService :
    IScreenRegionCaptureService
{
    private const uint SourceCopy = 0x00CC0020;
    private const uint CaptureLayeredWindows = 0x40000000;
    private const uint RgbColors = 0;

    private readonly Window _owner;

    public WindowsScreenRegionCaptureService(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    public async Task<OcrImage?> CaptureRegionAsync(
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new OcrException(
                "Screen-region OCR is currently available on Windows.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var screens = _owner.Screens;
        var screen = screens.ScreenFromWindow(_owner)
            ?? screens.Primary
            ?? throw new OcrException(
                "No screen is available for OCR capture.");

        var selector = new ScreenRegionSelectionWindow(screen);
        var region = await selector.SelectAsync(cancellationToken);

        if (region is null)
        {
            return null;
        }

        // Let the topmost selection overlay disappear before BitBlt.
        await Task.Delay(
            TimeSpan.FromMilliseconds(100),
            cancellationToken);

        try
        {
            return Capture(region.Value);
        }
        catch (OcrException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new OcrException(
                "The selected screen region could not be captured.",
                exception);
        }
    }

    private static OcrImage Capture(PixelRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new OcrException(
                "Select a larger screen region for OCR.");
        }

        var screenDc = GetDC(IntPtr.Zero);

        if (screenDc == IntPtr.Zero)
        {
            throw LastNativeError(
                "Windows could not access the screen.");
        }

        var memoryDc = IntPtr.Zero;
        var bitmapHandle = IntPtr.Zero;
        var previousObject = IntPtr.Zero;

        try
        {
            memoryDc = CreateCompatibleDC(screenDc);

            if (memoryDc == IntPtr.Zero)
            {
                throw LastNativeError(
                    "Windows could not create a capture surface.");
            }

            bitmapHandle = CreateCompatibleBitmap(
                screenDc,
                region.Width,
                region.Height);

            if (bitmapHandle == IntPtr.Zero)
            {
                throw LastNativeError(
                    "Windows could not allocate the captured image.");
            }

            previousObject = SelectObject(
                memoryDc,
                bitmapHandle);

            if (previousObject == IntPtr.Zero)
            {
                throw LastNativeError(
                    "Windows could not prepare the captured image.");
            }

            if (!BitBlt(
                    memoryDc,
                    0,
                    0,
                    region.Width,
                    region.Height,
                    screenDc,
                    region.X,
                    region.Y,
                    SourceCopy | CaptureLayeredWindows))
            {
                throw LastNativeError(
                    "Windows could not copy the selected screen region.");
            }

            var pixels = new byte[
                checked(region.Width * region.Height * 4)];
            var bitmapInfo = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = region.Width,
                    Height = -region.Height,
                    Planes = 1,
                    BitCount = 32,
                    Compression = 0,
                    ImageSize = (uint)pixels.Length
                }
            };

            var copiedRows = GetDIBits(
                memoryDc,
                bitmapHandle,
                0,
                (uint)region.Height,
                pixels,
                ref bitmapInfo,
                RgbColors);

            if (copiedRows != region.Height)
            {
                throw LastNativeError(
                    "Windows could not read the captured image.");
            }

            using var bitmap = new WriteableBitmap(
                new PixelSize(region.Width, region.Height),
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Opaque);

            using (var frameBuffer = bitmap.Lock())
            {
                var sourceStride = region.Width * 4;

                for (var row = 0; row < region.Height; row++)
                {
                    Marshal.Copy(
                        pixels,
                        row * sourceStride,
                        IntPtr.Add(
                            frameBuffer.Address,
                            row * frameBuffer.RowBytes),
                        sourceStride);
                }
            }

            using var stream = new MemoryStream();
            bitmap.Save(
                stream,
                new PngBitmapEncoderOptions());

            return new OcrImage(
                stream.ToArray(),
                "image/png");
        }
        finally
        {
            if (previousObject != IntPtr.Zero
                && memoryDc != IntPtr.Zero)
            {
                SelectObject(memoryDc, previousObject);
            }

            if (bitmapHandle != IntPtr.Zero)
            {
                DeleteObject(bitmapHandle);
            }

            if (memoryDc != IntPtr.Zero)
            {
                DeleteDC(memoryDc);
            }

            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static OcrException LastNativeError(string message)
    {
        var error = Marshal.GetLastWin32Error();

        return new OcrException(
            message,
            new Win32Exception(error));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint ImageSize;
        public int XPixelsPerMeter;
        public int YPixelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetDC(IntPtr windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int ReleaseDC(
        IntPtr windowHandle,
        IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(
        IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteDC(
        IntPtr deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleBitmap(
        IntPtr deviceContext,
        int width,
        int height);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(
        IntPtr deviceContext,
        IntPtr graphicsObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool DeleteObject(
        IntPtr graphicsObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern bool BitBlt(
        IntPtr destinationDeviceContext,
        int destinationX,
        int destinationY,
        int width,
        int height,
        IntPtr sourceDeviceContext,
        int sourceX,
        int sourceY,
        uint rasterOperation);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetDIBits(
        IntPtr deviceContext,
        IntPtr bitmap,
        uint startScan,
        uint scanLineCount,
        [Out] byte[] bits,
        ref BitmapInfo bitmapInfo,
        uint usage);
}
