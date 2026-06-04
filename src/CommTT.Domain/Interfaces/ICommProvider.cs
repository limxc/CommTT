using CommTT.Domain.Events;
using CommTT.Domain.Models;

namespace CommTT.Domain.Interfaces;

public interface ICommProvider : IDisposable
{
    event EventHandler<CommEventArgs> DataReceived;
    event EventHandler<ConnectionState> StateChanged;
    Task ConnectAsync(ICommConfig config, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    ConnectionState State { get; }
}
