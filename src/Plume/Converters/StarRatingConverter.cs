using System.Globalization;
using System.Windows.Data;
using Plume.Services;

namespace Plume.Converters;

/// <summary>Convertit une note int (1..5) en chaîne d'étoiles « ★★★★☆ ».</summary>
public class StarRatingConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is int n ? StarRating.Format(n) : "";

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
