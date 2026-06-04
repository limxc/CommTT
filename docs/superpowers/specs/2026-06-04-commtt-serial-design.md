---
comet_change: comm-test-platform
role: technical-design
canonical_spec: openspec
---

# CommTT Serial Provider & Core Engine — Technical Design Document

## 1. 设计目标与范围

本设计文档覆盖 **CommTT v1.0** 的技术实现细节：
- Serial（串口）通讯 Provider 的完整实现
- Layer 1 / Layer 2 流式分帧与协议解析架构
- 核心引擎（ConnectionManager、MetricsAggregator、AlertEngine）
- Prism Module 动态加载与 WPF UI 数据流
- SQLite 数据持久化模型
- 完整测试策略（含模糊测试、稳定性测试、BenchmarkDotNet）

**范围边界**：本期仅实现 Serial Provider，TCP/UDP/MQTT/WebSocket/CAN 在架构中预留接口。

---

## 2. ICommProvider 接口设计（Domain 层）

```csharp
public interface ICommProvider : IDisposable, IAsyncDisposable
{
    string ConnectionId { get; }
    string ProviderType { get; }           // "Serial"
    ProviderState State { get; }           // Disconnected, Connecting, Connected, Error, Disposing
    
    // 生命周期
    Task<bool> ConnectAsync(ProviderConfigBase config, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    
    // 发送（纯异步，无阻塞）
    Task<int> SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    
    // 接收：推模式，Provider 内部分包完成后通过事件推送完整帧
    event EventHandler<TransportFrameReceivedEventArgs>? DataReceived;
    event EventHandler<ProtocolFrameReceivedEventArgs>? ProtocolDataReceived;
    
    // 状态与错误
    event EventHandler<ProviderStateChangedEventArgs>? StateChanged;
    event EventHandler<ProviderErrorEventArgs>? ErrorOccurred;
    
    // 指标快照（原子读取，无锁）
    CommMetrics GetMetricsSnapshot();
}

public enum ProviderState
{
    Disconnected,
    Connecting,
    Connected,
    Error,
    Disposing
}

public record TransportFrame(ReadOnlySequence<byte> Payload, DateTimeOffset Timestamp, string ConnectionId);
public record ProtocolFrame(string ProtocolName, object StructuredData, TransportFrame Source);
```

**关键决策**：
- **推模式（Push）**：上层无需轮询，Provider 内部分包完成后直接推送完整帧
- **状态机**：严格状态转换，状态变更通过 `StateChanged` 事件传播到 UI
- **连接 ID**：由 `ConnectionManager` 生成 8 字符 UUID，全局唯一标识
- **双重接收事件**：`DataReceived`（Layer 1 TransportFrame，原始帧）+ `ProtocolDataReceived`（Layer 2 ProtocolFrame，结构化帧）

---

## 3. Serial Provider 实现（Infrastructure 层）

### 3.1 高性能接收管道：System.IO.Pipelines

```csharp
public class SerialProvider : ICommProvider
{
    private readonly Pipe _pipe;
    private readonly Channel<TransportFrame> _frameChannel;
    private readonly ProtocolDispatcher _dispatcher;
    private readonly IFrameSplitter _frameSplitter;
    private readonly CancellationTokenSource _cts = new();
    
    public SerialProvider(IEventAggregator eventAggregator, IFrameSplitter frameSplitter)
    {
        _pipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: 65536,
            resumeWriterThreshold: 32768,
            minimumSegmentSize: 4096,
            useSynchronizationContext: false
        ));
        _frameChannel = Channel.CreateUnbounded<TransportFrame>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
        _dispatcher = new ProtocolDispatcher(eventAggregator);
        _frameSplitter = frameSplitter;
    }
    
    public async Task<bool> ConnectAsync(ProviderConfigBase config, CancellationToken ct)
    {
        var serialConfig = (SerialConfig)config;
        _serialPort = new SerialPort(serialConfig.PortName, serialConfig.BaudRate,
            serialConfig.Parity, serialConfig.DataBits, serialConfig.StopBits);
        _serialPort.Handshake = serialConfig.Handshake;
        _serialPort.Open();
        
        // 并行启动三个任务
        _ = Task.Run(() => FillPipeAsync(_cts.Token), _cts.Token);
        _ = Task.Run(() => SplitFramesAsync(_cts.Token), _cts.Token);
        _ = Task.Run(() => ParseAndDispatchAsync(_cts.Token), _cts.Token);
        
        return true;
    }
    
    // Task 1: 串口 → Pipe（零拷贝写入）
    private async Task FillPipeAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            Memory<byte> buffer = _pipe.Writer.GetMemory(4096);
            try
            {
                int read = await _serialPort.BaseStream.ReadAsync(buffer, ct);
                if (read == 0) break;
                
                _pipe.Writer.Advance(read);
                await _pipe.Writer.FlushAsync(ct);
                Interlocked.Add(ref _rxBytes, read);
            }
            catch (OperationCanceledException) { break; }
            catch (IOException) { break; }  // 串口断开
        }
        await _pipe.Writer.CompleteAsync();
    }
    
    // Task 2: Pipe → 分帧 → Channel
    private async Task SplitFramesAsync(CancellationToken ct)
    {
        await foreach (var frame in _frameSplitter.SplitAsync(_pipe.Reader, ct))
        {
            await _frameChannel.Writer.WriteAsync(frame, ct);
        }
        _frameChannel.Writer.Complete();
    }
    
    // Task 3: Channel → 并行协议解析 → EventAggregator
    private async Task ParseAndDispatchAsync(CancellationToken ct)
    {
        await foreach (var frame in _frameChannel.Reader.ReadAllAsync(ct))
        {
            _dispatcher.Dispatch(frame);  // 零拷贝广播到所有解析器
        }
    }
    
    public async Task<int> SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        await _serialPort.BaseStream.WriteAsync(data, ct);
        await _serialPort.BaseStream.FlushAsync(ct);
        Interlocked.Add(ref _txBytes, data.Length);
        return data.Length;
    }
}
```

