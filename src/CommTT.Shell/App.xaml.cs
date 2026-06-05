using System.Windows;
using Microsoft.Extensions.Logging;
using Prism.DryIoc;
using Prism.Ioc;
using Prism.Modularity;

namespace CommTT.Shell;

public partial class App : PrismApplication
{
    private readonly Bootstrapper _bootstrapper = new();

    protected override Window CreateShell() => Container.Resolve<MainWindow>();

    protected override void RegisterTypes(IContainerRegistry containerRegistry)
        => _bootstrapper.RegisterTypes(containerRegistry);

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        => _bootstrapper.ConfigureModuleCatalog(moduleCatalog);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var logger = Container?.Resolve<ILogger<App>>();
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
                var logger = Container?.Resolve<ILogger<App>>();
                logger?.LogError(ex, "Unhandled AppDomain exception");
            }
        }
        catch { /* avoid recursive exception */ }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            var logger = Container?.Resolve<ILogger<App>>();
            logger?.LogError(e.Exception, "Unobserved task exception");
        }
        catch { /* avoid recursive exception */ }
        e.SetObserved();
    }
}
