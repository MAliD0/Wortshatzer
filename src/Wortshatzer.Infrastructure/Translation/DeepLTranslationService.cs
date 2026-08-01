using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Wortshatzer.Core.Translation;
using Wortshatzer.Core.Words;

namespace Wortshatzer.Infrastructure.Translation;

public sealed class DeepLTranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly DeepLTranslationOptions _options;

    public string ProviderName => "DeepL API";

    public DeepLTranslationService(
        HttpClient httpClient,
        DeepLTranslationOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        _httpClient = httpClient;
        _options = options;
    }

    public async Task<WordTranslation> TranslateAsync(
        CapturedWord capturedWord,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capturedWord);

        var requestBody = new DeepLRequest(
            [capturedWord.Text],
            capturedWord.LanguagePair.Source.Code.ToUpperInvariant(),
            capturedWord.LanguagePair.Target.Code.ToUpperInvariant());

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(_options.ApiBaseUri, "v2/translate"));

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "DeepL-Auth-Key",
                _options.ApiKey);
        request.Headers.UserAgent.ParseAdd("Wortshatzer/0.1");
        request.Content = JsonContent.Create(requestBody);

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateServiceException(response.StatusCode);
            }

            var responseBody =
                await response.Content.ReadFromJsonAsync<DeepLResponse>(
                    cancellationToken: cancellationToken);

            var translatedText =
                responseBody?.Translations.FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(translatedText))
            {
                throw new TranslationException(
                    "DeepL returned an empty translation.");
            }

            return new WordTranslation(
                capturedWord,
                translatedText);
        }
        catch (TranslationException)
        {
            throw;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new TranslationException(
                "The DeepL request timed out.");
        }
        catch (HttpRequestException exception)
        {
            throw new TranslationException(
                "Could not connect to DeepL. Check your internet connection.",
                exception);
        }
    }

    private static TranslationException CreateServiceException(
        HttpStatusCode statusCode)
    {
        return (int)statusCode switch
        {
            401 or 403 => new TranslationException(
                "DeepL rejected the API key. Check WORTSHATZER_DEEPL_API_KEY."),
            429 => new TranslationException(
                "DeepL rate limit reached. Try again shortly."),
            456 => new TranslationException(
                "DeepL translation quota has been exceeded."),
            >= 500 => new TranslationException(
                "DeepL is temporarily unavailable."),
            _ => new TranslationException(
                $"DeepL request failed with status code {(int)statusCode}.")
        };
    }

    private sealed record DeepLRequest(
        [property: JsonPropertyName("text")]
        string[] Text,

        [property: JsonPropertyName("source_lang")]
        string SourceLanguage,

        [property: JsonPropertyName("target_lang")]
        string TargetLanguage);

    private sealed class DeepLResponse
    {
        [JsonPropertyName("translations")]
        public List<DeepLTranslation> Translations { get; init; } = [];
    }

    private sealed class DeepLTranslation
    {
        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }
}
