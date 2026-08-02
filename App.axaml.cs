using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Wortshatzer.Core.Shortcuts;
using Wortshatzer.Core.Translation;
using Wortshatzer.Infrastructure.Dictionary;
using Wortshatzer.Infrastructure.Ocr;
using Wortshatzer.Infrastructure.Translation;
using Wortshatzer.Services;
using Wortshatzer.ViewModels;
using Wortshatzer.Views;

namespace Wortshatzer;

public partial class App : Application
{
    private const string DeepLApiKeyVariable =
        "WORTSHATZER_DEEPL_API_KEY";
    private const string DeepLApiUrlVariable =
        "WORTSHATZER_DEEPL_API_URL";

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            var clipboard = mainWindow.Clipboard
                ?? throw new InvalidOperationException(
                    "The desktop clipboard service is unavailable.");

            var translationService =
                CreateTranslationService(out var deepLHttpClient);
            var captureService =
                new ClipboardCaptureService(clipboard);
            var shortcutService =
                new WindowsGlobalShortcutService();
            var popupPresenter = new TranslationPopupPresenter();

            var applicationDataDirectory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Wortshatzer");

            var ocrHttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(2)
            };
            ocrHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Wortshatzer/0.1");

            var ocrLanguageDataManager =
                new OcrLanguageDataManager(
                    ocrHttpClient,
                    Path.Combine(
                        applicationDataDirectory,
                        "tessdata"));
            var textRecognitionService =
                new TesseractTextRecognitionService(
                    ocrLanguageDataManager);
            var clipboardOcrCaptureService =
                new ClipboardOcrCaptureService(
                    clipboard,
                    textRecognitionService);
            var screenRegionCaptureService =
                new WindowsScreenRegionCaptureService(mainWindow);

            var dictionaryHttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(20)
            };
            dictionaryHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Wortshatzer/0.1");
            dictionaryHttpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd(
                "en-US,en;q=0.8,de;q=0.7");

            var dictionaryLookupService =
                new HttpDictionaryLookupService(
                    dictionaryHttpClient,
                    new AngleSharpScraperEngine());
            var scraperProfileStore =
                new JsonScraperProfileStore(
                    Path.Combine(
                        applicationDataDirectory,
                        "scraper-profiles.json"));
            var activeScraperProfileStore =
                new JsonActiveScraperProfileStore(
                    Path.Combine(
                        applicationDataDirectory,
                        "active-scraper-profiles.json"));
            var scraperProfileResolver =
                new ScraperProfileResolver(
                    scraperProfileStore,
                    activeScraperProfileStore,
                    BuiltInScraperProfiles.All);

            var translationMethods =
                new List<TranslationMethodOption>
                {
                    new(
                        translationService is DeepLTranslationService
                            ? "deepl"
                            : "demo",
                        translationService),
                    new(
                        "web-scraper",
                        new ScraperTranslationService(
                            scraperProfileResolver,
                            dictionaryLookupService))
                };

            if (translationService is DeepLTranslationService)
            {
                translationMethods.Add(
                    new TranslationMethodOption(
                        "demo",
                        new InMemoryTranslationService()));
            }

            var viewModel = new MainWindowViewModel(
                translationService,
                captureService,
                clipboardOcrCaptureService,
                screenRegionCaptureService,
                textRecognitionService);
            viewModel.ConfigureTranslationMethods(
                translationMethods);
            viewModel.ConfigureDictionaryIntegration(
                dictionaryLookupService,
                scraperProfileResolver);

            GlobalShortcutRegistration[] shortcuts =
            [
                new(
                    GlobalShortcutAction.CaptureClipboard,
                    new GlobalShortcutGesture(
                        ShortcutModifiers.Control
                            | ShortcutModifiers.Alt,
                        ShortcutKey.Z)),
                new(
                    GlobalShortcutAction.CaptureOcrRegion,
                    new GlobalShortcutGesture(
                        ShortcutModifiers.Control
                            | ShortcutModifiers.Shift,
                        ShortcutKey.O))
            ];

            void OnShortcutPressed(
                object? sender,
                GlobalShortcutPressedEventArgs eventArgs)
            {
                Dispatcher.UIThread.Post(
                    () => _ = viewModel.HandleGlobalShortcutAsync(
                        eventArgs.Action));
            }

            ScraperSettingsWindow? scraperSettingsWindow = null;

            async void OnScraperSettingsRequested()
            {
                if (scraperSettingsWindow is not null)
                {
                    scraperSettingsWindow.Activate();
                    return;
                }

                var settingsViewModel =
                    new ScraperSettingsViewModel(
                        scraperProfileStore,
                        dictionaryLookupService,
                        BuiltInScraperProfiles.All);
                settingsViewModel.ConfigureActiveProfileStore(
                    activeScraperProfileStore);
                var window = new ScraperSettingsWindow
                {
                    DataContext = settingsViewModel
                };

                scraperSettingsWindow = window;

                try
                {
                    await settingsViewModel.InitializeAsync();
                    await window.ShowDialog(mainWindow);
                }
                finally
                {
                    scraperSettingsWindow = null;
                }
            }

            shortcutService.ShortcutPressed += OnShortcutPressed;

            var failedShortcuts = shortcutService.Start(shortcuts);

            viewModel.SetShortcutStatus(
                shortcuts,
                failedShortcuts);

            viewModel.TranslationReady += popupPresenter.Show;
            viewModel.DictionaryResultReady +=
                popupPresenter.ShowDictionary;
            viewModel.ScraperSettingsRequested +=
                OnScraperSettingsRequested;
            mainWindow.DataContext = viewModel;

            mainWindow.Closed += (_, _) =>
            {
                shortcutService.ShortcutPressed -= OnShortcutPressed;
                viewModel.TranslationReady -= popupPresenter.Show;
                viewModel.DictionaryResultReady -=
                    popupPresenter.ShowDictionary;
                viewModel.ScraperSettingsRequested -=
                    OnScraperSettingsRequested;
                shortcutService.Dispose();
                viewModel.DisposeDictionaryIntegration();
                viewModel.Dispose();
                popupPresenter.Dispose();
                ocrHttpClient.Dispose();
                dictionaryHttpClient.Dispose();
                deepLHttpClient?.Dispose();
            };

            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ITranslationService CreateTranslationService(
        out HttpClient? deepLHttpClient)
    {
        var apiKey =
            Environment.GetEnvironmentVariable(DeepLApiKeyVariable);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            deepLHttpClient = null;
            return new InMemoryTranslationService();
        }

        var configuredApiUrl =
            Environment.GetEnvironmentVariable(DeepLApiUrlVariable);
        Uri? apiBaseUri = null;

        if (!string.IsNullOrWhiteSpace(configuredApiUrl)
            && !Uri.TryCreate(
                configuredApiUrl,
                UriKind.Absolute,
                out apiBaseUri))
        {
            throw new InvalidOperationException(
                $"{DeepLApiUrlVariable} must contain an absolute URL.");
        }

        deepLHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        var options = new DeepLTranslationOptions(
            apiKey,
            apiBaseUri);

        return new DeepLTranslationService(
            deepLHttpClient,
            options);
    }
}
