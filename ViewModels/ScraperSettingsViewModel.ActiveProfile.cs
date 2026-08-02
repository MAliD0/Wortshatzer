using CommunityToolkit.Mvvm.Input;
using Wortshatzer.Core.Dictionary;

namespace Wortshatzer.ViewModels;

public partial class ScraperSettingsViewModel
{
    private IActiveScraperProfileStore? _activeProfileStore;

    public void ConfigureActiveProfileStore(
        IActiveScraperProfileStore activeProfileStore)
    {
        ArgumentNullException.ThrowIfNull(activeProfileStore);

        if (_activeProfileStore is not null)
        {
            throw new InvalidOperationException(
                "Active dictionary profile storage is already configured.");
        }

        _activeProfileStore = activeProfileStore;
    }

    [RelayCommand]
    private async Task SetActiveProfileAsync()
    {
        var selected = SelectedProfile;
        var store = _activeProfileStore;

        if (selected is null)
        {
            StatusMessage =
                "Choose a saved profile first.";
            return;
        }

        if (store is null)
        {
            StatusMessage =
                "Active profile storage is unavailable.";
            return;
        }

        IsBusy = true;

        try
        {
            var profile = selected.Profile;

            await store.SetActiveProfileNameAsync(
                profile.SourceLanguageCode,
                profile.TargetLanguageCode,
                profile.Name);

            StatusMessage =
                $"'{profile.Name}' is now active for {profile.SourceLanguageCode} → {profile.TargetLanguageCode}.";
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            StatusMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
