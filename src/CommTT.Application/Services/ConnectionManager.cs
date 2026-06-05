using CommTT.Application.Interfaces;
using CommTT.Application.Logging;
using CommTT.Domain.Events;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;
using Microsoft.Extensions.Logging;

namespace CommTT.Application.Services;

public class ConnectionManager : IConnectionManager
{
    private ICommProvider? _provider;
    public ConnectionState State => _provider?.State ?? ConnectionState.Disconnected;
    public event EventHandler<CommEventArgs>? DataReceived;
    public event EventHandler<ConnectionState>? StateChanged;

    private readonly ILogger<ConnectionManager> _logger;
    private readonly ProtocolStateLogger _stateLogger;

    public ConnectionManager(ILogger<ConnectionManager> logger, ProtocolStateLogger stateLogger)
    {
        _logger = logger;
        _stateLogger = stateLogger;
    }

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

    private void OnStateChanged(object? s, ConnectionState e)
    {
        var oldState = State;
        _stateLogger.Write("Unknown", "", oldState, e, "ProviderEvent", $"Provider state changed to {e}");
        _logger.LogInformation("Connection state changed from {OldState} to {NewState}", oldState, e);
        StateChanged?.Invoke(this, e);
    }
}
