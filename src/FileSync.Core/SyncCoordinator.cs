namespace FileSync.Core;

public sealed class SyncCoordinator
{
    private const int ErrorCloudOperationNotUnderSyncRoot = unchecked((int)0x80070186);
    private const int ErrorCloudProviderNotRunning = unchecked((int)0x801F0005);
    private const int ErrorAlreadyExists = unchecked((int)0x800700B7);
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
        NativeSyncInterop.ConnectRootOrThrow(_settings.SyncRootPath);
        _logger.Info($"Sync root initialized at {_settings.SyncRootPath}");
    }

    public void UnregisterSyncRoot()
    {
        NativeSyncInterop.DisconnectRootOrThrow();
        NativeSyncInterop.UnregisterRootOrThrow(_settings.SyncRootPath);
        _logger.Info($"Sync root unregistered at {_settings.SyncRootPath}");
    }

    public void DisconnectProvider()
    {
        NativeSyncInterop.DisconnectRootOrThrow();
        _logger.Info("Sync provider disconnected.");
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
            TryCreatePlaceholderWithSyncRootRecovery(item);

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
        if (item.Status != SyncStatus.Synced)
        {
            item.Status = SyncStatus.Failed;
            item.ErrorMessage = $"Only files in '{SyncStatus.Synced}' state can be hydrated. Re-upload this file first.";
            item.UpdatedAtUtc = DateTime.UtcNow;
            _logger.Error($"Hydration skipped because file is not synced: {item.LocalPath} (Status={item.Status})");
            return item;
        }

        if (!File.Exists(item.LocalPath))
        {
            item.Status = SyncStatus.Failed;
            item.ErrorMessage = $"Local placeholder not found at '{item.LocalPath}'. Upload/create placeholder first, then hydrate.";
            item.UpdatedAtUtc = DateTime.UtcNow;
            _logger.Error($"Hydration skipped because placeholder is missing: {item.LocalPath}");
            return item;
        }

        try
        {
            NativeSyncInterop.TriggerHydrationOrThrow(item.LocalPath);
            NativeSyncInterop.NotifyStateOrThrow(item.LocalPath, SyncStatus.Downloaded);
            item.Status = SyncStatus.Downloaded;
            item.ErrorMessage = null;
            item.UpdatedAtUtc = DateTime.UtcNow;
            return item;
        }
        catch (NativeSyncException ex) when (ex.HResult == ErrorCloudProviderNotRunning)
        {
            _logger.Info("Cloud provider was not running during hydration. Reconnecting and retrying once.");
            try
            {
                NativeSyncInterop.ConnectRootOrThrow(_settings.SyncRootPath);
                NativeSyncInterop.TriggerHydrationOrThrow(item.LocalPath);
                NativeSyncInterop.NotifyStateOrThrow(item.LocalPath, SyncStatus.Downloaded);
                item.Status = SyncStatus.Downloaded;
                item.ErrorMessage = null;
                item.UpdatedAtUtc = DateTime.UtcNow;
                return item;
            }
            catch (Exception retryEx)
            {
                _logger.Error($"Hydration retry failed for {item.LocalPath}", retryEx);
                item.Status = SyncStatus.Failed;
                item.ErrorMessage = GetUserFriendlyError(retryEx);
                item.UpdatedAtUtc = DateTime.UtcNow;
                return item;
            }
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

    public async Task<FileItem> DownloadWithoutCfapiAsync(FileItem item, CancellationToken ct = default)
    {
        try
        {
            await _s3.DownloadAsync(item.FileName, item.LocalPath, ct);
            item.Status = SyncStatus.Downloaded;
            item.ErrorMessage = null;
            item.UpdatedAtUtc = DateTime.UtcNow;
            _logger.Info($"Direct S3 download completed for {item.LocalPath}");
            return item;
        }
        catch (Exception ex)
        {
            _logger.Error($"Direct S3 download failed for {item.LocalPath}", ex);
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
            if (ex.HResult == ErrorCloudOperationNotUnderSyncRoot)
            {
                return "Placeholder creation is only supported under a registered sync root. Re-initialize the sync root and retry.";
            }
            if (ex.HResult == ErrorAlreadyExists)
            {
                return "A file with the same name already exists in the sync root. Rename/delete the existing file or metadata entry, then retry upload.";
            }
            if (ex.HResult == ErrorCloudProviderNotRunning)
            {
                return "Cloud file provider is not running for this sync root. Re-initialize sync root, upload again, and keep the app running while hydrating.";
            }

            return ex.Message;
        }

        return ex.Message;
    }

    private void TryCreatePlaceholderWithSyncRootRecovery(FileItem item)
    {
        try
        {
            NativeSyncInterop.CreatePlaceholderOrThrow(item.LocalPath, item.SizeBytes, SyncStatus.Synced);
            NativeSyncInterop.NotifyStateOrThrow(item.LocalPath, SyncStatus.Synced);
        }
        catch (NativeSyncException ex) when (ex.HResult == ErrorCloudOperationNotUnderSyncRoot)
        {
            _logger.Info("Sync root appears stale/unregistered. Re-registering sync root and retrying placeholder creation once.");
            Directory.CreateDirectory(_settings.SyncRootPath);
            NativeSyncInterop.RegisterRootOrThrow(_settings.SyncRootPath);
            NativeSyncInterop.ConnectRootOrThrow(_settings.SyncRootPath);
            NativeSyncInterop.CreatePlaceholderOrThrow(item.LocalPath, item.SizeBytes, SyncStatus.Synced);
            NativeSyncInterop.NotifyStateOrThrow(item.LocalPath, SyncStatus.Synced);
        }
        catch (NativeSyncException ex) when (ex.HResult == ErrorCloudProviderNotRunning)
        {
            _logger.Info("Cloud provider was not running during placeholder creation. Reconnecting and retrying once.");
            NativeSyncInterop.ConnectRootOrThrow(_settings.SyncRootPath);
            NativeSyncInterop.CreatePlaceholderOrThrow(item.LocalPath, item.SizeBytes, SyncStatus.Synced);
            NativeSyncInterop.NotifyStateOrThrow(item.LocalPath, SyncStatus.Synced);
        }
        catch (NativeSyncException ex) when (ex.HResult == ErrorAlreadyExists && File.Exists(item.LocalPath))
        {
            _logger.Info($"Placeholder already exists at {item.LocalPath}. Reusing existing file and marking as synced.");
            NativeSyncInterop.NotifyStateOrThrow(item.LocalPath, SyncStatus.Synced);
        }
    }

}
