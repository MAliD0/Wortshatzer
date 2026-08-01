using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wortshatzer.Core.Languages;
using Wortshatzer.Core.Translation;
using Wortshatzer.Core.Words;

namespace Wortshatzer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ITranslationService _translationService;

    [ObservableProperty]
    private Language? _selectedSourceLanguage;

    [ObservableProperty]
    private Language? _selectedTargetLanguage;

    [ObservableProperty]
    private string _capturedText = string.Empty;

    [ObservableProperty]
    private string _translatedSourceText = string.Empty;

    [ObservableProperty]
    private string _translatedText = string.Empty;

    [ObservableProperty]
    private string _translationDirection = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Enter a word to translate.";

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private bool _hasTranslation;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _showEmptyState = true;

    public IReadOnlyList<Language> Languages { get; }

    public IAsyncRelayCommand TranslateCommand { get; }

    public MainWindowViewModel(ITranslationService translationService)
    {
        ArgumentNullException.ThrowIfNull(translationService);

        _translationService = translationService;

        Languages =
        [
            new Language("de", "German"),
            new Language("en", "English"),
            new Language("pl", "Polish"),
            new Language("ru", "Russian")
        ];

        TranslateCommand = new AsyncRelayCommand(
            TranslateAsync,
            CanTranslate);

        SelectedSourceLanguage = Languages[0];
        SelectedTargetLanguage = Languages[1];
        CapturedText = "vielleicht";
    }

    private bool CanTranslate()
    {
        return !IsTranslating
            && !string.IsNullOrWhiteSpace(CapturedText)
            && SelectedSourceLanguage is not null
            && SelectedTargetLanguage is not null
            && SelectedSourceLanguage.Code != SelectedTargetLanguage.Code;
    }

    private async Task TranslateAsync(CancellationToken cancellationToken)
    {
        if (!CanTranslate()
            || SelectedSourceLanguage is null
            || SelectedTargetLanguage is null)
        {
            return;
        }

        IsTranslating = true;
        HasTranslation = false;
        HasError = false;
        ShowEmptyState = true;
        StatusMessage = "Translating…";

        try
        {
            var languagePair = new LanguagePair(
                SelectedSourceLanguage,
                SelectedTargetLanguage);

            var capturedWord = new CapturedWord(
                CapturedText,
                languagePair);

            var translation = await _translationService.TranslateAsync(
                capturedWord,
                cancellationToken);

            TranslatedSourceText = translation.CapturedWord.Text;
            TranslatedText = translation.TranslatedText;
            TranslationDirection =
                $"{languagePair.Source.DisplayName} → {languagePair.Target.DisplayName}";

            HasTranslation = true;
            ShowEmptyState = false;
            StatusMessage = "Translation ready.";
        }
        catch (OperationCanceledException)
        {
            ShowEmptyState = true;
            StatusMessage = "Translation cancelled.";
        }
        catch (InvalidOperationException exception)
        {
            HasError = true;
            ShowEmptyState = false;
            StatusMessage = exception.Message;
        }
        catch
        {
            HasError = true;
            ShowEmptyState = false;
            StatusMessage = "Translation failed. Please try again.";
        }
        finally
        {
            IsTranslating = false;
        }
    }

    partial void OnCapturedTextChanged(string value)
    {
        OnTranslationInputChanged();
    }

    partial void OnSelectedSourceLanguageChanged(Language? value)
    {
        OnTranslationInputChanged();
    }

    partial void OnSelectedTargetLanguageChanged(Language? value)
    {
        OnTranslationInputChanged();
    }

    partial void OnIsTranslatingChanged(bool value)
    {
        TranslateCommand.NotifyCanExecuteChanged();
    }

    private void OnTranslationInputChanged()
    {
        HasTranslation = false;
        HasError = false;
        ShowEmptyState = true;
        TranslatedSourceText = string.Empty;
        TranslatedText = string.Empty;
        TranslationDirection = string.Empty;

        StatusMessage =
            SelectedSourceLanguage?.Code == SelectedTargetLanguage?.Code
                ? "Choose two different languages."
                : "Enter a word to translate.";

        TranslateCommand.NotifyCanExecuteChanged();
    }
}
