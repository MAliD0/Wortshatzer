using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wortshatzer.Core.Capture;
using Wortshatzer.Core.Languages;
using Wortshatzer.Core.Translation;
using Wortshatzer.Core.Words;

namespace Wortshatzer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ITranslationService _translationService;
    private readonly ITextCaptureService _textCaptureService;
    private CancellationTokenSource? _capturedTranslationCancellation;
    private bool _isDisposed;

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
    private string _captureStatus = "Clipboard monitoring is off.";

    [ObservableProperty]
    private bool _isClipboardCaptureEnabled;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private bool _hasTranslation;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _showEmptyState = true;

    public event Action<WordTranslation>? TranslationReady;

    public IReadOnlyList<Language> Languages { get; }

    public IAsyncRelayCommand TranslateCommand { get; }

    public MainWindowViewModel(
        ITranslationService translationService,
        ITextCaptureService textCaptureService)
    {
        ArgumentNullException.ThrowIfNull(translationService);
        ArgumentNullException.ThrowIfNull(textCaptureService);

        _translationService = translationService;
        _textCaptureService = textCaptureService;
        _textCaptureService.TextCaptured += OnTextCaptured;

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

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _capturedTranslationCancellation?.Cancel();
        _capturedTranslationCancellation?.Dispose();
        _textCaptureService.TextCaptured -= OnTextCaptured;
        _textCaptureService.Dispose();
        _isDisposed = true;
    }

    private bool CanTranslate()
    {
        return !IsTranslating
            && !string.IsNullOrWhiteSpace(CapturedText)
            && SelectedSourceLanguage is not null
            && SelectedTargetLanguage is not null
            && SelectedSourceLanguage.Code != SelectedTargetLanguage.Code;
    }

    private Task TranslateAsync(CancellationToken cancellationToken)
    {
        return TranslateTextAsync(
            CapturedText,
            showPopup: false,
            cancellationToken);
    }

    private async Task TranslateTextAsync(
        string text,
        bool showPopup,
        CancellationToken cancellationToken)
    {
        var sourceLanguage = SelectedSourceLanguage;
        var targetLanguage = SelectedTargetLanguage;

        if (string.IsNullOrWhiteSpace(text)
            || sourceLanguage is null
            || targetLanguage is null
            || sourceLanguage.Code == targetLanguage.Code)
        {
            return;
        }

        IsTranslating = true;
        HasTranslation = false;
        HasError = false;
        ShowEmptyState = true;
        StatusMessage = showPopup
            ? $"Captured '{text}' from the clipboard. Translating…"
            : "Translating…";

        try
        {
            var languagePair = new LanguagePair(
                sourceLanguage,
                targetLanguage);

            var capturedWord = new CapturedWord(
                text,
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
            StatusMessage = showPopup
                ? "Clipboard word translated."
                : "Translation ready.";

            if (showPopup)
            {
                TranslationReady?.Invoke(translation);
            }
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

    private void OnTextCaptured(
        object? sender,
        TextCapturedEventArgs eventArgs)
    {
        if (!IsClipboardCaptureEnabled)
        {
            return;
        }

        _capturedTranslationCancellation?.Cancel();
        _capturedTranslationCancellation?.Dispose();
        _capturedTranslationCancellation =
            new CancellationTokenSource();

        CapturedText = eventArgs.Text;
        CaptureStatus = $"Captured: {eventArgs.Text}";

        _ = TranslateTextAsync(
            eventArgs.Text,
            showPopup: true,
            _capturedTranslationCancellation.Token);
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

    partial void OnIsClipboardCaptureEnabledChanged(bool value)
    {
        if (value)
        {
            _textCaptureService.Start();
            CaptureStatus =
                "Monitoring clipboard. Copy a word or a phrase up to three words.";
        }
        else
        {
            _textCaptureService.Stop();
            CaptureStatus = "Clipboard monitoring is off.";
        }
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
