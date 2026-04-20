using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace FileSync.Core;

public sealed class S3SyncService
{
    private readonly AppSettings _settings;
    private readonly IAppLogger _logger;
    private readonly IAmazonS3 _client;

    public S3SyncService(AppSettings settings, IAppLogger logger)
    {
        _settings = settings;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ServiceURL = settings.ServiceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "us-east-1"
        };
        var creds = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
        _client = new AmazonS3Client(creds, config);
    }

    public async Task UploadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var key = Path.GetFileName(filePath);
        _logger.Info($"Uploading to S3: {key}");
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            FilePath = filePath
        }, cancellationToken);
    }

    public async Task DownloadAsync(string key, string destinationPath, CancellationToken cancellationToken = default)
    {
        _logger.Info($"Downloading from S3: {key}");
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var response = await _client.GetObjectAsync(new GetObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = key
        }, cancellationToken);

        await response.WriteResponseStreamToFileAsync(destinationPath, false, cancellationToken);
    }
}
