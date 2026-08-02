using Wortshatzer.Core.Dictionary;
using Xunit;

namespace Wortshatzer.Tests;

public sealed class ScraperProfileTests
{
    [Fact]
    public void BuildSearchUri_EncodesLookupWord()
    {
        var profile = CreateProfile(
            "https://dictionary.test/search/{word}");

        var uri = profile.BuildSearchUri("groß artig");

        Assert.Equal(
            "https://dictionary.test/search/gro%C3%9F%20artig",
            uri.AbsoluteUri);
    }

    [Fact]
    public void Constructor_RequiresWordPlaceholder()
    {
        Assert.Throws<ArgumentException>(
            () => CreateProfile(
                "https://dictionary.test/search"));
    }

    [Fact]
    public void AttributeRule_RequiresAttributeName()
    {
        Assert.Throws<ArgumentException>(
            () => new ScraperExtractionRule(
                DictionaryField.AudioUrl,
                "audio",
                ScraperValueSource.Attribute));
    }

    private static ScraperProfile CreateProfile(
        string searchUrl)
    {
        return new ScraperProfile(
            "Test dictionary",
            searchUrl,
            "de",
            "en",
            [
                new ScraperExtractionRule(
                    DictionaryField.Headword,
                    ".headword")
            ]);
    }
}
