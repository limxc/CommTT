using CommTT.Application.Interfaces;
using CommTT.Application.Services;
using CommTT.Infrastructure.Data;
using CommTT.Modules.SerialProvider;
using Prism.Ioc;
using Prism.Modularity;

namespace CommTT.Shell;

public class Bootstrapper
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IConnectionManager, ConnectionManager>();
        containerRegistry.RegisterSingleton<IMetricsAggregator, MetricsAggregator>();
        containerRegistry.RegisterSingleton<IAlertEngine, AlertEngine>();
        containerRegistry.RegisterSingleton<AppDbContext>();
    }

    public void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<SerialProviderModule>();
    }
}