**关键决策**：
- **Pipe 内存池**：`System.IO.Pipelines.Pipe` 使用 `ArrayPool<byte>` 复用内存，避免 GC 压力
- **背压控制**：`pauseWriterThreshold = 64KB`，消费端慢时 `FlushAsync` 自动阻塞，内存不爆炸
- **零拷贝分帧**：`ReadOnlySequence<byte>.Slice()` 不产生新内存，只是引用切片
- **三任务并行**：Fill → Split → Parse 形成流水线，最大化吞吐

---

## 4. Layer 1：流式分帧器（IFrameSplitter）

### 4.1 接口定义

```csharp
public interface IFrameSplitter
{
    IAsyncEnumerable<TransportFrame> SplitAsync(PipeReader reader, CancellationToken ct);
}
```

### 4.2 TimeoutSplitter（时间驱动）

```csharp
public class TimeoutSplitter : IFrameSplitter
{
    private readonly TimeSpan _timeout;
    
    public async IAsyncEnumerable<TransportFrame> SplitAsync(
        PipeReader reader,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var accumulator = new SequenceBuilder();
        DateTimeOffset lastDataTime = DateTimeOffset.UtcNow;
        
        using var timeoutCts = new CancellationTokenSource();
        
        while (!ct.IsCancellationRequested)
        {
            timeoutCts.CancelAfter(_timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            
            try
            {
                ReadResult result = await reader.ReadAsync(linkedCts.Token);
                ReadOnlySequence<byte> buffer = result.Buffer;
                
                if (buffer.IsEmpty && result.IsCompleted) break;
                
                accumulator.Append(buffer);
                lastDataTime = DateTimeOffset.UtcNow;
                reader.AdvanceTo(buffer.End);
                
                if (result.IsCompleted) break;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                if (accumulator.Length > 0)
                {
                    var frame = new TransportFrame(
                        accumulator.ToSequence(), lastDataTime, ConnectionId);
                    yield return frame;
                    accumulator.Clear();
                }
                
                timeoutCts.Dispose();
                timeoutCts = new CancellationTokenSource();
            }
        }
        
        if (accumulator.Length > 0)
            yield return new TransportFrame(accumulator.ToSequence(), lastDataTime, ConnectionId);
        
        await reader.CompleteAsync();
    }
}
```

### 4.3 DelimiterSplitter（分隔符驱动）

```csharp
public class DelimiterSplitter : IFrameSplitter
{
    private readonly byte[] _delimiter;
    
    public async IAsyncEnumerable<TransportFrame> SplitAsync(
        PipeReader reader,
        [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ReadResult result = await reader.ReadAsync(ct);
            ReadOnlySequence<byte> buffer = result.Buffer;
            
            SequencePosition? delimPos = buffer.PositionOf(_delimiter);
            
            while (delimPos != null)
            {
                var frame = buffer.Slice(0, delimPos.Value);
                yield return new TransportFrame(frame, DateTimeOffset.UtcNow, ConnectionId);
                
                SequencePosition consumed = buffer.GetPosition(_delimiter.Length, delimPos.Value);
                reader.AdvanceTo(consumed);
                
                buffer = buffer.Slice(consumed);
                delimPos = buffer.PositionOf(_delimiter);
            }
            
            if (result.IsCompleted)
            {
                if (!buffer.IsEmpty)
                    yield return new TransportFrame(buffer, DateTimeOffset.UtcNow, ConnectionId);
                break;
            }
            
            reader.AdvanceTo(buffer.Start, buffer.End);
        }
        
        await reader.CompleteAsync();
    }
}
```

### 4.4 FixedLengthSplitter（固定长度驱动）

```csharp
public class FixedLengthSplitter : IFrameSplitter
{
    private readonly int _frameLength;
    
    public async IAsyncEnumerable<TransportFrame> SplitAsync(
        PipeReader reader,
        [EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ReadResult result = await reader.ReadAsync(ct);
            ReadOnlySequence<byte> buffer = result.Buffer;
            
            while (buffer.Length >= _frameLength)
            {
                var position = buffer.GetPosition(_frameLength);
                var frame = buffer.Slice(0, position);
                yield return new TransportFrame(frame, DateTimeOffset.UtcNow, ConnectionId);
                
                reader.AdvanceTo(position);
                buffer = buffer.Slice(position);
            }
            
            if (result.IsCompleted)
            {
                if (!buffer.IsEmpty)
                    yield return new TransportFrame(buffer, DateTimeOffset.UtcNow, ConnectionId);
                break;
            }
            
            reader.AdvanceTo(buffer.Start, buffer.End);
        }
        
        await reader.CompleteAsync();
    }
}
```

---

## 5. Layer 2：预制协议解析器

### 5.1 基础接口

