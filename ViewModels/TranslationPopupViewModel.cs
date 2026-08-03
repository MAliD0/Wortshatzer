using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wortshatzer.Core.Dictionary;
using Wortshatzer.Core.Translation;
using Wortshatzer.Core.Words;

namespace Wortshatzer.ViewModels;

public partial class TranslationPopupViewModel :
    ViewModelBase
{
    private readonly Func<
        string,
        CancellationToken,
        Task<WordTranslation>> _translateAsync;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private string _sourceText = string.Empty;

    [ObservableProperty]
    private string _translatedText = string.Empty;

    [ObservableProperty]
    private string _direction =
        "Enter a word or short phrase";

    [ObservableProperty]
    private string _statusMessage =
        "Ready to translate.";

    [ObservableProperty]
    private string _dictionaryText = string.Empty;

    [ObservableProperty]
    private string _dictionarySource = string.Empty;

    [ObservableProperty]
    private bool _hasTranslation;

    [ObservableProperty]
    private bool _hasDictionaryDetails;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private bool _hasError;

    public IAsyncRelayCommand TranslateCommand { get; }

    public TranslationPopupViewModel(
        Func<
            string,
            CancellationToken,
            Task<WordTranslation>> translateAsync)
    {
        ArgumentNullException.ThrowIfNull(translateAsync);

        _translateAsync = translateAsync;
        TranslateCommand = new AsyncRelayCommand(
            TranslateAsync,
            CanTranslate);
    }

    public void PrepareInputCorrection(
        string suggestedText,
        string message)
    {
        ArgumentNullException.ThrowIfNull(suggestedText);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        InputText = suggestedText
            .ReplaceLineEndings(" ")
            .Trim();
        SourceText = string.Empty;
        TranslatedText = string.Empty;
        Direction = "Review captured text";
        StatusMessage = message.Trim();
        HasTranslation = false;
        HasError = false;
        ResetDictionaryResult();
    }

    public void ApplyTranslation(
        WordTranslation translation)
    {
        ArgumentNullException.ThrowIfNull(translation);

        var isNewResult = !string.Equals(
            SourceText,
            translation.CapturedWord.Text,
            StringComparison.OrdinalIgnoreCase);

        SourceText = translation.CapturedWord.Text;
        InputText = translation.CapturedWord.Text;
        TranslatedText = translation.TranslatedText;
        Direction =
            $"{translation.CapturedWord.LanguagePair.Source.DisplayName} → "
            + $"{translation.CapturedWord.LanguagePair.Target.DisplayName}";
        StatusMessage = "Translation ready.";
        HasTranslation = true;
        HasError = false;

        if (isNewResult)
        {
            ResetDictionaryResult();
        }
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

    private bool CanTranslate()
    {
        return !IsTranslating
            && !string.IsNullOrWhiteSpace(InputText);
    }

    private async Task TranslateAsync(
        CancellationToken cancellationToken)
    {
        IsTranslating = true;
        HasError = false;
        StatusMessage = "Translating…";
        ResetDictionaryResult();

        try
        {
            var translation = await _translateAsync(
                InputText,
                cancellationToken);

            ApplyTranslation(translation);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Translation cancelled.";
        }
        catch (TranslationException exception)
        {
            HasError = true;
            StatusMessage = exception.Message;
        }
        catch
        {
            HasError = true;
            StatusMessage =
                "Translation failed. Please try again.";
        }
        finally
        {
            IsTranslating = false;
        }
    }

    private void ResetDictionaryResult()
    {
        DictionaryText = string.Empty;
        DictionarySource = string.Empty;
        HasDictionaryDetails = false;
    }

    partial void OnInputTextChanged(string value)
    {
        TranslateCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsTranslatingChanged(bool value)
    {
        TranslateCommand.NotifyCanExecuteChanged();
    }
}
