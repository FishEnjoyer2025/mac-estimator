using System.Windows;

namespace MacEstimator.App.Converters;

/// <summary>
/// Freezable proxy that lets XAML DataTemplates and Expander headers
/// reach a DataContext that is not in their direct visual tree.
/// </summary>
public class BindingProxy : Freezable
{
    public static readonly DependencyProperty DataProperty =
        DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy), new UIPropertyMetadata(null));

    public object Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    protected override Freezable CreateInstanceCore() => new BindingProxy();
}
