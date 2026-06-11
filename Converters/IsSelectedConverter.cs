using System.Globalization;

namespace Irc7m.Converters;

/// <summary>
/// Converter that checks if the bound value equals the converter parameter.
/// Used for checking if an item is the selected item in a list.
/// </summary>
public class IsSelectedConverter : IValueConverter
{
    public Color SelectedColor { get; set; } = Color.FromArgb("#0078d4");
    public Color UnselectedColor { get; set; } = Colors.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // value is the item being checked
        // parameter is the SelectedNick from the parent binding
        if (value == null || parameter == null)
            return UnselectedColor;
        
        return value.Equals(parameter) ? SelectedColor : UnselectedColor;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

