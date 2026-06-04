using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace CommTT.Modules.SerialProvider.ViewModels;

public class NavigationViewModel : BindableBase
{
    private readonly IRegionManager _regionManager;
    public DelegateCommand<string> NavigateCommand { get; }

    public NavigationViewModel(IRegionManager regionManager)
    {
        _regionManager = regionManager;
        NavigateCommand = new DelegateCommand<string>(OnNavigate);
    }

    private void OnNavigate(string target) => _regionManager.RequestNavigate("ContentRegion", target);
}
