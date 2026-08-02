namespace Wortshatzer.Core.Ocr;

public sealed record OcrImage
{
    public ReadOnlyMemory<byte> Data { get; }

    public string MediaType { get; }

    public OcrImage(
        ReadOnlyMemory<byte> data,
        string mediaType)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException(
                "OCR image data cannot be empty.",
                nameof(data));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

        Data = data;
        MediaType = mediaType.Trim();
    }
}

public sealed record OcrResult
{
    public string Text { get; }

    public string LanguageCode { get; }

    public double? Confidence { get; }

    public OcrResult(
        string text,
        string languageCode,
        double? confidence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        if (confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(confidence),
                "OCR confidence must be between zero and one.");
        }

        Text = text.Trim();
        LanguageCode = languageCode.Trim().ToLowerInvariant();
        Confidence = confidence;
    }
}

public sealed class OcrException : Exception
{
    public OcrException(string message)
        : base(message)
    {
    }

    public OcrException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}

public interface ITextRecognitionService
{
    string ProviderName { get; }

    IReadOnlyCollection<string> SupportedLanguageCodes { get; }

    Task<OcrResult> RecognizeAsync(
        OcrImage image,
        string languageCode,
        CancellationToken cancellationToken = default);
}

public interface IClipboardOcrCaptureService
{
    Task<OcrResult?> RecognizeCurrentImageAsync(
        string languageCode,
        CancellationToken cancellationToken = default);
}

public interface IScreenRegionCaptureService
{
    Task<OcrImage?> CaptureRegionAsync(
        CancellationToken cancellationToken = default);
}
