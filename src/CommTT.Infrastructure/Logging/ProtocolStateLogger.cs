using System.Text;
using CommTT.Domain.Models;

namespace CommTT.Infrastructure.Logging;

public class ProtocolStateLogger
{
    private readonly string _baseDir;
    private readonly Lock _lock = new();
    private string? _currentFile;
    private StreamWriter? _writer;

    public ProtocolStateLogger(string baseDir = "")
    {
        _baseDir = string.IsNullOrEmpty(baseDir)
            ? Path.Combine(AppContext.BaseDirectory, "Logs")
            : baseDir;
    }

    public void Write(string protocolType, string connectionId,
        ConnectionState oldState, ConnectionState newState, string triggerReason, string details)
    {
        EnsureWriter();
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {protocolType} | {connectionId} | {oldState} → {newState} | {triggerReason} | {details}";
        lock (_lock)
        {
            _writer!.WriteLine(line);
            _writer.Flush();
        }
    }

    private void EnsureWriter()
    {
        var dir = Path.Combine(_baseDir, DateTime.Now.ToString("yyyyMMdd"));
        var file = Path.Combine(dir, "protocol-state.txt");
        if (file != _currentFile)
        {
            Directory.CreateDirectory(dir);
            _writer?.Dispose();
            _writer = new StreamWriter(file, append: true, encoding: Encoding.UTF8);
            _currentFile = file;
        }
    }
}