```csharp
public interface IProtocolParser
{
    string ProtocolName { get; }
    int Priority { get; }           // 高优先级先尝试
    ProtocolFrame? TryParse(TransportFrame frame);
}

public abstract class ProtocolParserBase : IProtocolParser
{
    protected readonly ArrayBufferWriter<byte> _accumulator = new(4096);
    public string ProtocolName { get; protected set; } = string.Empty;
    public int Priority { get; protected set; } = 0;
    
    public ProtocolFrame? TryParse(TransportFrame frame)
    {
        foreach (var segment in frame.Payload)
            _accumulator.Write(segment.Span);
        
        var result = TryParseFrame(_accumulator.WrittenSpan, out int consumed);
        if (result != null && consumed > 0)
        {
            _accumulator.Advance(consumed);
            return result;
        }
        return null;
    }
    
    protected abstract ProtocolFrame? TryParseFrame(ReadOnlySpan<byte> buffer, out int consumed);
}
```

### 5.2 HeaderLengthParser（包头+长度字段）

```csharp
public class HeaderLengthParser : ProtocolParserBase
{
    public int HeaderLength { get; set; } = 2;
    public int LengthFieldOffset { get; set; } = 0;
    public int LengthFieldSize { get; set; } = 2;
    public bool IncludeHeaderInLength { get; set; } = true;
    public Endianness Endian { get; set; } = Endianness.BigEndian;
    public int MaxFrameLength { get; set; } = 1024 * 1024;  // 1MB 防溢出
    
    protected override ProtocolFrame? TryParseFrame(ReadOnlySpan<byte> buffer, out int consumed)
    {
        consumed = 0;
        if (buffer.Length < HeaderLength) return null;
        
        var lengthSpan = buffer.Slice(LengthFieldOffset, LengthFieldSize);
        int payloadLength = ReadLength(lengthSpan);
        
        int totalFrameLength = IncludeHeaderInLength ? payloadLength : HeaderLength + payloadLength;
        if (totalFrameLength > MaxFrameLength)
        {
            consumed = 1;  // 跳过错误包头，重新同步
            return null;
        }
        if (buffer.Length < totalFrameLength) return null;
        
        var frameData = buffer.Slice(0, totalFrameLength).ToArray();
        consumed = totalFrameLength;
        
        return new ProtocolFrame(ProtocolName, new HeaderLengthFrameData
        {
            Header = frameData.AsSpan(0, HeaderLength).ToArray(),
            Payload = frameData.AsSpan(HeaderLength).ToArray(),
            TotalLength = totalFrameLength
        }, new TransportFrame(new ReadOnlySequence<byte>(frameData), DateTimeOffset.UtcNow, string.Empty));
    }
    
    private int ReadLength(ReadOnlySpan<byte> span) => Endian == Endianness.BigEndian
        ? span.Length == 2 ? BinaryPrimitives.ReadUInt16BigEndian(span) : BinaryPrimitives.ReadUInt32BigEndian(span)
        : span.Length == 2 ? BinaryPrimitives.ReadUInt16LittleEndian(span) : BinaryPrimitives.ReadUInt32LittleEndian(span);
}

public record HeaderLengthFrameData(byte[] Header, byte[] Payload, int TotalLength);
```

### 5.3 HeaderFooterParser（包头+包尾）

```csharp
public class HeaderFooterParser : ProtocolParserBase
{
    public byte[] HeaderPattern { get; set; } = Array.Empty<byte>();
    public byte[] FooterPattern { get; set; } = Array.Empty<byte>();
    public int MaxFrameLength { get; set; } = 65535;
    
    protected override ProtocolFrame? TryParseFrame(ReadOnlySpan<byte> buffer, out int consumed)
    {
        consumed = 0;
        
        int headerPos = FindPattern(buffer, HeaderPattern);
        if (headerPos < 0)
        {
            if (buffer.Length > HeaderPattern.Length - 1)
                consumed = buffer.Length - HeaderPattern.Length + 1;
            return null;
        }
        
        int searchStart = headerPos + HeaderPattern.Length;
        int footerPos = FindPattern(buffer.Slice(searchStart), FooterPattern);
        
        if (footerPos < 0)
        {
            consumed = headerPos;
            return null;
        }
        footerPos += searchStart;
        
        int frameLength = footerPos + FooterPattern.Length - headerPos;
        if (frameLength > MaxFrameLength)
        {
            consumed = headerPos + 1;
            return null;
        }
        
        var frameData = buffer.Slice(headerPos, frameLength).ToArray();
        consumed = footerPos + FooterPattern.Length;
        
        return new ProtocolFrame(ProtocolName, new HeaderFooterFrameData
        {
            Header = HeaderPattern,
            Payload = frameData.AsSpan(HeaderPattern.Length, frameLength - HeaderPattern.Length - FooterPattern.Length).ToArray(),
            Footer = FooterPattern
        }, new TransportFrame(new ReadOnlySequence<byte>(frameData), DateTimeOffset.UtcNow, string.Empty));
    }
}

public record HeaderFooterFrameData(byte[] Header, byte[] Payload, byte[] Footer);
```

### 5.4 DelimiterParser（分隔符）

```csharp
public class DelimiterParser : ProtocolParserBase
{
    public byte[] Delimiter { get; set; } = new byte[] { 0x0D, 0x0A };
    public bool IncludeDelimiterInFrame { get; set; } = false;
    public bool DiscardEmptyFrames { get; set; } = true;
    
    protected override ProtocolFrame? TryParseFrame(ReadOnlySpan<byte> buffer, out int consumed)
    {
        consumed = 0;
        int delimPos = FindPattern(buffer, Delimiter);
        if (delimPos < 0) return null;
        
        int payloadLength = IncludeDelimiterInFrame ? delimPos + Delimiter.Length : delimPos;
        if (payloadLength == 0 && DiscardEmptyFrames)
        {
            consumed = delimPos + Delimiter.Length;
            return null;
        }
        
        var frameData = buffer.Slice(0, payloadLength).ToArray();
        consumed = delimPos + Delimiter.Length;
        
        return new ProtocolFrame(ProtocolName, new DelimiterFrameData
        {
            Payload = frameData,
            Delimiter = Delimiter
        }, new TransportFrame(new ReadOnlySequence<byte>(frameData), DateTimeOffset.UtcNow, string.Empty));
    }
}

public record DelimiterFrameData(byte[] Payload, byte[] Delimiter);
```

