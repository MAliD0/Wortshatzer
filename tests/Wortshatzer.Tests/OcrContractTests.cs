using Wortshatzer.Core.Ocr;
using Xunit;

namespace Wortshatzer.Tests;

public sealed class OcrContractTests
{
    [Fact]
    public void OcrResult_NormalizesTextAndLanguage()
    {
        var result = new OcrResult(
            "  Vielleicht  ",
            "DE",
            0.91);

        Assert.Equal("Vielleicht", result.Text);
        Assert.Equal("de", result.LanguageCode);
        Assert.Equal(0.91, result.Confidence);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void OcrResult_RejectsInvalidConfidence(
        double confidence)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new OcrResult(
                "word",
                "en",
                confidence));
    }
}
