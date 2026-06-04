using CommTT.Domain.Interfaces;
using CommTT.Modules.SerialProvider.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;

namespace CommTT.Modules.SerialProvider;

public class SerialProviderModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var regionManager = containerProvider.Resolve<IRegionManager>();
        regionManager.RegisterViewWithRegion("NavigationRegion", typeof(NavigationView));
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<ICommProvider, Services.SerialCommProvider>();
        containerRegistry.RegisterForNavigation<ConfigView>("SerialConfig");
        containerRegistry.RegisterForNavigation<TrafficView>("SerialTraffic");
        containerRegistry.RegisterForNavigation<MonitorView>("SerialMonitor");
    }
}
