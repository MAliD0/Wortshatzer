using CommunityToolkit.Mvvm.ComponentModel;
using Wortshatzer.Core.Translation;
using Wortshatzer.Core.Words;

namespace Wortshatzer.ViewModels;

public partial class MainWindowViewModel
{
    [ObservableProperty]
    private bool _isPopupAlwaysVisible;

    public event Action<bool>? PopupAlwaysVisibleChanged;

    public event Action<string, string>? PopupInputRequested;

    public async Task<WordTranslation> TranslateFromPopupAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (IsTranslating)
        {
            throw new TranslationException(
                "Another translation is already in progress.");
        }

        WordTranslation? completedTranslation = null;

        void CaptureCompletedTranslation(
            WordTranslation translation)
        {
            completedTranslation = translation;
        }

        TranslationReady += CaptureCompletedTranslation;

        try
        {
            CapturedText = text.Trim();

            await TranslateTextAsync(
                CapturedText,
                showPopup: true,
                cancellationToken);
        }
        finally
        {
            TranslationReady -= CaptureCompletedTranslation;
        }

        return completedTranslation
            ?? throw new TranslationException(StatusMessage);
    }

    partial void OnIsPopupAlwaysVisibleChanged(bool value)
    {
        PopupAlwaysVisibleChanged?.Invoke(value);
    }
}
