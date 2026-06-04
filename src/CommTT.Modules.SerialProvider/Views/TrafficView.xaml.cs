using System.Windows.Controls;
using CommTT.Modules.SerialProvider.ViewModels;

namespace CommTT.Modules.SerialProvider.Views;

public partial class TrafficView : UserControl
{
    public TrafficView(TrafficViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
