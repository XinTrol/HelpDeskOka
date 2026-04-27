using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace DiplomHelpDeskOka.Converters;

public class ToggleBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isActive = value is bool b && b;

        // active / inactive
        return isActive
            ? new SolidColorBrush(Color.Parse("#C9A86A"))
            : new SolidColorBrush(Color.Parse("#E0E0E0"));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}