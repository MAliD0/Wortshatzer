namespace Wortshatzer.Core.Languages;

public sealed record Language
{
    public string Code { get; }
    public string DisplayName { get; }

    public Language(string code, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Code = code.Trim().ToLowerInvariant();
        DisplayName = displayName.Trim();
    }
}