### 5.5 HeaderFixedLengthParser（包头+固定长度）

```csharp
public class HeaderFixedLengthParser : ProtocolParserBase
{
    public int HeaderLength { get; set; } = 4;
    public int PayloadLength { get; set; } = 256;
    public int? ChecksumLength { get; set; } = 2;
    
    private int TotalFrameLength => HeaderLength + PayloadLength + (ChecksumLength ?? 0);
    
    protected override ProtocolFrame? TryParseFrame(ReadOnlySpan<byte> buffer, out int consumed)
    {
        consumed = 0;
        if (buffer.Length < TotalFrameLength) return null;
        
        var frameData = buffer.Slice(0, TotalFrameLength).ToArray();
        consumed = TotalFrameLength;
        
        int payloadEnd = HeaderLength + PayloadLength;
        var result = new HeaderFixedLengthFrameData
        {
            Header = frameData.AsSpan(0, HeaderLength).ToArray(),
            Payload = frameData.AsSpan(HeaderLength, PayloadLength).ToArray(),
            Checksum = ChecksumLength.HasValue 
                ? frameData.AsSpan(payloadEnd, ChecksumLength.Value).ToArray() : null
        };
        
        if (ChecksumLength.HasValue && !VerifyChecksum(result))
        {
            consumed = 1;
            return null;
        }
        
        return new ProtocolFrame(ProtocolName, result, 
            new TransportFrame(new ReadOnlySequence<byte>(frameData), DateTimeOffset.UtcNow, string.Empty));
    }
    
    private bool VerifyChecksum(HeaderFixedLengthFrameData frame)
    {
        // 具体校验算法由子类或配置指定（如 CRC16、Checksum8）
        return true;  // 默认通过
    }
}

public record HeaderFixedLengthFrameData(byte[] Header, byte[] Payload, byte[]? Checksum);
```

---

## 6. ProtocolDispatcher（Application 层）

```csharp
public class ProtocolDispatcher
{
    private readonly List<IProtocolParser> _parsers = new();
    private readonly IEventAggregator _eventAggregator;
    
    public ProtocolDispatcher(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
    }
    
    public void AddParser(IProtocolParser parser)
    {
        _parsers.Add(parser);
        _parsers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }
    
    public void Dispatch(TransportFrame transportFrame)
    {
        // 1. 总是发布原始帧（Hex 视图）
        _eventAggregator.Publish(new TransportFrameReceivedEventArgs(transportFrame));
        
        // 2. 并行解析（零拷贝广播）
        foreach (var parser in _parsers)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var result = parser.TryParse(transportFrame);
                    if (result != null)
                    {
                        _eventAggregator.Publish(new ProtocolFrameReceivedEventArgs(result));
                    }
                }
                catch (Exception ex)
                {
                    // 解析器异常不应影响其他解析器
                    _eventAggregator.Publish(new ParserErrorEventArgs(parser.ProtocolName, ex));
                }
            });
        }
    }
}
```

---

## 7. ConnectionManager（Application 层）

```csharp
public class ConnectionManager : IConnectionManager
{
    private readonly ConcurrentDictionary<string, ICommProvider> _connections = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly IEventAggregator _eventAggregator;
    
    public async Task<ICommProvider> CreateConnectionAsync(
        string providerType, ProviderConfigBase config, CancellationToken ct = default)
    {
        var factory = _serviceProvider.GetRequiredService<ICommProviderFactory>();
        var provider = factory.Create(providerType);
        
        var connectionId = Guid.NewGuid().ToString("N")[..8];
        if (!_connections.TryAdd(connectionId, provider))
            throw new InvalidOperationException("Connection ID collision");
        
        // 使用 WeakEventManager 防止内存泄漏
        provider.StateChanged += OnProviderStateChanged;
        provider.DataReceived += OnProviderDataReceived;
        provider.ErrorOccurred += OnProviderError;
        
        bool connected = await provider.ConnectAsync(config, ct);
        if (!connected)
        {
            _connections.TryRemove(connectionId, out _);
            throw new ConnectionFailedException(config);
        }
        
        return provider;
    }
    
    public async Task CloseConnectionAsync(string connectionId, CancellationToken ct = default)
    {
        if (_connections.TryRemove(connectionId, out var provider))
        {
            await provider.DisconnectAsync(ct);
            await provider.DisposeAsync();
        }
    }
    
    public IReadOnlyList<ICommProvider> GetAllConnections() => _connections.Values.ToList();
}
```

---

## 8. MetricsAggregator（Application 层）

