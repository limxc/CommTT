using CommTT.Application.Interfaces;
using CommTT.Domain.Models;
using Prism.Commands;
using Prism.Mvvm;

namespace CommTT.Modules.SerialProvider.ViewModels;

public class ConfigViewModel : BindableBase
{
    private readonly IConnectionManager _connectionManager;
    private string _portName = "COM1";
    private string _baudRate = "115200";
    private string _status = "Disconnected";

    public string PortName
    {
        get => _portName;
        set => SetProperty(ref _portName, value);
    }

    public string BaudRate
    {
        get => _baudRate;
        set => SetProperty(ref _baudRate, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public DelegateCommand ConnectCommand { get; }
    public DelegateCommand DisconnectCommand { get; }

    public ConfigViewModel(IConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        ConnectCommand = new DelegateCommand(async () =>
        {
            var config = new SerialConfig(PortName, int.Parse(BaudRate));
            await _connectionManager.ConnectAsync(config);
            Status = "Connected";
        });
        DisconnectCommand = new DelegateCommand(async () =>
        {
            await _connectionManager.DisconnectAsync();
            Status = "Disconnected";
        });
    }
}
