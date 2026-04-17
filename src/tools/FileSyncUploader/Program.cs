using FileSync.Core;

if (args.Length == 0 || !File.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: FileSyncUploader.exe <filePath>");
    return 1;
}

var logger = new FileLogger(Path.Combine(AppContext.BaseDirectory, "FileSyncUploader.log"));
var settings = new AppSettings();
var store = new MetadataStore(Path.Combine(AppContext.BaseDirectory, "filesync-metadata.json"), logger);
var s3 = new S3SyncService(settings, logger);
var coordinator = new SyncCoordinator(settings, s3, store, logger);

try
{
    coordinator.InitializeSyncRoot();
    var item = await coordinator.UploadAndCreatePlaceholderAsync(args[0]);
    var existing = coordinator.LoadItems().ToList();
    existing.RemoveAll(x => x.FileName.Equals(item.FileName, StringComparison.OrdinalIgnoreCase));
    existing.Add(item);
    coordinator.SaveItems(existing);
    Console.WriteLine($"{item.FileName}: {item.Status}");
    return 0;
}
catch (Exception ex)
{
    logger.Error("Context menu upload failed", ex);
    Console.Error.WriteLine(ex.Message);
    return 2;
}
