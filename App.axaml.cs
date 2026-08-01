using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Wortshatzer.Infrastructure.Translation;
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
            var translationService = new InMemoryTranslationService();

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(translationService)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
