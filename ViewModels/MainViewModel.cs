namespace Wortshatzer.ViewModels;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _email = string.Empty;
    private decimal? _age = 10;
    private int _selectedThemeIndex;
    private bool? _notificationsEnabled;

    public string Greeting
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name)) return "Enter your name";

            return $"Hello, {Name}";
        }
    }

    public bool? NotificationsEnabled
    {
        get => _notificationsEnabled;
        set => SetProperty(ref _notificationsEnabled, value);
    }

    public int SelectedThemeIndex
    {
        get => _selectedThemeIndex;
        set => SetProperty(ref _selectedThemeIndex, value);
    }

    public decimal? Age
    {
        get => _age;
        set => SetProperty(ref _age, value);
    }

    public string Email
    {
        get => _name;

        set => SetProperty(ref _name, value);
    }

    public string Name
    {
        get => _name;

        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(Greeting));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool SetProperty<T>(ref T field, T newValue, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, newValue))
        {
            return false;
        }

        field = newValue;
        OnPropertyChanged(propertyName);

        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}