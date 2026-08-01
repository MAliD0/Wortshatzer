namespace Wortshatzer.Core.Languages;

public sealed record LanguagePair
{
    public Language Source { get; }
    public Language Target { get; }

    public LanguagePair(Language source, Language target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (source.Code == target.Code)
        {
            throw new ArgumentException(
                "Source and target languages must be different.");
        }

        Source = source;
        Target = target;
    }
}