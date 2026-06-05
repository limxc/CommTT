using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;

namespace CommTT.Shell;

public partial class App : PrismApplication
{
    private readonly Bootstrapper _bootstrapper = new();
    private static ILoggerFactory? _staticLoggerFactory;

    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
        => _bootstrapper.RegisterTypes(containerRegistry);

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        => _bootstrapper.ConfigureModuleCatalog(moduleCatalog);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Initialize static logger before registering exception handlers
        var logDir = Path.Combine(AppContext.BaseDirectory, "Logs", DateTime.Now.ToString("yyyyMMdd"));
        Directory.CreateDirectory(logDir);
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(logDir, "runtime.txt"),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}");
        var serilogLogger = loggerConfig.CreateLogger();
        _staticLoggerFactory = new LoggerFactory();
        _staticLoggerFactory.AddSerilog(serilogLogger);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var logger = _staticLoggerFactory?.CreateLogger<App>() ?? Container?.Resolve<ILogger<App>>();
            logger?.LogError(e.Exception, "Unhandled UI exception");
        }
        catch { /* avoid recursive exception */ }
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            if (e.ExceptionObject is Exception ex)
            {
                var logger = _staticLoggerFactory?.CreateLogger<App>() ?? Container?.Resolve<ILogger<App>>();
                logger?.LogError(ex, "Unhandled AppDomain exception");
            }
        }
        catch { /* avoid recursive exception */ }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            var logger = _staticLoggerFactory?.CreateLogger<App>() ?? Container?.Resolve<ILogger<App>>();
            logger?.LogError(e.Exception, "Unobserved task exception");
        }
        catch { /* avoid recursive exception */ }
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _staticLoggerFactory?.Dispose();
        base.OnExit(e);
    }
}
