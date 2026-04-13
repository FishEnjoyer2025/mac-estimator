using System.Windows;
using MacEstimator.App.Services;
using MacEstimator.App.ViewModels;
using MacEstimator.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Velopack;
using Velopack.Sources;

namespace MacEstimator.App;

public partial class App : Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Velopack must be first — handles install/uninstall hooks
        VelopackApp.Build().Run();

        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        DispatcherUnhandledException += (s, args) =>
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "mac-estimator", "error.log");
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
                System.IO.File.AppendAllText(logPath,
                    $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{args.Exception}\n");
            }
            catch { }
            MessageBox.Show(args.Exception.Message, "MAC Estimator Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<EstimateFileService>();
                services.AddSingleton<PdfGenerator>();
                services.AddSingleton<ReportGenerator>();
                services.AddSingleton<JobIndexService>();
                services.AddSingleton<ConfigService>();
                services.AddSingleton<HistoricalDataService>();
                services.AddSingleton<PdfTextExtractor>();
                services.AddSingleton<KeywordScoringService>();
                services.AddSingleton<KeywordConfigService>();
                services.AddSingleton<PricingConfigService>();
                services.AddSingleton<GeminiService>();
                services.AddSingleton<PlanReaderService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<PdfExclusionViewModel>();
                services.AddSingleton<InsightsViewModel>();
                services.AddSingleton<WarRoomViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var vm = _host.Services.GetRequiredService<MainViewModel>();

        // Load historical data for pricing hints
        var historicalService = _host.Services.GetRequiredService<HistoricalDataService>();
        _ = historicalService.LoadAsync().ContinueWith(_ =>
            Dispatcher.Invoke(() =>
            {
                LineItemViewModel.SetHistoricalService(historicalService);
                RoomViewModel.SetHistoricalService(historicalService);
            }));

        // Pre-load pricing config (creates xlsx on first run)
        var pricingService = _host.Services.GetRequiredService<PricingConfigService>();
        _ = pricingService.LoadAsync();

        // Start with one default room
        vm.NewEstimateCommand.Execute(null);
        vm.IsModified = false;

        mainWindow.Show();

        // Check for updates in background (non-blocking)
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var source = new GithubSource("https://github.com/FishEnjoyer2025/mac-estimator", null, false);
            var mgr = new UpdateManager(source);

            if (!mgr.IsInstalled)
                return;

            var update = await mgr.CheckForUpdatesAsync();
            if (update is null)
                return;

            await mgr.DownloadUpdatesAsync(update);

            // Ask user before restarting — don't lose unsaved work
            var result = await Dispatcher.InvokeAsync(() =>
                MessageBox.Show(
                    $"Version {update.TargetFullRelease.Version} is ready to install. Restart now?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information));

            if (result == MessageBoxResult.Yes)
                mgr.ApplyUpdatesAndRestart(update);
        }
        catch
        {
            // Silently ignore update failures — don't block the app
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
