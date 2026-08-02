using Wortshatzer.Core.Capture;
using Xunit;

namespace Wortshatzer.Tests;

public sealed class CapturedTextNormalizerTests
{
    [Theory]
    [InlineData("  vielleicht!  ", "vielleicht")]
    [InlineData("short phrase", "short phrase")]
    [InlineData("\tword\t", "word")]
    public void TryNormalize_AcceptsShortText(
        string input,
        string expected)
    {
        var accepted = CapturedTextNormalizer.TryNormalize(
            input,
            out var normalized);

        Assert.True(accepted);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("one two three four")]
    [InlineData("first\nsecond")]
    public void TryNormalize_RejectsUnsupportedClipboardContent(
        string input)
    {
        var accepted = CapturedTextNormalizer.TryNormalize(
            input,
            out var normalized);

        Assert.False(accepted);
        Assert.Empty(normalized);
    }
}
