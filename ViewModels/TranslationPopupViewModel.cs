using Wortshatzer.Core.Words;

namespace Wortshatzer.ViewModels;

public sealed class TranslationPopupViewModel
{
    public string SourceText { get; }
    public string TranslatedText { get; }
    public string Direction { get; }

    public TranslationPopupViewModel(WordTranslation translation)
    {
        ArgumentNullException.ThrowIfNull(translation);

        SourceText = translation.CapturedWord.Text;
        TranslatedText = translation.TranslatedText;
        Direction =
            $"{translation.CapturedWord.LanguagePair.Source.DisplayName} → " +
            $"{translation.CapturedWord.LanguagePair.Target.DisplayName}";
    }
}
