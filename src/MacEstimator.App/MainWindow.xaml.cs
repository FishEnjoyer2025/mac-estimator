using System.ComponentModel;
using System.Windows;
using MacEstimator.App.ViewModels;

namespace MacEstimator.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm, PdfExclusionViewModel pdfVm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        PdfExclusionControl.DataContext = pdfVm;
        Loaded += async (_, _) => await pdfVm.InitializeCommand.ExecuteAsync(null);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_vm.IsModified && !_vm.ConfirmDiscard())
            e.Cancel = true;

        base.OnClosing(e);
    }
}
