using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Skua.App.Avalonia.Views;

public partial class OptionContainerView : UserControl
{
    public OptionContainerView()
    {
        InitializeComponent();
    }

    private void CloseClicked(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (this.VisualRoot is Window w)
            w.Close();
    }
}

public static class TypeConverters
{
    public static IValueConverter IsBool { get; } = new TypeCheckConverter(typeof(bool));
    public static IValueConverter IsEnum { get; } = new TypeCheckConverter(null, isEnum: true);
    public static IValueConverter IsOther { get; } = new TypeCheckConverter(null, isOther: true);

    private class TypeCheckConverter : IValueConverter
    {
        private readonly Type? _type;
        private readonly bool _isEnum;
        private readonly bool _isOther;

        public TypeCheckConverter(Type? type, bool isEnum = false, bool isOther = false)
        {
            _type = type;
            _isEnum = isEnum;
            _isOther = isOther;
        }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not Type t) return false;
            if (_type != null) return t == _type;
            if (_isEnum) return t.IsEnum;
            if (_isOther) return t != typeof(bool) && !t.IsEnum;
            return false;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}
