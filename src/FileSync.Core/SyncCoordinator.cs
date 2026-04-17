namespace FileSync.Core;

public sealed class SyncCoordinator
{
    private readonly AppSettings _settings;
    private readonly S3SyncService _s3;
    private readonly MetadataStore _store;
    private readonly IAppLogger _logger;

    public SyncCoordinator(AppSettings settings, S3SyncService s3, MetadataStore store, IAppLogger logger)
    {
        _settings = settings;
        _s3 = s3;
        _store = store;
        _logger = logger;
    }

    public void InitializeSyncRoot()
    {
        Directory.CreateDirectory(_settings.SyncRootPath);
        NativeSyncInterop.RegisterRootOrThrow(_settings.SyncRootPath);
        _logger.Info($"Sync root initialized at {_settings.SyncRootPath}");
    }

    public async Task<FileItem> UploadAndCreatePlaceholderAsync(string sourcePath, CancellationToken ct = default)
    {
        var fileInfo = new FileInfo(sourcePath);
        var item = new FileItem
        {
            FileName = fileInfo.Name,
            SizeBytes = fileInfo.Length,
            LocalPath = Path.Combine(_settings.SyncRootPath, fileInfo.Name),
            Status = SyncStatus.Pending,
            UpdatedAtUtc = DateTime.UtcNow
        };

        try
        {
            await _s3.UploadAsync(sourcePath, ct);
            NativeSyncInterop.CreatePlaceholderOrThrow(item.LocalPath, item.SizeBytes, SyncStatus.Synced);
            NativeSyncInterop.NotifyStateOrThrow(item.LocalPath, SyncStatus.Synced);

            item.Status = SyncStatus.Synced;
            item.ErrorMessage = null;
            item.UpdatedAtUtc = DateTime.UtcNow;
            return item;
        }
        catch (Exception ex)
        {
            _logger.Error($"Upload/sync failed for {sourcePath}", ex);
            item.Status = SyncStatus.Failed;
            item.ErrorMessage = GetUserFriendlyError(ex);
            item.UpdatedAtUtc = DateTime.UtcNow;
            return item;
        }
    }

    public IReadOnlyList<FileItem> LoadItems() => _store.Load();

    public void SaveItems(IEnumerable<FileItem> items) => _store.Save(items);

    public FileItem Hydrate(FileItem item)
    {
        try
        {
            NativeSyncInterop.TriggerHydrationOrThrow(item.LocalPath);
            NativeSyncInterop.NotifyStateOrThrow(item.LocalPath, SyncStatus.Downloaded);
            item.Status = SyncStatus.Downloaded;
            item.ErrorMessage = null;
            item.UpdatedAtUtc = DateTime.UtcNow;
            return item;
        }
        catch (Exception ex)
        {
            _logger.Error($"Hydration failed for {item.LocalPath}", ex);
            item.Status = SyncStatus.Failed;
            item.ErrorMessage = GetUserFriendlyError(ex);
            item.UpdatedAtUtc = DateTime.UtcNow;
            return item;
        }
    }

    private static string GetUserFriendlyError(Exception ex)
    {
        if (ex is HttpRequestException)
        {
            return "Cannot connect to LocalStack at localhost:4566. Start LocalStack and create the S3 bucket first.";
        }

        if (ex is NativeSyncException)
        {
            return ex.Message;
        }

        return ex.Message;
    }
}
