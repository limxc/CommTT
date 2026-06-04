using System.Collections.ObjectModel;
using CommTT.Application.Interfaces;
using CommTT.Domain.Models;
using Prism.Mvvm;

namespace CommTT.Modules.SerialProvider.ViewModels;

public class TrafficViewModel : BindableBase
{
    public ObservableCollection<CommDataFrame> Frames { get; } = new();

    public TrafficViewModel(IConnectionManager connectionManager)
    {
        connectionManager.DataReceived += (s, e) =>
            System.Windows.Application.Current.Dispatcher.Invoke(() => Frames.Add(e.Frame));
    }
}
