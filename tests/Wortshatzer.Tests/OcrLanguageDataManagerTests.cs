using System.Net;
using System.Net.Http.Headers;
using Wortshatzer.Core.Ocr;
using Wortshatzer.Infrastructure.Ocr;
using Xunit;

namespace Wortshatzer.Tests;

public sealed class OcrLanguageDataManagerTests
{
    [Fact]
    public async Task EnsureLanguageAsync_UsesFallbackSource()
    {
        var directory = CreateTemporaryDirectory();
        var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.Host == "primary.test")
            {
                return new HttpResponseMessage(
                    HttpStatusCode.ServiceUnavailable);
            }

            var content = new ByteArrayContent(
                new byte[70 * 1024]);
            content.Headers.ContentType =
                new MediaTypeHeaderValue(
                    "application/octet-stream");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = content
            };
        });

        try
        {
            using var httpClient = new HttpClient(handler);
            var manager = new OcrLanguageDataManager(
                httpClient,
                directory,
                [
                    new Uri("https://primary.test/models/"),
                    new Uri("https://fallback.test/models/")
                ]);

            await manager.EnsureLanguageAsync(
                "de",
                TestContext.Current.CancellationToken);

            Assert.Equal(2, handler.RequestCount);
            Assert.True(
                new FileInfo(
                    Path.Combine(directory, "deu.traineddata"))
                .Length >= 64 * 1024);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task EnsureLanguageAsync_ReportsManualInstallPath()
    {
        var directory = CreateTemporaryDirectory();
        var handler = new StubHandler(
            _ => new HttpResponseMessage(
                HttpStatusCode.Forbidden));

        try
        {
            using var httpClient = new HttpClient(handler);
            var manager = new OcrLanguageDataManager(
                httpClient,
                directory,
                [
                    new Uri("https://primary.test/models/"),
                    new Uri("https://fallback.test/models/")
                ]);

            var exception =
                await Assert.ThrowsAsync<OcrException>(
                    () => manager.EnsureLanguageAsync(
                        "de",
                        TestContext.Current.CancellationToken));

            Assert.Equal(2, handler.RequestCount);
            Assert.Contains(directory, exception.Message);
            Assert.Contains(
                "deu.traineddata",
                exception.Message);
            Assert.Contains("HTTP 403", exception.Message);
        }
        finally
        {
            TryDeleteDirectory(directory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "wortshatzer-tests",
            Guid.NewGuid().ToString("N"));
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Test cleanup is best-effort.
        }
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
