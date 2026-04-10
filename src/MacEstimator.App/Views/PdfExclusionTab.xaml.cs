using System.Windows;
using System.Windows.Controls;
using MacEstimator.App.ViewModels;

namespace MacEstimator.App.Views;

public partial class PdfExclusionTab : UserControl
{
    public PdfExclusionTab()
    {
        InitializeComponent();
        Drop += OnDrop;
        DragOver += OnDragOver;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        try
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false) &&
                e.Data.GetData(DataFormats.FileDrop, false) is string[] files &&
                files.Length > 0 &&
                DataContext is PdfExclusionViewModel vm)
            {
                e.Handled = true;
                await vm.HandleFileDrop(files[0]);
            }
        }
        catch
        {
            // Ignore drag-drop format errors
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        try
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        catch
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }
}
