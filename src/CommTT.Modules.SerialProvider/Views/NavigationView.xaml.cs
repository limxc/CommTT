using System.Windows.Controls;
using CommTT.Modules.SerialProvider.ViewModels;

namespace CommTT.Modules.SerialProvider.Views;

public partial class NavigationView : UserControl
{
    public NavigationView(NavigationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
