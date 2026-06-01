using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace firstProject.Converters;

public class ViewportToScreenshotSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        double viewportWidth = value switch
        {
            Size size => size.Width,
            double viewport => viewport,
            _ => 0d
        };

        if (double.IsNaN(viewportWidth) || viewportWidth <= 0)
        {
            return 120d;
        }

        const double horizontalPadding = 28d;
        double available = Math.Max(0d, viewportWidth - horizontalPadding);

        int columns = available >= 520d ? 3 : available >= 360d ? 2 : 1;
        double gap = 8d;
        double width = (available - (columns - 1) * gap) / columns;
        width = Math.Max(120d, width);

        string mode = parameter?.ToString() ?? "Width";
        if (string.Equals(mode, "Height", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(72d, Math.Round(width * 0.6d));
        }

        return Math.Round(width);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