```csharp
public class MetricsAggregator : IMetricsAggregator
{
    private readonly ConcurrentDictionary<string, ProviderCounters> _counters = new();
    private readonly IEventAggregator _eventAggregator;
    private readonly Timer _timer;
    private readonly TimeSpan _aggregationPeriod = TimeSpan.FromMilliseconds(100);
    
    public MetricsAggregator(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
        _timer = new Timer(_ => AggregateAndPublish(), null, _aggregationPeriod, _aggregationPeriod);
    }
    
    public void ReportTx(string connectionId, int bytes)
        => _counters.AddOrUpdate(connectionId, 
            _ => new ProviderCounters { TxBytes = bytes }, 
            (_, old) => { Interlocked.Add(ref old.TxBytes, bytes); return old; });
    
    public void ReportRx(string connectionId, int bytes, int frames)
        => _counters.AddOrUpdate(connectionId,
            _ => new ProviderCounters { RxBytes = bytes, RxFrames = frames },
            (_, old) => { Interlocked.Add(ref old.RxBytes, bytes); Interlocked.Add(ref old.RxFrames, frames); return old; });
    
    private void AggregateAndPublish()
    {
        foreach (var (connId, counters) in _counters)
        {
            var txSnapshot = Interlocked.Exchange(ref counters.TxBytes, 0);
            var rxSnapshot = Interlocked.Exchange(ref counters.RxBytes, 0);
            var frameSnapshot = Interlocked.Exchange(ref counters.RxFrames, 0);
            var errSnapshot = Interlocked.Exchange(ref counters.ErrorCount, 0);
            
            var metrics = new CommMetrics
            {
                ConnectionId = connId,
                ThroughputBps = (txSnapshot + rxSnapshot) * 10,  // 100ms → 1s
                RxFramesPerSec = frameSnapshot * 10,
                ErrorCount = errSnapshot,
                Timestamp = DateTimeOffset.UtcNow
            };
            
            _eventAggregator.Publish(new MetricsUpdatedEvent(metrics));
        }
    }
}

public class ProviderCounters
{
    public long TxBytes;
    public long RxBytes;
    public long RxFrames;
    public long ErrorCount;
}
```

---

## 9. AlertEngine（Application 层）

```csharp
public class AlertEngine : IAlertEngine
{
    private readonly List<AlertRule> _rules = new();
    private readonly IEventAggregator _eventAggregator;
    
    public AlertEngine(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
        _eventAggregator.GetEvent<MetricsUpdatedEvent>().Subscribe(EvaluateRules, ThreadOption.BackgroundThread);
    }
    
    public void AddRule(AlertRule rule)
    {
        lock (_rules) _rules.Add(rule);
    }
    
    private void EvaluateRules(MetricsUpdatedEvent metrics)
    {
        lock (_rules)
        {
            foreach (var rule in _rules.Where(r => r.Enabled && r.ConnectionId == metrics.ConnectionId))
            {
                try
                {
                    bool triggered = EvaluateExpression(rule.Expression, metrics.Metrics);
                    if (triggered)
                    {
                        _eventAggregator.Publish(new AlertTriggeredEvent
                        {
                            RuleId = rule.Id,
                            ConnectionId = metrics.ConnectionId,
                            Message = rule.Message,
                            Severity = rule.Severity,
                            TriggeredAt = DateTimeOffset.UtcNow
                        });
                    }
                }
                catch (Exception ex)
                {
                    _eventAggregator.Publish(new AlertEvaluationErrorEvent(rule.Id, ex));
                }
            }
        }
    }
    
    // 简单表达式解析：支持 >, <, >=, <=, ==, AND, OR
    private bool EvaluateExpression(string expression, CommMetrics metrics) { /* ... */ }
}
```

---

## 10. SQLite 数据持久化（Infrastructure 层）

```csharp
public class CommDbContext : DbContext
{
    public DbSet<ConnectionConfig> ConnectionConfigs { get; set; }
    public DbSet<TrafficLog> TrafficLogs { get; set; }
    public DbSet<AlertRecord> AlertRecords { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=commtt.db");
}

public class ConnectionConfig
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string JsonConfig { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public class TrafficLog
{
    public Guid Id { get; set; }
    public string ConnectionId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public bool IsTx { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string? TextPreview { get; set; }
    public int DataLength { get; set; }
}

public class AlertRecord
{
    public Guid Id { get; set; }
    public string RuleId { get; set; } = string.Empty;
    public string ConnectionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public DateTimeOffset TriggeredAt { get; set; }
    public bool Acknowledged { get; set; }
}
```

---

## 11. Prism Module 架构与 UI 数据流

### 11.1 模块划分

| 模块 | 项目 | 职责 |
|------|------|------|
| `CommTT.Shell` | WPF 启动项目 | Bootstrapper、主窗口、全局资源、主题 |
| `CommTT.Domain` | .NET 类库 | ICommProvider、DataFrame、CommMetrics、领域事件 |
| `CommTT.Application` | .NET 类库 | ConnectionManager、MetricsAggregator、AlertEngine、用例服务 |
| `CommTT.Infrastructure` | .NET 类库 | EF Core DbContext、SQLite 配置 |
| `CommTT.SerialProvider` | .NET 类库 + Module | SerialProvider + SerialModule + Serial Views/ViewModels |
| `CommTT.CommonUI` | WPF 控件库 | HexViewer、TrafficChart、StatusIndicator |

### 11.2 SerialProviderModule 注册

```csharp
public class SerialProviderModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.Register<ICommProvider, SerialProvider>(nameof(SerialProvider));
        containerRegistry.RegisterForNavigation<SerialConfigView, SerialConfigViewModel>();
        containerRegistry.RegisterForNavigation<SerialTrafficView, SerialTrafficViewModel>();
        containerRegistry.RegisterForNavigation<SerialMonitorView, SerialMonitorViewModel>();
    }
    
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var regionManager = containerProvider.Resolve<IRegionManager>();
        regionManager.RegisterViewWithRegion("NavigationRegion", typeof(SerialNavItemView));
    }
}
```

### 11.3 UI 数据流

