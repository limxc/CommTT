using System.Text;

namespace CommTT.Application.Logging;

public class ProtocolExceptionLogger : IDisposable
{
    private readonly string _baseDir;
    private readonly Lock _lock = new();
    private string? _currentFile;
    private StreamWriter? _writer;

    public ProtocolExceptionLogger(string baseDir = "")
    {
        _baseDir = string.IsNullOrEmpty(baseDir)
            ? Path.Combine(AppContext.BaseDirectory, "Logs")
            : baseDir;
    }

    public void Write(string protocolType, string connectionId,
        string exceptionType, string message, string context, string rawDataSnapshot)
    {
        EnsureWriter();
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {protocolType} | {connectionId} | {exceptionType} | {message} | {context} | Raw: {rawDataSnapshot}";
        lock (_lock)
        {
            _writer!.WriteLine(line);
            _writer.Flush();
        }
    }

    private void EnsureWriter()
    {
        var dir = Path.Combine(_baseDir, DateTime.Now.ToString("yyyyMMdd"));
        var file = Path.Combine(dir, "protocol-exception.txt");
        if (file != _currentFile)
        {
            Directory.CreateDirectory(dir);
            _writer?.Dispose();
            _writer = new StreamWriter(new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8);
            _currentFile = file;
        }
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _writer = null;
        _currentFile = null;
    }
}
