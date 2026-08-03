namespace Wortshatzer.Infrastructure.Translation;

public sealed class DeepLTranslationOptions
{
    public static readonly Uri DefaultFreeApiBaseUri =
        new("https://api-free.deepl.com/");

    public string ApiKey { get; }
    public Uri ApiBaseUri { get; }

    public DeepLTranslationOptions(
        string apiKey,
        Uri? apiBaseUri = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var selectedBaseUri =
            apiBaseUri ?? DefaultFreeApiBaseUri;

        if (!selectedBaseUri.IsAbsoluteUri
            || selectedBaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                "The DeepL API base URI must be an absolute HTTPS address.",
                nameof(apiBaseUri));
        }

        ApiKey = apiKey.Trim();
        ApiBaseUri = EnsureTrailingSlash(selectedBaseUri);
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var value = uri.AbsoluteUri;

        return value.EndsWith(
            "/",
            StringComparison.Ordinal)
                ? uri
                : new Uri($"{value}/");
    }
}