```
SerialTrafficView (View)
    └── SerialTrafficViewModel (Prism ViewModel)
            ├── 注入 IConnectionManager
            ├── 订阅 EventAggregator.GetEvent<TransportFrameReceivedEvent>()
            │       └── ObservableCollection<TransportFrame> → UI 绑定 (HexViewer)
            ├── 订阅 EventAggregator.GetEvent<ProtocolFrameReceivedEvent>()
            │       └── ObservableCollection<ProtocolFrame> → UI 绑定 (ProtocolTreeView)
            ├── 订阅 EventAggregator.GetEvent<MetricsUpdatedEvent>()
            │       └── 更新 Throughput、RxFrames → UI 绑定 (TrafficChart)
            └── SendCommand (DelegateCommand)
                    └── ICommProvider.SendAsync() → 返回结果
```

---

## 12. 完整测试策略

### 12.1 测试项目结构

```
tests/
├── CommTT.Domain.Tests/
├── CommTT.Application.Tests/
├── CommTT.SerialProvider.Tests/
│   ├── Layer1/
│   │   ├── TimeoutSplitterTests.cs
│   │   ├── DelimiterSplitterTests.cs
│   │   ├── FixedLengthSplitterTests.cs
│   │   └── Performance/
│   │       ├── BackPressureTests.cs
│   │       └── MemoryStabilityTests.cs
│   ├── Layer2/
│   │   ├── HeaderLengthParserTests.cs
│   │   ├── HeaderFooterParserTests.cs
│   │   ├── DelimiterParserTests.cs
│   │   ├── HeaderFixedLengthParserTests.cs
│   │   └── ParallelParsingTests.cs
│   ├── Fuzzing/
│   │   ├── FrameSplitterFuzzTests.cs
│   │   └── ProtocolParserFuzzTests.cs
│   └── Benchmarks/
│       ├── Layer1Benchmarks.cs
│       ├── Layer2Benchmarks.cs
│       └── EndToEndBenchmarks.cs
└── CommTT.Integration.Tests/
```

### 12.2 MockPipeDataSource（测试基础设施）

```csharp
public class MockPipeDataSource : IDisposable
{
    private readonly Pipe _pipe = new Pipe();
    private readonly byte[] _patternData;
    private readonly CancellationTokenSource _cts = new();
    
    public MockPipeDataSource(int patternLength = 4096, int seed = 42)
    {
        _patternData = new byte[patternLength];
        new Random(seed).NextBytes(_patternData);
    }
    
    public async Task GenerateAsync(TimeSpan duration, double bytesPerSecond, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        long totalBytes = 0;
        long targetBytes = (long)(bytesPerSecond * duration.TotalSeconds);
        
        while (sw.Elapsed < duration && !ct.IsCancellationRequested)
        {
            var elapsedSec = sw.Elapsed.TotalSeconds;
            var expectedBytes = (long)(bytesPerSecond * elapsedSec);
            var batchSize = (int)Math.Min(expectedBytes - totalBytes, 65536);
            
            if (batchSize <= 0) { await Task.Delay(1, ct); continue; }
            
            var memory = _pipe.Writer.GetMemory(batchSize);
            FillPattern(memory, totalBytes);
            _pipe.Writer.Advance(batchSize);
            
            var flushResult = await _pipe.Writer.FlushAsync(ct);
            totalBytes += batchSize;
            
            if (flushResult.IsCompleted) break;
        }
        
        await _pipe.Writer.CompleteAsync();
    }
    
    public PipeReader Reader => _pipe.Reader;
    
    private void FillPattern(Memory<byte> memory, long offset)
    {
        var span = memory.Span;
        for (int i = 0; i < span.Length; i++)
            span[i] = _patternData[(offset + i) % _patternData.Length];
    }
    
    public void Dispose() => _cts.Cancel();
}
```

### 12.3 高压性能测试（3秒 × 100MB/s）

```csharp
[Fact]
public async Task TimeoutSplitter_Should_Handle_100MBps_For_3_Seconds()
{
    await using var source = new MockPipeDataSource();
    var splitter = new TimeoutSplitter(TimeSpan.FromMilliseconds(50));
    var frames = new List<TransportFrame>();
    
    var generateTask = source.GenerateAsync(TimeSpan.FromSeconds(3), 100_000_000);
    var splitTask = Task.Run(async () =>
    {
        await foreach (var frame in splitter.SplitAsync(source.Reader))
            frames.Add(frame);
    });
    
    await Task.WhenAll(generateTask, splitTask);
    
    Assert.InRange(frames.Count, 58, 62);
    var totalPayload = frames.Sum(f => f.Payload.Length);
    Assert.InRange(totalPayload, 290_000_000, 310_000_000);
    
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    Assert.True(Process.GetCurrentProcess().WorkingSet64 < 100_000_000,
        "Memory leak detected");
}
```

### 12.4 稳定性测试（10轮 × 3秒，内存追踪）

