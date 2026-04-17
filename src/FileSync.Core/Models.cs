namespace FileSync.Core;

public enum SyncStatus
{
    Pending,
    Synced,
    Downloaded,
    Failed
}

public sealed class FileItem
{
    public string FileName { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Pending;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}

public sealed class AppSettings
{
    public string SyncRootPath { get; set; } = @"C:\CFAPI\SyncRoot";
    public string BucketName { get; set; } = "cfapi-files";
    public string ServiceUrl { get; set; } = "http://localhost:4566";
    public string AccessKey { get; set; } = "test";
    public string SecretKey { get; set; } = "test";
}
