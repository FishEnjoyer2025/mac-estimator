using System.Windows;
using System.Windows.Input;
using MacEstimator.App.Models;

namespace MacEstimator.App;

public partial class JobHistoryWindow : Window
{
    private readonly Func<JobIndexEntry, Task> _onOpen;

    public JobHistoryWindow(List<JobIndexEntry> entries, Func<JobIndexEntry, Task> onOpen)
    {
        InitializeComponent();
        _onOpen = onOpen;

        // Sort by most recently modified first
        JobList.ItemsSource = entries.OrderByDescending(e => e.ModifiedAt).ToList();
    }

    private async void OnOpenClick(object sender, RoutedEventArgs e)
    {
        if (JobList.SelectedItem is JobIndexEntry entry)
        {
            await _onOpen(entry);
            Close();
        }
    }

    private async void OnJobDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (JobList.SelectedItem is JobIndexEntry entry)
        {
            await _onOpen(entry);
            Close();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
