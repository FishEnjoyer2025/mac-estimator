using System.ComponentModel;
using System.Windows;
using MacEstimator.App.ViewModels;

namespace MacEstimator.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_vm.IsModified && !_vm.ConfirmDiscard())
            e.Cancel = true;

        base.OnClosing(e);
    }
}
