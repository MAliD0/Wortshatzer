using Wortshatzer.Core.Ocr;

namespace Wortshatzer.Infrastructure.Ocr;

public sealed class OcrLanguageDataManager
{
    private static readonly Uri LanguageDataBaseUri =
        new("https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/");

    private static readonly IReadOnlyDictionary<string, string>
        LanguageFiles = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ["de"] = "deu.traineddata",
            ["en"] = "eng.traineddata",
            ["pl"] = "pol.traineddata",
            ["ru"] = "rus.traineddata"
        };

    private readonly HttpClient _httpClient;
    private readonly string _dataDirectory;
    private readonly SemaphoreSlim _downloadLock = new(1, 1);

    public string DataDirectory => _dataDirectory;

    public IReadOnlyCollection<string> SupportedLanguageCodes =>
        LanguageFiles.Keys.ToArray();

    public OcrLanguageDataManager(
        HttpClient httpClient,
        string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        _httpClient = httpClient;
        _dataDirectory = Path.GetFullPath(dataDirectory);
    }

    public async Task EnsureLanguageAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var fileName = GetLanguageFileName(languageCode);
        var targetPath = Path.Combine(_dataDirectory, fileName);

        if (File.Exists(targetPath))
        {
            return;
        }

        await _downloadLock.WaitAsync(cancellationToken);

        try
        {
            if (File.Exists(targetPath))
            {
                return;
            }

            Directory.CreateDirectory(_dataDirectory);

            var temporaryPath = targetPath + ".download";

            try
            {
                using var response = await _httpClient.GetAsync(
                    new Uri(LanguageDataBaseUri, fileName),
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                await using var source =
                    await response.Content.ReadAsStreamAsync(
                        cancellationToken);
                await using var destination = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true);

                await source.CopyToAsync(
                    destination,
                    cancellationToken);

                File.Move(
                    temporaryPath,
                    targetPath,
                    overwrite: true);
            }
            catch (OperationCanceledException)
            {
                TryDelete(temporaryPath);
                throw;
            }
            catch (Exception exception)
            {
                TryDelete(temporaryPath);
                throw new OcrException(
                    $"Could not download OCR language data for '{languageCode}'.",
                    exception);
            }
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    private static string GetLanguageFileName(
        string languageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        if (LanguageFiles.TryGetValue(
                languageCode.Trim(),
                out var fileName))
        {
            return fileName;
        }

        throw new OcrException(
            $"OCR language '{languageCode}' is not supported.");
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
            // A later download replaces a stale temporary file.
        }
    }
}
