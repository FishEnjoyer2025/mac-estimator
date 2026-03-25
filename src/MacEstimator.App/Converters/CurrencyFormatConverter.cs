using System.Globalization;
using System.Windows.Data;

namespace MacEstimator.App.Converters;

public class CurrencyFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d)
            return d.ToString("C2", CultureInfo.CurrentCulture);
        return "$0.00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            s = s.Replace("$", "").Replace(",", "").Trim();
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out var result))
                return result;
        }
        return 0m;
    }
}
