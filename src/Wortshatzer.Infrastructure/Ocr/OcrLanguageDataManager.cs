using Wortshatzer.Core.Ocr;

namespace Wortshatzer.Infrastructure.Ocr;

public sealed class OcrLanguageDataManager
{
    private const long MinimumLanguageDataBytes = 64 * 1024;

    private static readonly IReadOnlyList<Uri>
        DefaultLanguageDataBaseUris =
        [
            new(
                "https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/main/"),
            new(
                "https://cdn.jsdelivr.net/gh/tesseract-ocr/tessdata_fast@main/")
        ];

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
    private readonly IReadOnlyList<Uri> _languageDataBaseUris;
    private readonly SemaphoreSlim _downloadLock = new(1, 1);

    public string DataDirectory => _dataDirectory;

    public IReadOnlyCollection<string> SupportedLanguageCodes =>
        LanguageFiles.Keys.ToArray();

    public OcrLanguageDataManager(
        HttpClient httpClient,
        string dataDirectory,
        IEnumerable<Uri>? languageDataBaseUris = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        var configuredBaseUris =
            (languageDataBaseUris ?? DefaultLanguageDataBaseUris)
            .Select(NormalizeBaseUri)
            .Distinct()
            .ToArray();

        if (configuredBaseUris.Length == 0)
        {
            throw new ArgumentException(
                "At least one OCR language-data source is required.",
                nameof(languageDataBaseUris));
        }

        _httpClient = httpClient;
        _dataDirectory = Path.GetFullPath(dataDirectory);
        _languageDataBaseUris = configuredBaseUris;
    }

    public async Task EnsureLanguageAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var fileName = GetLanguageFileName(languageCode);
        var targetPath = Path.Combine(_dataDirectory, fileName);

        if (IsUsableLanguageFile(targetPath))
        {
            return;
        }

        await _downloadLock.WaitAsync(cancellationToken);

        try
        {
            if (IsUsableLanguageFile(targetPath))
            {
                return;
            }

            Directory.CreateDirectory(_dataDirectory);
            TryDelete(targetPath);

            var temporaryPath = targetPath + ".download";
            Exception? lastFailure = null;

            foreach (var baseUri in _languageDataBaseUris)
            {
                TryDelete(temporaryPath);

                try
                {
                    await DownloadAsync(
                        new Uri(baseUri, fileName),
                        temporaryPath,
                        cancellationToken);

                    File.Move(
                        temporaryPath,
                        targetPath,
                        overwrite: true);
                    return;
                }
                catch (OperationCanceledException)
                {
                    TryDelete(temporaryPath);
                    throw;
                }
                catch (Exception exception)
                {
                    TryDelete(temporaryPath);
                    lastFailure = exception;
                }
            }

            var manualUrl =
                new Uri(_languageDataBaseUris[0], fileName);

            throw new OcrException(
                $"Could not download OCR language data for '{languageCode}'. "
                + $"Download '{manualUrl}' manually and save it as '{targetPath}'. "
                + $"Last error: {lastFailure?.Message}",
                lastFailure
                    ?? new InvalidOperationException(
                        "No OCR language-data source was attempted."));
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    private async Task DownloadAsync(
        Uri sourceUri,
        string temporaryPath,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            sourceUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} ({response.StatusCode}) from {sourceUri.Host}.");
        }

        var mediaType =
            response.Content.Headers.ContentType?.MediaType;

        if (mediaType?.StartsWith(
                "text/",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new InvalidDataException(
                $"{sourceUri.Host} returned '{mediaType}' instead of OCR language data.");
        }

        var contentLength =
            response.Content.Headers.ContentLength;

        if (contentLength.HasValue
            && contentLength.Value < MinimumLanguageDataBytes)
        {
            throw new InvalidDataException(
                $"{sourceUri.Host} returned an incomplete OCR language file.");
        }

        long downloadedBytes;

        await using (var source =
            await response.Content.ReadAsStreamAsync(
                cancellationToken))
        await using (var destination = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true))
        {
            await source.CopyToAsync(
                destination,
                cancellationToken);
            await destination.FlushAsync(cancellationToken);
            downloadedBytes = destination.Length;
        }

        if (downloadedBytes < MinimumLanguageDataBytes)
        {
            throw new InvalidDataException(
                $"{sourceUri.Host} returned an incomplete OCR language file.");
        }
    }

    private static Uri NormalizeBaseUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!uri.IsAbsoluteUri
            || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                "OCR language-data sources must be absolute HTTPS URLs.",
                nameof(uri));
        }

        return new Uri(
            uri.AbsoluteUri.TrimEnd('/') + "/",
            UriKind.Absolute);
    }

    private static bool IsUsableLanguageFile(string path)
    {
        try
        {
            return File.Exists(path)
                && new FileInfo(path).Length
                    >= MinimumLanguageDataBytes;
        }
        catch
        {
            return false;
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
            // A later download replaces a stale or incomplete file.
        }
    }
}
