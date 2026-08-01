using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Wortshatzer.Infrastructure.Translation;
using Wortshatzer.Services;
using Wortshatzer.ViewModels;
using Wortshatzer.Views;

namespace Wortshatzer;

public partial class App : Application
{
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

            var translationService = new InMemoryTranslationService();
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
            };

            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
