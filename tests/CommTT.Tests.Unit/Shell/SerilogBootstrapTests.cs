using Serilog;

namespace CommTT.Tests.Unit.Shell;

public class SerilogBootstrapTests : IDisposable
{
    private readonly string _testLogDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_testLogDir))
            Directory.Delete(_testLogDir, true);
    }

    [Fact]
    public void SerilogFileSink_CreatesRuntimeLog()
    {
        var logFile = Path.Combine(_testLogDir, "runtime.txt");
        Directory.CreateDirectory(_testLogDir);

        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logFile, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}")
            .CreateLogger();

        logger.Information("Test bootstrap log");
        logger.Dispose();

        Assert.True(File.Exists(logFile));
        var content = File.ReadAllText(logFile);
        Assert.Contains("Test bootstrap log", content);
    }
}
