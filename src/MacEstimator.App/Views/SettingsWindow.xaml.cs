using System.Collections.ObjectModel;
using System.Windows;
using MacEstimator.App.Models;
using MacEstimator.App.Services;

namespace MacEstimator.App;

public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;
    public ObservableCollection<SettingsLineItem> Items { get; }

    public static string[] UnitOptions { get; } = ["LF", "SF", "EA"];
    public static string[] ModeOptions { get; } = ["Per Unit", "Markup"];

    public bool WasSaved { get; private set; }

    public SettingsWindow(ConfigService configService, AppConfig config)
    {
        _configService = configService;

        Items = new ObservableCollection<SettingsLineItem>(
            config.DefaultLineItems.Select(li => new SettingsLineItem
            {
                Name = li.Name,
                DefaultRate = li.DefaultRate,
                Unit = ToUnitDisplay(li.Unit),
                Mode = ToModeDisplay(li.Mode),
                NameOptions = li.NameOptions?.ToList()
            }));

        InitializeComponent();
        ItemsGrid.ItemsSource = Items;
    }

    private void OnAddItem(object sender, RoutedEventArgs e)
    {
        Items.Add(new SettingsLineItem
        {
            Name = "New Item",
            DefaultRate = 0,
            Unit = "LF",
            Mode = "Per Unit"
        });
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = new AppConfig
            {
                DefaultLineItems = Items.Select(i => new LineItemConfig
                {
                    Name = i.Name,
                    DefaultRate = i.DefaultRate,
                    Unit = FromUnitDisplay(i.Unit),
                    Mode = FromModeDisplay(i.Mode),
                    NameOptions = i.NameOptions
                }).ToList()
            };
            await _configService.SaveAsync(config);
            WasSaved = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save defaults:\n\n{ex.Message}",
                "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    private static string ToUnitDisplay(string unit) => unit switch
    {
        "LinearFoot" => "LF",
        "SquareFoot" => "SF",
        "Each" => "EA",
        _ => unit
    };

    private static string FromUnitDisplay(string display) => display switch
    {
        "LF" => "LinearFoot",
        "SF" => "SquareFoot",
        "EA" => "Each",
        _ => display
    };

    private static string ToModeDisplay(string mode) => mode switch
    {
        "PerUnit" => "Per Unit",
        "VendorQuoteMarkup" => "Markup",
        _ => mode
    };

    private static string FromModeDisplay(string display) => display switch
    {
        "Per Unit" => "PerUnit",
        "Markup" => "VendorQuoteMarkup",
        _ => display
    };
}

/// <summary>
/// Display-friendly wrapper for the settings grid.
/// </summary>
public class SettingsLineItem
{
    public string Name { get; set; } = string.Empty;
    public decimal DefaultRate { get; set; }
    public string Unit { get; set; } = "LF";
    public string Mode { get; set; } = "Per Unit";
    public List<string>? NameOptions { get; set; }
}
