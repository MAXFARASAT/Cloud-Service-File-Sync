using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using FileSync.Core;

namespace FileSync.App;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private static readonly TimeSpan HydrationTimeout = TimeSpan.FromSeconds(45);
    private readonly SyncCoordinator _coordinator;
    private readonly IAppLogger _logger;
    private readonly AppSettings _settings;
    private string _syncRootPath;
    private string _lastMessage = "Ready.";
    private bool _isHydrating;

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

    public void UnregisterSyncRoot()
    {
        try
        {
            _settings.SyncRootPath = SyncRootPath;
            _coordinator.UnregisterSyncRoot();
            LastMessage = $"Sync root unregistered: {SyncRootPath}";
        }
        catch (Exception ex)
        {
            _logger.Error("Sync root unregistration failed", ex);
            LastMessage = $"Failed to unregister sync root: {ex.Message}";
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

    public async Task HydrateSelectedAsync()
    {
        if (_isHydrating)
        {
            LastMessage = "Hydration is already in progress. Please wait.";
            return;
        }

        if (SelectedFile is null)
        {
            LastMessage = "Select a file first to hydrate.";
            return;
        }

        _isHydrating = true;
        try
        {
            var selected = SelectedFile;
            LastMessage = $"Hydrating {selected.FileName}...";

            var hydrateTask = Task.Run(() => _coordinator.Hydrate(selected));
            var completedTask = await Task.WhenAny(hydrateTask, Task.Delay(HydrationTimeout));
            if (completedTask != hydrateTask)
            {
                LastMessage = $"Hydration timed out after {HydrationTimeout.TotalSeconds:0} seconds. Falling back to direct S3 download...";
                var fallbackResult = await _coordinator.DownloadWithoutCfapiAsync(selected);
                _coordinator.SaveItems(Files);
                LastMessage = fallbackResult.Status == SyncStatus.Failed && !string.IsNullOrWhiteSpace(fallbackResult.ErrorMessage)
                    ? $"{fallbackResult.FileName}: {fallbackResult.ErrorMessage}"
                    : $"{fallbackResult.FileName}: Downloaded via direct S3 fallback";
                OnPropertyChanged(nameof(SelectedFile));
                OnPropertyChanged(nameof(Files));
                return;
            }

            var result = await hydrateTask;
            if (result.Status == SyncStatus.Failed)
            {
                LastMessage = $"{result.FileName}: CFAPI hydrate failed. Falling back to direct S3 download...";
                var fallbackResult = await _coordinator.DownloadWithoutCfapiAsync(selected);
                _coordinator.SaveItems(Files);
                LastMessage = fallbackResult.Status == SyncStatus.Failed && !string.IsNullOrWhiteSpace(fallbackResult.ErrorMessage)
                    ? $"{fallbackResult.FileName}: {fallbackResult.ErrorMessage}"
                    : $"{fallbackResult.FileName}: Downloaded via direct S3 fallback";
                OnPropertyChanged(nameof(SelectedFile));
                OnPropertyChanged(nameof(Files));
                return;
            }

            _coordinator.SaveItems(Files);
            LastMessage = result.Status == SyncStatus.Failed && !string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? $"{result.FileName}: {result.ErrorMessage}"
                : $"{result.FileName}: {result.Status}";
            OnPropertyChanged(nameof(SelectedFile));
            OnPropertyChanged(nameof(Files));
        }
        finally
        {
            _isHydrating = false;
        }
    }

    public void DeleteSelectedEntry()
    {
        if (SelectedFile is null)
        {
            LastMessage = "Select a metadata entry first to delete.";
            return;
        }

        var removedName = SelectedFile.FileName;
        Files.Remove(SelectedFile);
        SelectedFile = null;
        _coordinator.SaveItems(Files);
        LastMessage = $"Deleted metadata entry: {removedName}";
        OnPropertyChanged(nameof(SelectedFile));
        OnPropertyChanged(nameof(Files));
    }

    public void ClearFailedEntries()
    {
        var failedItems = Files.Where(x => x.Status == SyncStatus.Failed).ToList();
        if (failedItems.Count == 0)
        {
            LastMessage = "No failed metadata entries found.";
            return;
        }

        foreach (var item in failedItems)
        {
            Files.Remove(item);
        }

        if (SelectedFile is not null && SelectedFile.Status == SyncStatus.Failed)
        {
            SelectedFile = null;
            OnPropertyChanged(nameof(SelectedFile));
        }

        _coordinator.SaveItems(Files);
        LastMessage = $"Deleted {failedItems.Count} failed metadata entr{(failedItems.Count == 1 ? "y" : "ies")}.";
        OnPropertyChanged(nameof(Files));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
