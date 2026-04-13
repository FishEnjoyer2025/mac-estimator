using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using MacEstimator.App.ViewModels;

namespace MacEstimator.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm, PdfExclusionViewModel pdfVm, InsightsViewModel insightsVm, WarRoomViewModel warRoomVm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        PdfExclusionControl.DataContext = pdfVm;
        InsightsControl.DataContext = insightsVm;
        WarRoomControl.DataContext = warRoomVm;

        // Wire War Room card clicks to open estimates in the Estimate tab
        warRoomVm.OpenEstimateCallback = filePath =>
        {
            _vm.OpenEstimateFromPath(filePath);
            MainTabControl.SelectedIndex = 1; // Switch to Estimate tab
        };

        Loaded += async (_, _) => await pdfVm.InitializeCommand.ExecuteAsync(null);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_vm.IsModified && !_vm.ConfirmDiscard())
            e.Cancel = true;

        base.OnClosing(e);
    }

    private void OnReportsClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.ContextMenu is not null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.DataContext = DataContext;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void OnContractorSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is string company)
        {
            _vm.ApplyContractorSuggestionCommand.Execute(company);
            lb.SelectedItem = null;
        }
    }
}
