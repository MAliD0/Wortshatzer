using CommunityToolkit.Mvvm.Input;

namespace Wortshatzer.ViewModels;

public partial class MainWindowViewModel
{
    public event Action? ScraperSettingsRequested;

    [RelayCommand]
    private void OpenScraperSettings()
    {
        ScraperSettingsRequested?.Invoke();
    }
}
