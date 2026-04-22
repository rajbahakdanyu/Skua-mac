using CommunityToolkit.Mvvm.ComponentModel;
using Skua.Core.Interfaces;
using Skua.Core.Models;

namespace Skua.Avalonia.Services;

public class AvaloniaThemeService : ObservableObject, IThemeService
{
    private bool _isDarkTheme = true;
    private ColorScheme _activeScheme;
    private object? _selectedColor;

    public event ThemeChangedEventHandler? ThemeChanged;
    public event SchemeChangedEventHandler? SchemeChanged;

    public List<object> Presets => new() { "Dark", "Light" };
    public List<object> UserThemes => new();
    public IEnumerable<object> ColorSelectionValues => Array.Empty<object>();
    
    private object _colorSelectionValue = "All";
    public object ColorSelectionValue
    {
        get => _colorSelectionValue;
        set => SetProperty(ref _colorSelectionValue, value);
    }

    public IEnumerable<object> ContrastValues => Array.Empty<object>();
    
    private object _contrastValue = "Medium";
    public object ContrastValue
    {
        get => _contrastValue;
        set => SetProperty(ref _contrastValue, value);
    }

    private float _desiredContrastRatio = 4.5f;
    public float DesiredContrastRatio
    {
        get => _desiredContrastRatio;
        set => SetProperty(ref _desiredContrastRatio, value);
    }

    private bool _isColorAdjusted;
    public bool IsColorAdjusted
    {
        get => _isColorAdjusted;
        set => SetProperty(ref _isColorAdjusted, value);
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set
        {
            if (SetProperty(ref _isDarkTheme, value))
                ApplyBaseTheme(value);
        }
    }

    public object? SelectedColor
    {
        get => _selectedColor;
        set => SetProperty(ref _selectedColor, value);
    }

    public ColorScheme ActiveScheme
    {
        get => _activeScheme;
        set => SetProperty(ref _activeScheme, value);
    }

    public void ApplyBaseTheme(bool isDark)
    {
        // Avalonia theme switching handled by App.axaml RequestedThemeVariant
        _isDarkTheme = isDark;
        if (global::Avalonia.Application.Current is not null)
        {
            global::Avalonia.Application.Current.RequestedThemeVariant = isDark
                ? global::Avalonia.Styling.ThemeVariant.Dark
                : global::Avalonia.Styling.ThemeVariant.Light;
        }
    }

    public void ChangeCustomColor(object? obj) { }

    public void ChangeScheme(ColorScheme scheme)
    {
        ActiveScheme = scheme;
        SchemeChanged?.Invoke(scheme, null);
    }

    public void SaveTheme(string name) { }
    public void SetCurrentTheme(object? theme)
    {
        if (theme is string s)
        {
            IsDarkTheme = s.Equals("Dark", StringComparison.OrdinalIgnoreCase);
        }
        ThemeChanged?.Invoke(theme);
    }
    public void RemoveTheme(object? theme) { }
}
