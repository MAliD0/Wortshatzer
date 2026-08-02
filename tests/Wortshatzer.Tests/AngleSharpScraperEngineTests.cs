using Wortshatzer.Core.Dictionary;
using Wortshatzer.Infrastructure.Dictionary;
using Xunit;

namespace Wortshatzer.Tests;

public sealed class AngleSharpScraperEngineTests
{
    private const string SampleHtml =
        """
        <html>
          <body>
            <article class="entry">
              <h1 class="headword"> vielleicht </h1>
              <span class="part"> adverb </span>
              <ul>
                <li class="translation">maybe</li>
                <li class="translation">perhaps</li>
                <li class="translation">maybe</li>
              </ul>
              <p class="example">
                Vielleicht kommt er morgen.
              </p>
              <audio src="/audio/vielleicht.mp3"></audio>
              <span class="difficulty">A2</span>
            </article>
          </body>
        </html>
        """;

    [Fact]
    public async Task ExtractAsync_UsesConfiguredFieldsAndFallbacks()
    {
        var profile = new ScraperProfile(
            "Test dictionary",
            "https://dictionary.test/de-en/{word}",
            "de",
            "en",
            [
                new ScraperExtractionRule(
                    DictionaryField.Headword,
                    ".headword",
                    resultMode: ScraperResultMode.First,
                    isRequired: true),
                new ScraperExtractionRule(
                    DictionaryField.Translation,
                    ".translation"),
                new ScraperExtractionRule(
                    DictionaryField.Example,
                    ".old-example",
                    fallbackSelectors: [".example"]),
                new ScraperExtractionRule(
                    DictionaryField.AudioUrl,
                    "audio",
                    ScraperValueSource.Attribute,
                    ScraperResultMode.First,
                    attributeName: "src"),
                new ScraperExtractionRule(
                    DictionaryField.Custom,
                    ".difficulty",
                    customFieldName: "Difficulty")
            ],
            ".entry");

        var engine = new AngleSharpScraperEngine();

        var result = await engine.ExtractAsync(
            profile,
            "vielleicht",
            SampleHtml,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ["vielleicht"],
            result.GetValues(DictionaryField.Headword));
        Assert.Equal(
            ["maybe", "perhaps"],
            result.GetValues(DictionaryField.Translation));
        Assert.Equal(
            ["Vielleicht kommt er morgen."],
            result.GetValues(DictionaryField.Example));
        Assert.Equal(
            ["https://dictionary.test/audio/vielleicht.mp3"],
            result.GetValues(DictionaryField.AudioUrl));
        Assert.Equal(["A2"], result.Fields["Difficulty"]);
    }

    [Fact]
    public async Task ExtractAsync_RejectsMissingRequiredField()
    {
        var profile = new ScraperProfile(
            "Test dictionary",
            "https://dictionary.test/{word}",
            "de",
            "en",
            [
                new ScraperExtractionRule(
                    DictionaryField.Translation,
                    ".missing",
                    isRequired: true)
            ]);

        var engine = new AngleSharpScraperEngine();

        var exception =
            await Assert.ThrowsAsync<DictionaryScrapingException>(
                () => engine.ExtractAsync(
                    profile,
                    "vielleicht",
                    SampleHtml,
                    TestContext.Current.CancellationToken));

        Assert.Contains("Translation", exception.Message);
    }
    [Fact]
    public async Task ExtractFirstSuggestionAsync_UsesFirstSafeLink()
    {
        const string suggestionHtml =
            """
            <main>
              <ul class="suggestions">
                <li>
                  <a href="/dictionary/german-english/vielleicht">
                    vielleicht
                  </a>
                </li>
                <li>
                  <a href="https://untrusted.test/word">
                    untrusted
                  </a>
                </li>
              </ul>
            </main>
            """;
        var profile = new ScraperProfile(
            "Test dictionary",
            "https://dictionary.test/{word}",
            "de",
            "en",
            [
                new ScraperExtractionRule(
                    DictionaryField.Translation,
                    ".translation",
                    isRequired: true)
            ],
            suggestionRule: new ScraperSuggestionRule(
                ".suggestions a"));
        var engine = new AngleSharpScraperEngine();

        var suggestion =
            await engine.ExtractFirstSuggestionAsync(
                profile,
                suggestionHtml,
                new Uri(
                    "https://dictionary.test/spellcheck?q=vieleicht"),
                TestContext.Current.CancellationToken);

        Assert.NotNull(suggestion);
        Assert.Equal("vielleicht", suggestion.Word);
        Assert.Equal(
            "https://dictionary.test/dictionary/german-english/vielleicht",
            suggestion.SourceUri.AbsoluteUri);
    }

}
