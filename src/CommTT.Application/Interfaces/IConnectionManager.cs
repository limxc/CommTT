using CommTT.Domain.Events;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;

namespace CommTT.Application.Interfaces;

public interface IConnectionManager
{
    event EventHandler<CommEventArgs> DataReceived;
    event EventHandler<ConnectionState> StateChanged;
    Task ConnectAsync(ICommConfig config);
    Task DisconnectAsync();
    Task SendAsync(ReadOnlyMemory<byte> data);
    ConnectionState State { get; }
    void RegisterProvider(ICommProvider provider);
}
