namespace FileSync.Core;

public interface IAppLogger
{
    void Info(string message);
    void Error(string message, Exception? ex = null);
}

public sealed class FileLogger : IAppLogger
{
    private readonly string _path;
    private readonly object _gate = new();

    public FileLogger(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
    }

    public void Info(string message) => Write("INFO", message);

    public void Error(string message, Exception? ex = null) =>
        Write("ERROR", $"{message}{Environment.NewLine}{ex}");

    private void Write(string level, string message)
    {
        lock (_gate)
        {
            File.AppendAllText(
                _path,
                $"{DateTime.UtcNow:O} [{level}] {message}{Environment.NewLine}");
        }
    }
}
