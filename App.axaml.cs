using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Wortshatzer.Core.Translation;
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
            var popupPresenter = new TranslationPopupPresenter();
            var viewModel = new MainWindowViewModel(
                translationService,
                captureService);

            viewModel.TranslationReady += popupPresenter.Show;
            mainWindow.DataContext = viewModel;

            mainWindow.Closed += (_, _) =>
            {
                viewModel.TranslationReady -= popupPresenter.Show;
                viewModel.Dispose();
                popupPresenter.Dispose();
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
