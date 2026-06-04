using CommTT.Application.Interfaces;
using CommTT.Domain.Events;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;

namespace CommTT.Application.Services;

public class ConnectionManager : IConnectionManager
{
    private ICommProvider? _provider;
    public ConnectionState State => _provider?.State ?? ConnectionState.Disconnected;
    public event EventHandler<CommEventArgs>? DataReceived;
    public event EventHandler<ConnectionState>? StateChanged;

    public void RegisterProvider(ICommProvider provider)
    {
        if (_provider != null)
        {
            _provider.DataReceived -= OnDataReceived;
            _provider.StateChanged -= OnStateChanged;
        }
        _provider = provider;
        _provider.DataReceived += OnDataReceived;
        _provider.StateChanged += OnStateChanged;
    }

    public async Task ConnectAsync(ICommConfig config) => await (_provider?.ConnectAsync(config) ?? Task.CompletedTask);
    public async Task DisconnectAsync() => await (_provider?.DisconnectAsync() ?? Task.CompletedTask);
    public async Task SendAsync(ReadOnlyMemory<byte> data) => await (_provider?.SendAsync(data) ?? Task.CompletedTask);
    private void OnDataReceived(object? s, CommEventArgs e) => DataReceived?.Invoke(this, e);
    private void OnStateChanged(object? s, ConnectionState e) => StateChanged?.Invoke(this, e);
}
