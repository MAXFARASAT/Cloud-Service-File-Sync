using System.Text.Json;
using System.IO;
using FileSync.Core;

namespace FileSync.App;

public partial class App : System.Windows.Application
{
    private SyncCoordinator? _coordinator;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        var basePath = AppContext.BaseDirectory;
        var settingsPath = Path.Combine(basePath, "appsettings.json");
        var dataPath = Path.Combine(basePath, "filesync-metadata.json");
        var logPath = Path.Combine(basePath, "filesync.log");

        var settings = LoadSettings(settingsPath);
        var logger = new FileLogger(logPath);
        var store = new MetadataStore(dataPath, logger);
        var s3 = new S3SyncService(settings, logger);
        var coordinator = new SyncCoordinator(settings, s3, store, logger);
        _coordinator = coordinator;

        var vm = new MainViewModel(coordinator, logger, settings);
        var window = new MainWindow { DataContext = vm };
        window.Show();
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        try
        {
            _coordinator?.DisconnectProvider();
        }
        catch
        {
            // Best effort shutdown cleanup.
        }

        base.OnExit(e);
    }

    private static AppSettings LoadSettings(string path)
    {
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }
}