```csharp
[Fact]
public async Task Stability_10_Rounds_3_Seconds_Each_Should_Not_Leak_Memory()
{
    var memoryReadings = new List<(int Round, long WorkingSetMB, long GcHeapMB, int Frames, double ElapsedSec)>();
    
    for (int round = 1; round <= 10; round++)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var memBefore = GC.GetTotalMemory(forceFullCollection: true);
        
        await using var source = new MockPipeDataSource();
        var splitter = new TimeoutSplitter(TimeSpan.FromMilliseconds(50));
        var dispatcher = new ProtocolDispatcher(new EventAggregator());
        dispatcher.AddParser(new PassThroughParser());
        
        var sw = Stopwatch.StartNew();
        var frames = 0;
        
        var generateTask = source.GenerateAsync(TimeSpan.FromSeconds(3), 100_000_000);
        var processTask = Task.Run(async () =>
        {
            await foreach (var frame in splitter.SplitAsync(source.Reader))
            {
                dispatcher.Dispatch(frame);
                Interlocked.Increment(ref frames);
            }
        });
        
        await Task.WhenAll(generateTask, processTask);
        sw.Stop();
        
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var memAfter = GC.GetTotalMemory(forceFullCollection: true);
        var workingSet = Process.GetCurrentProcess().WorkingSet64;
        
        memoryReadings.Add((round, workingSet / 1024 / 1024, memAfter / 1024 / 1024, frames, sw.Elapsed.TotalSeconds));
    }
    
    // 断言：内存增长趋势
    for (int i = 1; i < memoryReadings.Count; i++)
    {
        var growth = memoryReadings[i].WorkingSetMB - memoryReadings[i - 1].WorkingSetMB;
        Assert.True(growth < 5, $"Memory growth between round {i} and {i+1}: {growth}MB (leak suspected)");
    }
    
    // 输出报告
    foreach (var r in memoryReadings)
    {
        Console.WriteLine($"Round {r.Round}: WS={r.WorkingSetMB}MB, Heap={r.GcHeapMB}MB, Frames={r.Frames}, Time={r.ElapsedSec:F2}s");
    }
}
```

### 12.5 模糊测试（Fuzzing）

```csharp
public class FuzzTestDataGenerator
{
    private readonly Random _random = new Random();
    
    public byte[] GenerateRandomBytes(int minLength = 0, int maxLength = 1024 * 1024)
    {
        var length = _random.Next(minLength, maxLength);
        var data = new byte[length];
        _random.NextBytes(data);
        return data;
    }
    
    public byte[] GenerateMalformedHeaderLengthFrame()
    {
        // 长度字段 > 实际数据
        var data = new byte[10];
        data[0] = 0xAA; data[1] = 0x55;
        data[2] = 0x00; data[3] = 0xFF;  // Length = 65535，但只剩 6 字节
        return data;
    }
    
    public byte[] GenerateNestedDelimiters()
    {
        // 连续多个 \r\n
        var segments = new List<byte>();
        for (int i = 0; i < 100; i++)
        {
            segments.AddRange(new byte[] { 0x0D, 0x0A });
        }
        return segments.ToArray();
    }
    
    public byte[] GeneratePartialHeaderFooter()
    {
        // 只有包头没有包尾
        var data = new byte[100];
        data[0] = 0x7E;  // Header
        _random.NextBytes(data.AsSpan(1));
        return data;
    }
}

[Theory]
[InlineData(100)]   // 100 轮模糊测试
[InlineData(1000)]  // 1000 轮模糊测试
public void FrameSplitters_Should_Not_Crash_Under_Fuzzed_Input(int iterations)
{
    var fuzzer = new FuzzTestDataGenerator();
    var splitters = new IFrameSplitter[]
    {
        new TimeoutSplitter(TimeSpan.FromMilliseconds(10)),
        new DelimiterSplitter(new byte[] { 0x0D, 0x0A }),
        new FixedLengthSplitter(256)
    };
    
    for (int i = 0; i < iterations; i++)
    {
        var data = fuzzer.GenerateRandomBytes();
        var pipe = new Pipe();
        pipe.Writer.WriteAsync(data).AsTask().Wait();
        pipe.Writer.Complete();
        
        foreach (var splitter in splitters)
        {
            var frames = new List<TransportFrame>();
            var sw = Stopwatch.StartNew();
            
            try
            {
                foreach (var frame in splitter.SplitAsync(pipe.Reader, CancellationToken.None).ToBlockingEnumerable())
                {
                    frames.Add(frame);
                    if (sw.Elapsed > TimeSpan.FromSeconds(5))
                    {
                        Assert.Fail($"Splitter hung on iteration {i}");
                    }
                }
            }
            catch (Exception ex)
            {
                // 记录但继续，某些输入确实可能产生异常，但不能崩溃
                Console.WriteLine($"Exception on iteration {i}: {ex.Message}");
            }
            
            // 无死锁、无无限循环即为通过
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10), "Potential infinite loop detected");
        }
    }
}

[Theory]
[InlineData(100)]
public void ProtocolParsers_Should_Not_Crash_Under_Fuzzed_Input(int iterations)
{
    var fuzzer = new FuzzTestDataGenerator();
    var parsers = new IProtocolParser[]
    {
        new HeaderLengthParser { HeaderLength = 2, LengthFieldOffset = 0, LengthFieldSize = 2 },
        new HeaderFooterParser { HeaderPattern = new byte[] { 0x7E }, FooterPattern = new byte[] { 0x7E } },
        new DelimiterParser { Delimiter = new byte[] { 0x0D, 0x0A } },
        new HeaderFixedLengthParser { HeaderLength = 4, PayloadLength = 256, ChecksumLength = 2 }
    };
    
    for (int i = 0; i < iterations; i++)
    {
        var data = fuzzer.GenerateRandomBytes(0, 10000);
        var transportFrame = new TransportFrame(
            new ReadOnlySequence<byte>(data), DateTimeOffset.UtcNow, "test");
        
        foreach (var parser in parsers)
        {
            try
            {
                var result = parser.TryParse(transportFrame);
                // result 可以为 null，但不能抛异常
            }
            catch (Exception ex)
            {
                Assert.Fail($"Parser {parser.ProtocolName} crashed on iteration {i}: {ex}");
            }
        }
    }
}
```

### 12.6 BenchmarkDotNet 性能基准

