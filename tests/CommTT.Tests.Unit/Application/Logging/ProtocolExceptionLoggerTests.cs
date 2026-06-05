using CommTT.Application.Logging;

namespace CommTT.Tests.Unit.Application.Logging;

public class ProtocolExceptionLoggerTests : IDisposable
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ProtocolExceptionLoggerTests()
    {
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Write_CreatesFileWithCorrectFormat()
    {
        var dir = Path.Combine(_testDir, DateTime.Now.ToString("yyyyMMdd"));
        var file = Path.Combine(dir, "protocol-exception.txt");

        {
            using var logger = new ProtocolExceptionLogger(_testDir);
            logger.Write("Serial", "COM3", "IOException", "Port unavailable",
                "PortName=COM3,BaudRate=9600", "AABBCC");
        }

        Assert.True(File.Exists(file));

        var lines = File.ReadAllLines(file);
        Assert.Single(lines);
        Assert.Contains("Serial", lines[0]);
        Assert.Contains("COM3", lines[0]);
        Assert.Contains("IOException", lines[0]);
        Assert.Contains("Raw: AABBCC", lines[0]);
    }
}
