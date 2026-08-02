using System.Net;
using System.Text;
using Wortshatzer.Core.Dictionary;
using Wortshatzer.Infrastructure.Dictionary;
using Xunit;

namespace Wortshatzer.Tests;

public sealed class HttpDictionaryLookupServiceTests
{
    [Fact]
    public async Task LookupAsync_CachesSuccessfulResponse()
    {
        const string html =
            "<main><h1>vielleicht</h1><span class='translation'>maybe</span></main>";
        var handler = new StubHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    html,
                    Encoding.UTF8,
                    "text/html")
            });
        using var httpClient = new HttpClient(handler);
        var profile = CreateProfile();
        var service = new HttpDictionaryLookupService(
            httpClient,
            new AngleSharpScraperEngine());

        var first = await service.LookupAsync(
            profile,
            "vielleicht",
            TestContext.Current.CancellationToken);
        var second = await service.LookupAsync(
            profile,
            "vielleicht",
            TestContext.Current.CancellationToken);

        Assert.Equal(1, handler.RequestCount);
        Assert.Same(first, second);
        Assert.Equal(
            ["maybe"],
            first.GetValues(DictionaryField.Translation));
    }

    [Fact]
    public async Task LookupAsync_RejectsUnsuccessfulResponse()
    {
        var handler = new StubHandler(
            _ => new HttpResponseMessage(
                HttpStatusCode.TooManyRequests));
        using var httpClient = new HttpClient(handler);
        var service = new HttpDictionaryLookupService(
            httpClient,
            new AngleSharpScraperEngine());

        var exception =
            await Assert.ThrowsAsync<DictionaryScrapingException>(
                () => service.LookupAsync(
                    CreateProfile(),
                    "vielleicht",
                    TestContext.Current.CancellationToken));

        Assert.Contains("429", exception.Message);
    }

    [Fact]
    public async Task LookupAsync_RetriesFirstClosestSuggestion()
    {
        var handler = new StubHandler(request =>
        {
            var html = request.RequestUri!.AbsolutePath
                .EndsWith(
                    "/vielleicht",
                    StringComparison.Ordinal)
                ? "<main><h1>vielleicht</h1><span class='translation'>maybe</span></main>"
                : "<main><ul class='suggestions'><li><a href='/dictionary/de-en/vielleicht'>vielleicht</a></li></ul></main>";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    html,
                    Encoding.UTF8,
                    "text/html")
            };
        });
        using var httpClient = new HttpClient(handler);
        var profile = new ScraperProfile(
            "Test dictionary",
            "https://dictionary.test/dictionary/de-en/{word}",
            "de",
            "en",
            [
                new ScraperExtractionRule(
                    DictionaryField.Headword,
                    "h1",
                    resultMode: ScraperResultMode.First,
                    isRequired: true),
                new ScraperExtractionRule(
                    DictionaryField.Translation,
                    ".translation",
                    isRequired: true)
            ],
            "main",
            new ScraperSuggestionRule(
                ".suggestions a"));
        var service = new HttpDictionaryLookupService(
            httpClient,
            new AngleSharpScraperEngine());

        var result = await service.LookupAsync(
            profile,
            "vieleicht",
            TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal("vieleicht", result.Query);
        Assert.Equal(
            ["vielleicht"],
            result.GetValues(DictionaryField.Headword));
        Assert.Equal(
            ["maybe"],
            result.GetValues(DictionaryField.Translation));
        Assert.Equal(
            "https://dictionary.test/dictionary/de-en/vielleicht",
            result.SourceUri.AbsoluteUri);
    }

    [Fact]
    public void BuiltInProfiles_HaveSafeUrlsAndUsefulFields()
    {
        var cambridge =
            BuiltInScraperProfiles.CambridgeGermanEnglish;
        var verbformen =
            BuiltInScraperProfiles.VerbformenGerman;

        Assert.Equal(
            "https://dictionary.cambridge.org/dictionary/german-english/vielleicht",
            cambridge.BuildSearchUri("vielleicht").AbsoluteUri);
        Assert.Contains(
            cambridge.Fields,
            field => field.Field == DictionaryField.Translation);
        Assert.NotNull(cambridge.SuggestionRule);
        Assert.Equal(
            "https://www.verbformen.de/?w=gehen",
            verbformen.BuildSearchUri("gehen").AbsoluteUri);
        Assert.Contains(
            verbformen.Fields,
            field => field.Field == DictionaryField.Conjugation);
    }

    private static ScraperProfile CreateProfile()
    {
        return new ScraperProfile(
            "Test dictionary",
            "https://dictionary.test/{word}",
            "de",
            "en",
            [
                new ScraperExtractionRule(
                    DictionaryField.Headword,
                    "h1",
                    resultMode: ScraperResultMode.First,
                    isRequired: true),
                new ScraperExtractionRule(
                    DictionaryField.Translation,
                    ".translation",
                    isRequired: true)
            ],
            "main");
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => _requestCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            return Task.FromResult(responseFactory(request));
        }
    }
}
