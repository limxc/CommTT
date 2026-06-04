using System.Windows;
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
}
