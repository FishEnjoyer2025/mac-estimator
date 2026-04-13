using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MacEstimator.App.Models;
using MacEstimator.App.ViewModels;

namespace MacEstimator.App.Views;

public partial class WarRoomTab : UserControl
{
    public WarRoomTab()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is WarRoomViewModel vm)
        {
            await vm.LoadDataCommand.ExecuteAsync(null);
        }
    }
}

/// <summary>Shows element only when BidStatus == Draft</summary>
public class DraftVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is BidStatus s && s == BidStatus.Draft ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Shows element only when BidStatus == Submitted</summary>
public class SubmittedVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is BidStatus s && s == BidStatus.Submitted ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Shows element when count > 0</summary>
public class CountToVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int n && n > 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
