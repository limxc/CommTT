using System.Windows.Controls;
using CommTT.Modules.SerialProvider.ViewModels;

namespace CommTT.Modules.SerialProvider.Views;

public partial class MonitorView : UserControl
{
    public MonitorView(MonitorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
