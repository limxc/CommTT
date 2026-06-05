using System.IO;
using CommTT.Application.Interfaces;
using CommTT.Application.Services;
using CommTT.Infrastructure.Data;
using CommTT.Application.Logging;
using CommTT.Modules.SerialProvider;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;

namespace CommTT.Shell;

public class Bootstrapper
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IConnectionManager, ConnectionManager>();
        containerRegistry.RegisterSingleton<IMetricsAggregator, MetricsAggregator>();
        containerRegistry.RegisterSingleton<IAlertEngine, AlertEngine>();
        containerRegistry.RegisterSingleton<AppDbContext>();

        // Configure Serilog runtime log
        var logDir = Path.Combine(AppContext.BaseDirectory, "Logs", DateTime.Now.ToString("yyyyMMdd"));
        Directory.CreateDirectory(logDir);

        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(logDir, "runtime.txt"),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}")
            .CreateLogger();

        // Register M.E.L factory with Serilog
        var loggerFactory = new LoggerFactory();
        loggerFactory.AddSerilog(logger);
        containerRegistry.RegisterInstance<ILoggerFactory>(loggerFactory);

        // Register generic ILogger<T> factory
        containerRegistry.Register(typeof(ILogger<>), typeof(Logger<>));

        // Register custom audit loggers
        containerRegistry.RegisterSingleton<ProtocolExceptionLogger>();
        containerRegistry.RegisterSingleton<ProtocolStateLogger>();
    }

    public void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<SerialProviderModule>();
    }
}
