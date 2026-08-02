using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wortshatzer.Core.Capture;
using Wortshatzer.Core.Languages;
using Wortshatzer.Core.Ocr;
using Wortshatzer.Core.Shortcuts;
using Wortshatzer.Core.Translation;
using Wortshatzer.Core.Words;

namespace Wortshatzer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ITranslationService _translationService;
    private readonly ITextCaptureService _textCaptureService;
    private readonly IClipboardOcrCaptureService _clipboardOcrCaptureService;
    private readonly IScreenRegionCaptureService _screenRegionCaptureService;
    private readonly ITextRecognitionService _textRecognitionService;
    private CancellationTokenSource? _capturedTranslationCancellation;
    private bool _isCaptureInProgress;
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
    private string _shortcutStatus =
        "Global shortcuts are unavailable.";

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

    public string TranslationProviderName =>
        ActiveTranslationService.ProviderName;

    public IAsyncRelayCommand TranslateCommand { get; }

    public MainWindowViewModel(
        ITranslationService translationService,
        ITextCaptureService textCaptureService,
        IClipboardOcrCaptureService clipboardOcrCaptureService,
        IScreenRegionCaptureService screenRegionCaptureService,
        ITextRecognitionService textRecognitionService)
    {
        ArgumentNullException.ThrowIfNull(translationService);
        ArgumentNullException.ThrowIfNull(textCaptureService);
        ArgumentNullException.ThrowIfNull(clipboardOcrCaptureService);
        ArgumentNullException.ThrowIfNull(screenRegionCaptureService);
        ArgumentNullException.ThrowIfNull(textRecognitionService);

        _translationService = translationService;
        _textCaptureService = textCaptureService;
        _clipboardOcrCaptureService = clipboardOcrCaptureService;
        _screenRegionCaptureService = screenRegionCaptureService;
        _textRecognitionService = textRecognitionService;
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

    public void SetShortcutStatus(
        IEnumerable<GlobalShortcutRegistration> registrations,
        IReadOnlyCollection<GlobalShortcutAction> failedActions)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        ArgumentNullException.ThrowIfNull(failedActions);

        var descriptions = registrations.Select(registration =>
            failedActions.Contains(registration.Action)
                ? $"{registration.Gesture} unavailable"
                : $"{registration.Gesture} active");

        ShortcutStatus = string.Join(" • ", descriptions);
    }

    public async Task HandleGlobalShortcutAsync(
        GlobalShortcutAction action)
    {
        if (_isDisposed)
        {
            return;
        }

        if (action == GlobalShortcutAction.SaveLatestTranslation)
        {
            CaptureStatus =
                "Saving translations will be added with vocabulary storage.";
            return;
        }

        if (_isCaptureInProgress)
        {
            CaptureStatus =
                "Finish or cancel the current capture first.";
            return;
        }

        _isCaptureInProgress = true;

        try
        {
            switch (action)
            {
                case GlobalShortcutAction.CaptureClipboard:
                    await CaptureClipboardAsync();
                    break;
                case GlobalShortcutAction.CaptureOcrRegion:
                    await CaptureScreenRegionAsync();
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            CaptureStatus = "Capture was cancelled.";
        }
        catch (OcrException exception)
        {
            CaptureStatus = exception.Message;
        }
        finally
        {
            _isCaptureInProgress = false;
        }
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

    private async Task CaptureClipboardAsync()
    {
        CaptureStatus = "Reading clipboard text or image…";

        var capturedText =
            await _textCaptureService.CaptureCurrentAsync(
                TextCaptureSource.GlobalShortcut);

        if (capturedText)
        {
            return;
        }

        var sourceLanguage = SelectedSourceLanguage;

        if (sourceLanguage is null)
        {
            CaptureStatus =
                "Choose a source language before running OCR.";
            return;
        }

        CaptureStatus =
            $"Recognizing clipboard image as {sourceLanguage.DisplayName}…";

        var ocrResult =
            await _clipboardOcrCaptureService
                .RecognizeCurrentImageAsync(sourceLanguage.Code);

        if (ocrResult is null)
        {
            CaptureStatus =
                "The clipboard does not contain short text or an image.";
            return;
        }

        ProcessOcrResult(ocrResult);
    }

    private async Task CaptureScreenRegionAsync()
    {
        var sourceLanguage = SelectedSourceLanguage;

        if (sourceLanguage is null)
        {
            CaptureStatus =
                "Choose a source language before running OCR.";
            return;
        }

        CaptureStatus =
            "Drag around a word or short phrase. Press Esc to cancel.";

        var image =
            await _screenRegionCaptureService.CaptureRegionAsync();

        if (image is null)
        {
            CaptureStatus = "Screen-region capture cancelled.";
            return;
        }

        CaptureStatus =
            $"Recognizing selected region as {sourceLanguage.DisplayName}…";

        var ocrResult = await _textRecognitionService.RecognizeAsync(
            image,
            sourceLanguage.Code);

        ProcessOcrResult(ocrResult);
    }

    private void ProcessOcrResult(OcrResult ocrResult)
    {
        var singleLineText =
            ocrResult.Text.ReplaceLineEndings(" ");

        if (!CapturedTextNormalizer.TryNormalize(
                singleLineText,
                out var normalizedText))
        {
            CapturedText = singleLineText;
            CaptureStatus =
                "OCR found text, but it must be corrected to a word or phrase of up to three words.";
            return;
        }

        ProcessCapturedText(
            normalizedText,
            TextCaptureSource.Ocr);
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
            ? $"Captured '{text}'. Translating…"
            : "Translating…";

        try
        {
            var languagePair = new LanguagePair(
                sourceLanguage,
                targetLanguage);

            var capturedWord = new CapturedWord(
                text,
                languagePair);

            var translation = await ActiveTranslationService.TranslateAsync(
                capturedWord,
                cancellationToken);

            TranslatedSourceText = translation.CapturedWord.Text;
            TranslatedText = translation.TranslatedText;
            TranslationDirection =
                $"{languagePair.Source.DisplayName} → {languagePair.Target.DisplayName}";

            HasTranslation = true;
            ShowEmptyState = false;
            StatusMessage = showPopup
                ? "Captured text translated."
                : "Translation ready.";

            if (showPopup)
            {
                TranslationReady?.Invoke(translation);
            }

            TranslationCompleted?.Invoke(translation);
        }
        catch (OperationCanceledException)
        {
            ShowEmptyState = true;
            StatusMessage = "Translation cancelled.";
        }
        catch (TranslationException exception)
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
        if (eventArgs.Source == TextCaptureSource.ClipboardMonitor
            && !IsClipboardCaptureEnabled)
        {
            return;
        }

        ProcessCapturedText(
            eventArgs.Text,
            eventArgs.Source);
    }

    private void ProcessCapturedText(
        string text,
        TextCaptureSource source)
    {
        _capturedTranslationCancellation?.Cancel();
        _capturedTranslationCancellation?.Dispose();
        _capturedTranslationCancellation =
            new CancellationTokenSource();

        CapturedText = text;
        CaptureStatus = source switch
        {
            TextCaptureSource.GlobalShortcut =>
                $"Shortcut captured: {text}",
            TextCaptureSource.Ocr =>
                $"OCR captured: {text}",
            _ =>
                $"Clipboard captured: {text}"
        };

        _ = TranslateTextAsync(
            text,
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
        ResetDictionaryDetails();
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
