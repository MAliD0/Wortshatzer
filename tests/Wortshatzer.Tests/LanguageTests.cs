using Wortshatzer.Core.Languages;

namespace Wortshatzer.Tests;

public sealed class LanguageTests
{
    [Fact]
    public void Constructor_NormalizesLanguageCode()
    {
        var language = new Language(" DE ", " German ");

        Assert.Equal("de", language.Code);
        Assert.Equal("German", language.DisplayName);
    }

    [Fact]
    public void LanguagePair_RejectsMatchingLanguageCodes()
    {
        var source = new Language("de", "German");
        var target = new Language("DE", "Deutsch");

        Assert.Throws<ArgumentException>(
            () => new LanguagePair(source, target));
    }

    [Fact]
    public void LanguagePair_PreservesTranslationDirection()
    {
        var source = new Language("de", "German");
        var target = new Language("en", "English");

        var pair = new LanguagePair(source, target);

        Assert.Same(source, pair.Source);
        Assert.Same(target, pair.Target);
    }
}
