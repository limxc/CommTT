using CommTT.Application.Logging;
using CommTT.Domain.Models;

namespace CommTT.Tests.Unit.Application.Logging;

public class ProtocolStateLoggerTests : IDisposable
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ProtocolStateLoggerTests() => Directory.CreateDirectory(_testDir);

    public void Dispose()
    {
        if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Write_CreatesFileWithArrowSeparator()
    {
        var dir = Path.Combine(_testDir, DateTime.Now.ToString("yyyyMMdd"));
        var file = Path.Combine(dir, "protocol-state.txt");

        {
            using var logger = new ProtocolStateLogger(_testDir);
            logger.Write("Serial", "COM3", ConnectionState.Disconnected, ConnectionState.Connected, "UserAction", "Opened");
        }

        Assert.True(File.Exists(file));

        var lines = File.ReadAllLines(file);
        Assert.Single(lines);
        Assert.Contains("Disconnected → Connected", lines[0]);
        Assert.Contains("UserAction", lines[0]);
    }
}
