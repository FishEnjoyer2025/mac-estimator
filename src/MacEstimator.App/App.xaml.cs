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
            MessageBox.Show(args.Exception.Message, "MAC Estimator Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<EstimateFileService>();
                services.AddSingleton<PdfGenerator>();
                services.AddSingleton<JobIndexService>();
                services.AddSingleton<ConfigService>();
                services.AddSingleton<PdfTextExtractor>();
                services.AddSingleton<KeywordScoringService>();
                services.AddSingleton<KeywordConfigService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<PdfExclusionViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var vm = _host.Services.GetRequiredService<MainViewModel>();

        // Start with one default room
        vm.NewEstimateCommand.Execute(null);
        vm.IsModified = false;

        mainWindow.Show();

        // Check for updates in background (non-blocking)
        _ = CheckForUpdatesAsync();
    }

    private static async Task CheckForUpdatesAsync()
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
