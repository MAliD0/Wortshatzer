using CommunityToolkit.Mvvm.ComponentModel;
using Wortshatzer.Core.Dictionary;
using Wortshatzer.Core.Words;

namespace Wortshatzer.ViewModels;

public partial class TranslationPopupViewModel :
    ViewModelBase
{
    [ObservableProperty]
    private string _dictionaryText = string.Empty;

    [ObservableProperty]
    private string _dictionarySource = string.Empty;

    [ObservableProperty]
    private bool _hasDictionaryDetails;

    public string SourceText { get; }

    public string TranslatedText { get; }

    public string Direction { get; }

    public TranslationPopupViewModel(WordTranslation translation)
    {
        ArgumentNullException.ThrowIfNull(translation);

        SourceText = translation.CapturedWord.Text;
        TranslatedText = translation.TranslatedText;
        Direction =
            $"{translation.CapturedWord.LanguagePair.Source.DisplayName} → "
            + $"{translation.CapturedWord.LanguagePair.Target.DisplayName}";
    }

    public void ApplyDictionaryResult(
        DictionaryLookupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        DictionaryText = DictionaryResultFormatter.Format(
            result,
            maximumFields: 3,
            maximumValuesPerField: 2);
        DictionarySource = result.SourceName;
        HasDictionaryDetails =
            !string.IsNullOrWhiteSpace(DictionaryText);
    }
}
