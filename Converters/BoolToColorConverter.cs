using System.Globalization;

namespace Irc7m.Converters;

/// <summary>
/// Converts a bool to one of two colours.
/// Usage: TrueColor="#3c3c3c" FalseColor="#252526"
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public Color TrueColor  { get; set; } = Colors.White;
    public Color FalseColor { get; set; } = Colors.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TrueColor : FalseColor;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

