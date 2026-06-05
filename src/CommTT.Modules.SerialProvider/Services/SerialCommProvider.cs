using System.IO.Pipelines;
using System.IO.Ports;
using CommTT.Domain.Events;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;
using CommTT.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace CommTT.Modules.SerialProvider.Services;

public class SerialCommProvider : ICommProvider
{
    private SerialPort? _port;
    private readonly Pipe _pipe = new Pipe();
    private CancellationTokenSource? _cts;
    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;
    public event EventHandler<CommEventArgs>? DataReceived;
    public event EventHandler<ConnectionState>? StateChanged;

    private readonly ILogger<SerialCommProvider> _logger;
    private readonly ProtocolExceptionLogger _exceptionLogger;
    private readonly ProtocolStateLogger _stateLogger;

    public SerialCommProvider(ILogger<SerialCommProvider> logger,
        ProtocolExceptionLogger exceptionLogger,
        ProtocolStateLogger stateLogger)
    {
        _logger = logger;
        _exceptionLogger = exceptionLogger;
        _stateLogger = stateLogger;
    }

    public async Task ConnectAsync(ICommConfig config, CancellationToken ct = default)
    {
        if (config is not SerialConfig sc)
            throw new ArgumentException("Expected SerialConfig", nameof(config));

        try
        {
            _port = new SerialPort(sc.PortName, sc.BaudRate, sc.Parity, sc.DataBits, sc.StopBits);
            _port.Open();
            var oldState = State;
            State = ConnectionState.Connected;
            StateChanged?.Invoke(this, State);
            _stateLogger.Write("Serial", sc.PortName, oldState, ConnectionState.Connected, "UserAction", $"Port opened at {sc.BaudRate} baud");
            _logger.LogInformation("Serial port {PortName} opened at {BaudRate} baud", sc.PortName, sc.BaudRate);

            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _ = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            _exceptionLogger.Write("Serial", sc.PortName, ex.GetType().Name, ex.Message,
                $"PortName={sc.PortName},BaudRate={sc.BaudRate},Parity={sc.Parity},DataBits={sc.DataBits}", "");
            _logger.LogError(ex, "Failed to open serial port {PortName}", sc.PortName);
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        var portName = _port?.PortName ?? "unknown";
        try
        {
            _cts?.Cancel();
            if (_port?.IsOpen == true) { _port.Close(); }
            var oldState = State;
            State = ConnectionState.Disconnected;
            StateChanged?.Invoke(this, State);
            _stateLogger.Write("Serial", portName, oldState, ConnectionState.Disconnected, "UserAction", "Port closed by user");
            _logger.LogInformation("Serial port {PortName} closed", portName);
        }
        catch (Exception ex)
        {
            _exceptionLogger.Write("Serial", portName, ex.GetType().Name, ex.Message, "", "");
            _logger.LogError(ex, "Error during disconnect on {PortName}", portName);
            throw;
        }
        await Task.CompletedTask;
    }

    public async Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_port?.IsOpen != true)
            throw new InvalidOperationException("Port not open");
        try
        {
            await _port.BaseStream.WriteAsync(data, ct);
        }
        catch (Exception ex)
        {
            var rawHex = Convert.ToHexString(data.Span.Slice(0, Math.Min(data.Length, 64)));
            _exceptionLogger.Write("Serial", _port.PortName, ex.GetType().Name, ex.Message,
                $"PortName={_port.PortName}", rawHex);
            _logger.LogError(ex, "Send failed on {PortName}", _port.PortName);
            throw;
        }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var portName = _port?.PortName ?? "unknown";
        try
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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _exceptionLogger.Write("Serial", portName, ex.GetType().Name, ex.Message, $"PortName={portName}", "");
            _logger.LogError(ex, "Read loop error on {PortName}", portName);
            var oldState = State;
            State = ConnectionState.Error;
            StateChanged?.Invoke(this, State);
            _stateLogger.Write("Serial", portName, oldState, ConnectionState.Error, "IoException", $"Read loop terminated: {ex.Message}");
            throw;
        }
    }

    public void Dispose() { _cts?.Cancel(); _port?.Dispose(); _cts?.Dispose(); }
}
