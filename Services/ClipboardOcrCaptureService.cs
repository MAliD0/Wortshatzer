using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Wortshatzer.Core.Ocr;

namespace Wortshatzer.Services;

public sealed class ClipboardOcrCaptureService :
    IClipboardOcrCaptureService
{
    private readonly IClipboard _clipboard;
    private readonly ITextRecognitionService _textRecognitionService;

    public ClipboardOcrCaptureService(
        IClipboard clipboard,
        ITextRecognitionService textRecognitionService)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(textRecognitionService);

        _clipboard = clipboard;
        _textRecognitionService = textRecognitionService;
    }

    public async Task<OcrResult?> RecognizeCurrentImageAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var bitmap = await _clipboard.TryGetBitmapAsync();

        if (bitmap is null)
        {
            return null;
        }

        await using var stream = new MemoryStream();
        bitmap.Save(
            stream,
            new PngBitmapEncoderOptions());

        return await _textRecognitionService.RecognizeAsync(
            new OcrImage(stream.ToArray(), "image/png"),
            languageCode,
            cancellationToken);
    }
}
