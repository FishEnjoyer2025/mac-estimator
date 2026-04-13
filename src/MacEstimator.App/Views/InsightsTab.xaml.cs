using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MacEstimator.App.ViewModels;

namespace MacEstimator.App.Views;

public partial class InsightsTab : UserControl
{
    public InsightsTab()
    {
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is InsightsViewModel vm && !vm.IsLoaded)
        {
            await vm.LoadDataCommand.ExecuteAsync(null);
        }
    }

    /// <summary>
    /// Intercept mouse wheel on DataGrids and forward to the outer ScrollViewer
    /// so the whole page scrolls smoothly instead of DataGrids eating the events.
    /// </summary>
    private void OnDataGridPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source = sender
        };
        OuterScrollViewer.RaiseEvent(args);
    }
}
