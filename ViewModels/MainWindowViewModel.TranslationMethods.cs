using CommunityToolkit.Mvvm.ComponentModel;
using Wortshatzer.Core.Translation;

namespace Wortshatzer.ViewModels;

public sealed class TranslationMethodOption
{
    internal ITranslationService Service { get; }

    public string Id { get; }

    public string DisplayName => Service.ProviderName;

    public TranslationMethodOption(
        string id,
        ITranslationService service)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(service);

        Id = id.Trim();
        Service = service;
    }
}

public partial class MainWindowViewModel
{
    [ObservableProperty]
    private TranslationMethodOption? _selectedTranslationMethod;

    public IReadOnlyList<TranslationMethodOption>
        TranslationMethods { get; private set; } = [];

    private ITranslationService ActiveTranslationService =>
        SelectedTranslationMethod?.Service
            ?? _translationService;

    public void ConfigureTranslationMethods(
        IEnumerable<TranslationMethodOption> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);

        var configuredMethods = methods
            .GroupBy(
                method => method.Id,
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        if (configuredMethods.Length == 0)
        {
            throw new ArgumentException(
                "At least one translation method is required.",
                nameof(methods));
        }

        TranslationMethods = configuredMethods;
        SelectedTranslationMethod = configuredMethods[0];
    }

    partial void OnSelectedTranslationMethodChanged(
        TranslationMethodOption? value)
    {
        OnPropertyChanged(nameof(TranslationProviderName));
        OnTranslationInputChanged();
    }
}
