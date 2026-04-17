using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using FileSync.Core;

namespace FileSync.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly SyncCoordinator _coordinator;
    private readonly IAppLogger _logger;
    private readonly AppSettings _settings;
    private string _syncRootPath;
    private string _lastMessage = "Ready.";

    public MainViewModel(SyncCoordinator coordinator, IAppLogger logger, AppSettings settings)
    {
        _coordinator = coordinator;
        _logger = logger;
        _settings = settings;
        _syncRootPath = settings.SyncRootPath;

        foreach (var item in coordinator.LoadItems())
        {
            Files.Add(item);
        }
    }

    public ObservableCollection<FileItem> Files { get; } = new();
    public FileItem? SelectedFile { get; set; }

    public string SyncRootPath
    {
        get => _syncRootPath;
        set
        {
            _syncRootPath = value;
            OnPropertyChanged();
        }
    }

    public string LastMessage
    {
        get => _lastMessage;
        set
        {
            _lastMessage = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void InitializeSyncRoot()
    {
        try
        {
            _settings.SyncRootPath = SyncRootPath;
            _coordinator.InitializeSyncRoot();
            LastMessage = $"Sync root initialized: {SyncRootPath}";
        }
        catch (Exception ex)
        {
            _logger.Error("Sync root initialization failed", ex);
            LastMessage = $"Failed to initialize sync root: {ex.Message}";
        }
    }

    public async Task HandleDropAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                continue;
            }

            LastMessage = $"Uploading {Path.GetFileName(path)}...";
            _settings.SyncRootPath = SyncRootPath;
            var result = await _coordinator.UploadAndCreatePlaceholderAsync(path);

            var existing = Files.FirstOrDefault(x => x.FileName.Equals(result.FileName, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                Files.Add(result);
            }
            else
            {
                existing.SizeBytes = result.SizeBytes;
                existing.Status = result.Status;
                existing.UpdatedAtUtc = result.UpdatedAtUtc;
            }

            _coordinator.SaveItems(Files);
            LastMessage = result.Status == SyncStatus.Failed && !string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? $"{result.FileName}: {result.ErrorMessage}"
                : $"{result.FileName}: {result.Status}";
        }
    }

    public void HydrateSelected()
    {
        if (SelectedFile is null)
        {
            LastMessage = "Select a file first to hydrate.";
            return;
        }

        var result = _coordinator.Hydrate(SelectedFile);
        _coordinator.SaveItems(Files);
        LastMessage = result.Status == SyncStatus.Failed && !string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? $"{result.FileName}: {result.ErrorMessage}"
            : $"{result.FileName}: {result.Status}";
        OnPropertyChanged(nameof(SelectedFile));
        OnPropertyChanged(nameof(Files));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
