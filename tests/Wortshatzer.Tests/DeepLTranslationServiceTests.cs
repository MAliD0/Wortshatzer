using System.Net;
using System.Text;
using Wortshatzer.Core.Languages;
using Wortshatzer.Core.Translation;
using Wortshatzer.Core.Words;
using Wortshatzer.Infrastructure.Translation;
using Xunit;

namespace Wortshatzer.Tests;

public sealed class DeepLTranslationServiceTests
{
    [Fact]
    public async Task TranslateAsync_SendsAuthenticatedLanguageRequest()
    {
        string? authorizationHeader = null;
        string? requestBody = null;

        var handler = new StubHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                authorizationHeader =
                    request.Headers.Authorization?.ToString();
                requestBody = await request.Content!.ReadAsStringAsync(
                    cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"translations":[{"text":"maybe"}]}""",
                        Encoding.UTF8,
                        "application/json")
                };
            });

        using var httpClient = new HttpClient(handler);
        var options = new DeepLTranslationOptions(
            "test-key",
            new Uri("https://api-free.deepl.test/"));
        var service = new DeepLTranslationService(
            httpClient,
            options);

        var translation = await service.TranslateAsync(
            CreateCapturedWord("vielleicht", "de", "en"),
            TestContext.Current.CancellationToken);

        Assert.Equal("DeepL-Auth-Key test-key", authorizationHeader);
        Assert.Contains(
            "\"source_lang\":\"DE\"",
            requestBody);
        Assert.Contains(
            "\"target_lang\":\"EN\"",
            requestBody);
        Assert.Contains(
            "\"vielleicht\"",
            requestBody);
        Assert.Equal("maybe", translation.TranslatedText);
    }

    [Fact]
    public async Task TranslateAsync_MapsRejectedKeyToDomainError()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.Forbidden)));

        using var httpClient = new HttpClient(handler);
        var options = new DeepLTranslationOptions(
            "invalid-key",
            new Uri("https://api-free.deepl.test/"));
        var service = new DeepLTranslationService(
            httpClient,
            options);

        var exception = await Assert.ThrowsAsync<TranslationException>(
            () => service.TranslateAsync(
                CreateCapturedWord("vielleicht", "de", "en"),
                TestContext.Current.CancellationToken));

        Assert.Contains("API key", exception.Message);
    }

    private static CapturedWord CreateCapturedWord(
        string text,
        string sourceCode,
        string targetCode)
    {
        var pair = new LanguagePair(
            new Language(sourceCode, "Source"),
            new Language(targetCode, "Target"));

        return new CapturedWord(text, pair);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<
            HttpRequestMessage,
            CancellationToken,
            Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(
            Func<
                HttpRequestMessage,
                CancellationToken,
                Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request, cancellationToken);
        }
    }
}
