namespace Wortshatzer.Core.Translation;

public sealed class TranslationException : Exception
{
    public TranslationException(string message)
        : base(message)
    {
    }

    public TranslationException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