```csharp
[MemoryDiagnoser]
[GcServer(true)]
[SimpleJob(RuntimeMoniker.Net80, iterationCount: 10)]
public class Layer1Benchmarks
{
    private Pipe _pipe;
    private byte[] _testData;
    
    [Params(1024, 4096, 65536, 1048576)]  // 1KB, 4KB, 64KB, 1MB
    public int FrameSize;
    
    [GlobalSetup]
    public void Setup()
    {
        _testData = new byte[100 * 1024 * 1024];  // 100MB 测试数据
        new Random(42).NextBytes(_testData);
        _pipe = new Pipe();
    }
    
    [Benchmark]
    public async Task TimeoutSplitter_Throughput()
    {
        var splitter = new TimeoutSplitter(TimeSpan.FromMilliseconds(50));
        _pipe.Writer.WriteAsync(_testData.AsMemory()).AsTask().Wait();
        _pipe.Writer.Complete();
        
        long totalBytes = 0;
        await foreach (var frame in splitter.SplitAsync(_pipe.Reader))
        {
            totalBytes += frame.Payload.Length;
        }
    }
    
    [Benchmark]
    public async Task DelimiterSplitter_Throughput()
    {
        // 每 FrameSize 字节插入 \r\n
        var splitter = new DelimiterSplitter(new byte[] { 0x0D, 0x0A });
        // ... 准备带分隔符的数据 ...
        // ... 运行基准 ...
    }
    
    [Benchmark]
    public async Task FixedLengthSplitter_Throughput()
    {
        var splitter = new FixedLengthSplitter(FrameSize);
        _pipe.Writer.WriteAsync(_testData.AsMemory()).AsTask().Wait();
        _pipe.Writer.Complete();
        
        long totalBytes = 0;
        await foreach (var frame in splitter.SplitAsync(_pipe.Reader))
        {
            totalBytes += frame.Payload.Length;
        }
    }
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, iterationCount: 10)]
public class Layer2Benchmarks
{
    [Params(1000, 10000)]
    public int FrameCount;
    
    private TransportFrame[] _testFrames;
    
    [GlobalSetup]
    public void Setup()
    {
        _testFrames = new TransportFrame[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            var data = new byte[256];
            new Random(i).NextBytes(data);
            _testFrames[i] = new TransportFrame(
                new ReadOnlySequence<byte>(data), DateTimeOffset.UtcNow, "test");
        }
    }
    
    [Benchmark(Baseline = true)]
    public void HeaderLengthParser_Parse()
    {
        var parser = new HeaderLengthParser();
        foreach (var frame in _testFrames)
            parser.TryParse(frame);
    }
    
    [Benchmark]
    public void HeaderFooterParser_Parse()
    {
        var parser = new HeaderFooterParser();
        foreach (var frame in _testFrames)
            parser.TryParse(frame);
    }
    
    [Benchmark]
    public void DelimiterParser_Parse()
    {
        var parser = new DelimiterParser();
        foreach (var frame in _testFrames)
            parser.TryParse(frame);
    }
    
    [Benchmark]
    public void HeaderFixedLengthParser_Parse()
    {
        var parser = new HeaderFixedLengthParser();
        foreach (var frame in _testFrames)
            parser.TryParse(frame);
    }
}

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80, iterationCount: 5)]
public class EndToEndBenchmarks
{
    [Benchmark]
    public async Task SerialProvider_Pipeline_100MB()
    {
        await using var source = new MockPipeDataSource();
        var splitter = new TimeoutSplitter(TimeSpan.FromMilliseconds(50));
        var dispatcher = new ProtocolDispatcher(new EventAggregator());
        dispatcher.AddParser(new PassThroughParser());
        
        var generateTask = source.GenerateAsync(TimeSpan.FromSeconds(1), 100_000_000);
        var processTask = Task.Run(async () =>
        {
            await foreach (var frame in splitter.SplitAsync(source.Reader))
                dispatcher.Dispatch(frame);
        });
        
        await Task.WhenAll(generateTask, processTask);
    }
}
```

### 12.7 测试运行与报告

```bash
# 运行所有测试（含高压/稳定性/模糊测试，耗时较长）
dotnet test --filter "FullyQualifiedName~SerialProvider" --logger "console;verbosity=detailed"

# 运行 BenchmarkDotNet（生成 Markdown/JSON 报告）
dotnet run --project tests/CommTT.SerialProvider.Benchmarks -c Release
# 输出：BenchmarkDotNet.Artifacts/results/*.md
```

---

## 13. 关键设计决策总结

| 维度 | 决策 |
|------|------|
| **接收缓冲区** | `System.IO.Pipelines.Pipe`（内存池 + 背压控制） |
| **Layer 1 分帧** | `IFrameSplitter` 异步流式接口：`Timeout` / `Delimiter` / `FixedLength` |
| **Layer 2 解析** | 4 种预制解析器：`HeaderLength` / `HeaderFooter` / `Delimiter` / `HeaderFixedLength` |
| **多解析器并行** | `ProtocolDispatcher` 零拷贝广播，各解析器独立 `Task` 运行 |
| **并发模型** | `ConcurrentDictionary<string, ICommProvider>` + `Interlocked` 原子计数 |
| **UI 架构** | Prism `Region` 导航 + MaterialDesignInXamlToolkit + 模块动态加载 |
| **数据持久化** | SQLite + EF Core Code First，配置 JSON 序列化 |
| **测试** | xUnit + 模糊测试（Fuzzing）+ 10轮稳定性内存追踪 + BenchmarkDotNet 性能基准 |
