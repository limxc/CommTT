using System.IO.Pipelines;
using System.IO.Ports;
using CommTT.Domain.Events;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;

namespace CommTT.Modules.SerialProvider.Services;

public class SerialCommProvider : ICommProvider
{
    private SerialPort? _port;
    private readonly Pipe _pipe = new Pipe();
    private CancellationTokenSource? _cts;
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public event EventHandler<CommEventArgs>? DataReceived;
    public event EventHandler<ConnectionState>? StateChanged;

    public async Task ConnectAsync(ICommConfig config, CancellationToken ct = default)
    {
        if (config is not SerialConfig sc) throw new ArgumentException("Expected SerialConfig", nameof(config));
        _port = new SerialPort(sc.PortName, sc.BaudRate, sc.Parity, sc.DataBits, sc.StopBits);
        _port.Open();
        State = ConnectionState.Connected;
        StateChanged?.Invoke(this, State);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        _cts?.Cancel();
        if (_port?.IsOpen == true) { _port.Close(); }
        State = ConnectionState.Disconnected;
        StateChanged?.Invoke(this, State);
        await Task.CompletedTask;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_port?.IsOpen != true) throw new InvalidOperationException("Port not open");
        await _port.BaseStream.WriteAsync(data, ct);
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _port?.IsOpen == true)
        {
            var memory = _pipe.Writer.GetMemory(512);
            int read = await _port.BaseStream.ReadAsync(memory, ct);
            if (read == 0) break;
            _pipe.Writer.Advance(read);
            await _pipe.Writer.FlushAsync(ct);
        }
        await _pipe.Writer.CompleteAsync();
    }

    public void Dispose() { _cts?.Cancel(); _port?.Dispose(); _cts?.Dispose(); }
}
