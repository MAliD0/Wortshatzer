using System.Net;
using System.Text;
using Wortshatzer.Core.Dictionary;
using Wortshatzer.Infrastructure.Dictionary;
using Xunit;

namespace Wortshatzer.Tests;

public sealed class HttpDictionaryProfileCacheTests
{
    [Fact]
    public async Task LookupAsync_RefetchesWhenSelectorsChange()
    {
        const string html =
            "<main><span class='first'>maybe</span><span class='second'>perhaps</span></main>";
        var handler = new StubHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    html,
                    Encoding.UTF8,
                    "text/html")
            });
        using var httpClient = new HttpClient(handler);
        var service = new HttpDictionaryLookupService(
            httpClient,
            new AngleSharpScraperEngine());
        var firstProfile = CreateProfile(".first");
        var editedProfile = CreateProfile(".second");

        var first = await service.LookupAsync(
            firstProfile,
            "vielleicht",
            TestContext.Current.CancellationToken);
        var edited = await service.LookupAsync(
            editedProfile,
            "vielleicht",
            TestContext.Current.CancellationToken);

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(
            ["maybe"],
            first.GetValues(DictionaryField.Translation));
        Assert.Equal(
            ["perhaps"],
            edited.GetValues(DictionaryField.Translation));
    }

    private static ScraperProfile CreateProfile(
        string selector)
    {
        return new ScraperProfile(
            "Live editor profile",
            "https://dictionary.test/{word}",
            "de",
            "en",
            [
                new ScraperExtractionRule(
                    DictionaryField.Translation,
                    selector,
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
