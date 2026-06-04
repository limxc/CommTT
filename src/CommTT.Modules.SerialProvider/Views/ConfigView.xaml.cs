using System.Windows.Controls;
using CommTT.Modules.SerialProvider.ViewModels;

namespace CommTT.Modules.SerialProvider.Views;

public partial class ConfigView : UserControl
{
    public ConfigView(ConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
