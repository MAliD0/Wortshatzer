using TesseractOCR;
using TesseractOCR.Enums;
using Wortshatzer.Core.Ocr;

namespace Wortshatzer.Infrastructure.Ocr;

public sealed class TesseractTextRecognitionService :
    ITextRecognitionService
{
    private static readonly IReadOnlyDictionary<string, Language>
        Languages = new Dictionary<string, Language>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["de"] = Language.German,
            ["en"] = Language.English,
            ["pl"] = Language.Polish,
            ["ru"] = Language.Russian
        };

    private readonly OcrLanguageDataManager _languageDataManager;

    public string ProviderName => "Tesseract OCR";

    public IReadOnlyCollection<string> SupportedLanguageCodes =>
        Languages.Keys.ToArray();

    public TesseractTextRecognitionService(
        OcrLanguageDataManager languageDataManager)
    {
        ArgumentNullException.ThrowIfNull(languageDataManager);
        _languageDataManager = languageDataManager;
    }

    public async Task<OcrResult> RecognizeAsync(
        OcrImage image,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        if (!Languages.TryGetValue(
                languageCode.Trim(),
                out var language))
        {
            throw new OcrException(
                $"OCR language '{languageCode}' is not supported.");
        }

        await _languageDataManager.EnsureLanguageAsync(
            languageCode,
            cancellationToken);

        var temporaryImagePath = Path.Combine(
            Path.GetTempPath(),
            $"wortshatzer-ocr-{Guid.NewGuid():N}{GetFileExtension(image.MediaType)}");

        try
        {
            await File.WriteAllBytesAsync(
                temporaryImagePath,
                image.Data.ToArray(),
                cancellationToken);

            return await Task.Run(
                () => RecognizeFile(
                    temporaryImagePath,
                    languageCode,
                    language,
                    cancellationToken),
                cancellationToken);
        }
        finally
        {
            TryDelete(temporaryImagePath);
        }
    }

    private OcrResult RecognizeFile(
        string imagePath,
        string languageCode,
        Language language,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var engine = new Engine(
                _languageDataManager.DataDirectory,
                language,
                EngineMode.Default);
            using var image =
                TesseractOCR.Pix.Image.LoadFromFile(imagePath);
            using var page = engine.Process(image);

            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(page.Text))
            {
                throw new OcrException(
                    "No readable text was found in the image.");
            }

            var confidence = Math.Clamp(
                Convert.ToDouble(page.MeanConfidence),
                0,
                1);

            return new OcrResult(
                page.Text,
                languageCode,
                confidence);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OcrException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new OcrException(
                "OCR could not process the selected image.",
                exception);
        }
    }

    private static string GetFileExtension(
        string mediaType)
    {
        return mediaType.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/bmp" => ".bmp",
            "image/tiff" => ".tiff",
            "image/webp" => ".webp",
            _ => ".png"
        };
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temporary OCR images are cleaned up on a best-effort basis.
        }
    }
}
