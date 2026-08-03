namespace Wortshatzer.Core.Capture;

public static class CapturedTextNormalizer
{
    public static bool TryNormalize(
        string? capturedText,
        out string normalizedText)
    {
        normalizedText = string.Empty;

        if (string.IsNullOrWhiteSpace(capturedText))
        {
            return false;
        }

        var candidate = capturedText.Trim();

        if (candidate.Length > 64
            || candidate.Contains('\r')
            || candidate.Contains('\n'))
        {
            return false;
        }

        candidate = candidate.Trim(
            ' ', '\t', '.', ',', ';', ':', '!', '?',
            '"', '\'', '“', '”', '„',
            '(', ')', '[', ']', '{', '}');

        if (string.IsNullOrWhiteSpace(candidate)
            || !candidate.Any(char.IsLetter))
        {
            return false;
        }

        var parts = candidate.Split(
            [' ', '\t'],
            StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);

        if (parts.Length is < 1 or > 3)
        {
            return false;
        }

        normalizedText = string.Join(' ', parts);
        return true;
    }
}
