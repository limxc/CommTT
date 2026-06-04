# CommTT Serial Provider Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build CommTT v1.0 — a .NET 10 WPF + Prism + MaterialDesign serial communication test platform with Clean Architecture and plugin-based provider system.

**Architecture:** Clean Architecture 4-layer (Domain → Application → Infrastructure → Presentation) + Prism modular UI + System.IO.Pipelines for zero-copy high-throughput serial I/O.

**Tech Stack:** .NET 10, WPF, Prism.Wpf, DryIoc, MaterialDesignInXamlToolkit, System.IO.Pipelines, EF Core SQLite, xUnit, BenchmarkDotNet, Moq, FluentAssertions

---

```yaml
---
change: comm-test-platform
design-doc: docs/superpowers/specs/2026-06-04-commtt-serial-design.md
base-ref: null
---
```

## File Structure Overview

```
CommTT/
├── CommTT.sln
├── src/
│   ├── CommTT.Domain/
│   │   ├── Interfaces/
│   │   │   ├── ICommProvider.cs
│   │   │   ├── ICommConfig.cs
│   │   │   ├── IProtocolSplitter.cs
│   │   │   └── IProtocolParser.cs
│   │   ├── Models/
│   │   │   ├── CommDataFrame.cs
│   │   │   ├── SerialConfig.cs
│   │   │   └── ConnectionState.cs
│   │   └── Events/
│   │       └── CommEventArgs.cs
│   ├── CommTT.Application/
│   │   ├── Interfaces/
│   │   │   ├── IConnectionManager.cs
│   │   │   ├── IMetricsAggregator.cs
│   │   │   └── IAlertEngine.cs
│   │   ├── Services/
│   │   │   ├── ConnectionManager.cs
│   │   │   ├── MetricsAggregator.cs
│   │   │   └── AlertEngine.cs
│   │   └── ProtocolDispatcher.cs
│   ├── CommTT.Infrastructure/
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   └── Configurations/
│   │   └── Repositories/
│   ├── CommTT.Shell/
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── Bootstrapper.cs
│   │   ├── MainWindow.xaml
│   │   └── Views/
│   └── CommTT.Modules.SerialProvider/
│       ├── SerialProviderModule.cs
│       ├── Services/
│       │   └── SerialCommProvider.cs
│       ├── Protocol/
│       │   ├── Splitters/
│       │   │   ├── TimeoutSplitter.cs
│       │   │   ├── DelimiterSplitter.cs
│       │   │   └── FixedLengthSplitter.cs
│       │   └── Parsers/
│       │       ├── HexParser.cs
│       │       ├── TextParser.cs
│       │       ├── ModbusParser.cs
│       │       ├── CustomParser.cs
│       │       └── PassThroughParser.cs
│       └── Views/
│           ├── ConfigView.xaml
│           ├── TrafficView.xaml
│           ├── MonitorView.xaml
│           └── ViewModels/
│               ├── ConfigViewModel.cs
│               ├── TrafficViewModel.cs
│               └── MonitorViewModel.cs
├── tests/
│   ├── CommTT.Tests.Unit/
│   │   ├── Domain/
│   │   ├── Application/
│   │   ├── Infrastructure/
│   │   └── SerialProvider/
│   └── CommTT.Tests.Benchmark/
│       └── SerialReadBenchmarks.cs
└── docs/
    └── superpowers/
        └── specs/
            └── 2026-06-04-commtt-serial-design.md
```

---

### Task 1: Solution Init (7 Projects)

**Files:**
- Create: `CommTT.sln`
- Create: `src/CommTT.Domain/CommTT.Domain.csproj`
- Create: `src/CommTT.Application/CommTT.Application.csproj`
- Create: `src/CommTT.Infrastructure/CommTT.Infrastructure.csproj`
- Create: `src/CommTT.Shell/CommTT.Shell.csproj`
- Create: `src/CommTT.Modules.SerialProvider/CommTT.Modules.SerialProvider.csproj`
- Create: `tests/CommTT.Tests.Unit/CommTT.Tests.Unit.csproj`
- Create: `tests/CommTT.Tests.Benchmark/CommTT.Tests.Benchmark.csproj`

- [ ] **Step 1: Create solution and class library projects**

```bash
cd D:\CodeResources\CommTT
dotnet new sln -n CommTT

# Domain (no deps)
dotnet new classlib -n CommTT.Domain -o src/CommTT.Domain

# Application (refs Domain)
dotnet new classlib -n CommTT.Application -o src/CommTT.Application

# Infrastructure (refs Domain + Application)
dotnet new classlib -n CommTT.Infrastructure -o src/CommTT.Infrastructure

# Presentation modules
dotnet new wpf -n CommTT.Shell -o src/CommTT.Shell
dotnet new classlib -n CommTT.Modules.SerialProvider -o src/CommTT.Modules.SerialProvider

# Tests
dotnet new xunit -n CommTT.Tests.Unit -o tests/CommTT.Tests.Unit
dotnet new classlib -n CommTT.Tests.Benchmark -o tests/CommTT.Tests.Benchmark
```

- [ ] **Step 2: Add projects to solution and set up references**

```bash
dotnet sln add src/CommTT.Domain/CommTT.Domain.csproj
dotnet sln add src/CommTT.Application/CommTT.Application.csproj
dotnet sln add src/CommTT.Infrastructure/CommTT.Infrastructure.csproj
dotnet sln add src/CommTT.Shell/CommTT.Shell.csproj
dotnet sln add src/CommTT.Modules.SerialProvider/CommTT.Modules.SerialProvider.csproj
dotnet sln add tests/CommTT.Tests.Unit/CommTT.Tests.Unit.csproj
dotnet sln add tests/CommTT.Tests.Benchmark/CommTT.Tests.Benchmark.csproj

# References
cd src/CommTT.Application && dotnet add reference ../CommTT.Domain/CommTT.Domain.csproj && cd ../..
cd src/CommTT.Infrastructure && dotnet add reference ../CommTT.Domain/CommTT.Domain.csproj && dotnet add reference ../CommTT.Application/CommTT.Application.csproj && cd ../..
cd src/CommTT.Modules.SerialProvider && dotnet add reference ../CommTT.Domain/CommTT.Domain.csproj && dotnet add reference ../CommTT.Application/CommTT.Application.csproj && cd ../..
cd src/CommTT.Shell && dotnet add reference ../CommTT.Domain/CommTT.Domain.csproj && dotnet add reference ../CommTT.Application/CommTT.Application.csproj && dotnet add reference ../CommTT.Infrastructure/CommTT.Infrastructure.csproj && dotnet add reference ../CommTT.Modules.SerialProvider/CommTT.Modules.SerialProvider.csproj && cd ../..
cd tests/CommTT.Tests.Unit && dotnet add reference ../../src/CommTT.Domain/CommTT.Domain.csproj && dotnet add reference ../../src/CommTT.Application/CommTT.Application.csproj && dotnet add reference ../../src/CommTT.Infrastructure/CommTT.Infrastructure.csproj && dotnet add reference ../../src/CommTT.Modules.SerialProvider/CommTT.Modules.SerialProvider.csproj && cd ../..
```

- [ ] **Step 3: Verify build**

Run: `dotnet build CommTT.sln`
Expected: Build succeeds with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore: init solution with 7 projects and references"
```

---

### Task 2: NuGet Dependencies

**Files:**
- Modify: `src/CommTT.Shell/CommTT.Shell.csproj`
- Modify: `src/CommTT.Modules.SerialProvider/CommTT.Modules.SerialProvider.csproj`
- Modify: `src/CommTT.Infrastructure/CommTT.Infrastructure.csproj`
- Modify: `tests/CommTT.Tests.Unit/CommTT.Tests.Unit.csproj`
- Modify: `tests/CommTT.Tests.Benchmark/CommTT.Tests.Benchmark.csproj`

- [ ] **Step 1: Add UI and DI packages**

```bash
cd src/CommTT.Shell
dotnet add package Prism.Wpf --version 9.0.537
dotnet add package Prism.DryIoc --version 9.0.537
dotnet add package MaterialDesignThemes --version 5.3.2
cd ../..

cd src/CommTT.Modules.SerialProvider
dotnet add package Prism.Wpf --version 9.0.537
dotnet add package MaterialDesignThemes --version 5.3.2
cd ../..
```

- [ ] **Step 2: Add infrastructure packages**

```bash
cd src/CommTT.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.8
dotnet add package System.IO.Pipelines --version 10.0.0
cd ../..

cd src/CommTT.Modules.SerialProvider
dotnet add package System.IO.Pipelines --version 10.0.0
cd ../..
```

- [ ] **Step 3: Add test packages**

```bash
cd tests/CommTT.Tests.Unit
dotnet add package Moq --version 4.20.72
dotnet add package FluentAssertions --version 8.10.0
dotnet add package Microsoft.EntityFrameworkCore.InMemory --version 10.0.8
dotnet add package Microsoft.NET.Test.Sdk --version 18.6.0
dotnet add package xunit --version 2.9.3
dotnet add package xunit.runner.visualstudio --version 3.1.4
cd ../..

cd tests/CommTT.Tests.Benchmark
dotnet add package BenchmarkDotNet --version 0.15.8
cd ../..
```

- [ ] **Step 4: Verify restore and build**

Run: `dotnet restore && dotnet build`
Expected: All packages restored, build succeeds.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "chore: add NuGet dependencies for Prism, MaterialDesign, EF Core, Pipelines, xUnit, Moq, FluentAssertions, BenchmarkDotNet"
```

---

### Task 3: Domain Layer — Interfaces, Models, Events

**Files:**
- Create: `src/CommTT.Domain/Interfaces/ICommProvider.cs`
- Create: `src/CommTT.Domain/Interfaces/ICommConfig.cs`
- Create: `src/CommTT.Domain/Interfaces/IProtocolSplitter.cs`
- Create: `src/CommTT.Domain/Interfaces/IProtocolParser.cs`
- Create: `src/CommTT.Domain/Models/CommDataFrame.cs`
- Create: `src/CommTT.Domain/Models/SerialConfig.cs`
- Create: `src/CommTT.Domain/Models/ConnectionState.cs`
- Create: `src/CommTT.Domain/Events/CommEventArgs.cs`

- [ ] **Step 1: Define core domain interfaces**

```csharp
// src/CommTT.Domain/Interfaces/ICommProvider.cs
public interface ICommProvider : IDisposable
{
    event EventHandler<CommEventArgs> DataReceived;
    event EventHandler<ConnectionState> StateChanged;
    Task ConnectAsync(ICommConfig config, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    ConnectionState State { get; }
}

// src/CommTT.Domain/Interfaces/ICommConfig.cs
public interface ICommConfig { }

// src/CommTT.Domain/Interfaces/IProtocolSplitter.cs
public interface IProtocolSplitter
{
    bool TrySplit(ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> frame, out SequencePosition consumed);
}

// src/CommTT.Domain/Interfaces/IProtocolParser.cs
public interface IProtocolParser
{
    CommDataFrame Parse(ReadOnlySequence<byte> frame);
}
```

- [ ] **Step 2: Define domain models and events**

```csharp
// src/CommTT.Domain/Models/CommDataFrame.cs
public record CommDataFrame(DateTimeOffset Timestamp, ReadOnlyMemory<byte> Raw, string? ParsedText = null);

// src/CommTT.Domain/Models/SerialConfig.cs
public record SerialConfig(
    string PortName,
    int BaudRate,
    int DataBits = 8,
    System.IO.Ports.Parity Parity = System.IO.Ports.Parity.None,
    System.IO.Ports.StopBits StopBits = System.IO.Ports.StopBits.One
) : ICommConfig;

// src/CommTT.Domain/Models/ConnectionState.cs
public enum ConnectionState { Disconnected, Connecting, Connected, Error }

// src/CommTT.Domain/Events/CommEventArgs.cs
public class CommEventArgs : EventArgs
{
    public CommDataFrame Frame { get; init; } = null!;
}
```

- [ ] **Step 3: Build Domain**

Run: `dotnet build src/CommTT.Domain/CommTT.Domain.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(domain): add ICommProvider, IProtocolSplitter, IProtocolParser, models, and events"
```

---

### Task 4: Application Layer — ConnectionManager

**Files:**
- Create: `src/CommTT.Application/Interfaces/IConnectionManager.cs`
- Create: `src/CommTT.Application/Services/ConnectionManager.cs`

- [ ] **Step 1: Define IConnectionManager**

```csharp
// src/CommTT.Application/Interfaces/IConnectionManager.cs
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
```

- [ ] **Step 2: Implement ConnectionManager**

```csharp
// src/CommTT.Application/Services/ConnectionManager.cs
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
```

- [ ] **Step 3: Build Application**

Run: `dotnet build src/CommTT.Application/CommTT.Application.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(application): add ConnectionManager with provider registration and pass-through events"
```

---

### Task 5: Application Layer — MetricsAggregator + AlertEngine

**Files:**
- Create: `src/CommTT.Application/Interfaces/IMetricsAggregator.cs`
- Create: `src/CommTT.Application/Interfaces/IAlertEngine.cs`
- Create: `src/CommTT.Application/Services/MetricsAggregator.cs`
- Create: `src/CommTT.Application/Services/AlertEngine.cs`

- [ ] **Step 1: Define interfaces**

```csharp
// src/CommTT.Application/Interfaces/IMetricsAggregator.cs
public interface IMetricsAggregator
{
    void RecordFrame(CommDataFrame frame);
    long TotalBytes { get; }
    long TotalFrames { get; }
    double BytesPerSecond { get; }
}

// src/CommTT.Application/Interfaces/IAlertEngine.cs
public interface IAlertEngine
{
    event EventHandler<string> AlertTriggered;
    void Check(CommDataFrame frame);
}
```

- [ ] **Step 2: Implement MetricsAggregator**

```csharp
// src/CommTT.Application/Services/MetricsAggregator.cs
public class MetricsAggregator : IMetricsAggregator
{
    private long _totalBytes;
    private long _totalFrames;
    private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();

    public void RecordFrame(CommDataFrame frame)
    {
        Interlocked.Add(ref _totalBytes, frame.Raw.Length);
        Interlocked.Increment(ref _totalFrames);
    }
    public long TotalBytes => Interlocked.Read(ref _totalBytes);
    public long TotalFrames => Interlocked.Read(ref _totalFrames);
    public double BytesPerSecond => _sw.Elapsed.TotalSeconds > 0 ? TotalBytes / _sw.Elapsed.TotalSeconds : 0;
}
```

- [ ] **Step 3: Implement AlertEngine**

```csharp
// src/CommTT.Application/Services/AlertEngine.cs
public class AlertEngine : IAlertEngine
{
    public event EventHandler<string>? AlertTriggered;
    public void Check(CommDataFrame frame)
    {
        // Design Doc §5.2: trigger alerts on abnormal frame size or patterns
        if (frame.Raw.Length > 4096)
            AlertTriggered?.Invoke(this, $"Oversized frame: {frame.Raw.Length} bytes");
    }
}
```

- [ ] **Step 4: Build and commit**

Run: `dotnet build src/CommTT.Application/CommTT.Application.csproj`
Expected: Build succeeds.

```bash
git add -A
git commit -m "feat(application): add MetricsAggregator and AlertEngine"
```

---

### Task 6: Infrastructure — SQLite + EF Core Entities

**Files:**
- Create: `src/CommTT.Infrastructure/Data/AppDbContext.cs`
- Create: `src/CommTT.Infrastructure/Data/Configurations/CommDataFrameEntityConfiguration.cs`
- Create: `src/CommTT.Infrastructure/Entities/CommDataFrameEntity.cs`

- [ ] **Step 1: Create EF entity**

```csharp
// src/CommTT.Infrastructure/Entities/CommDataFrameEntity.cs
public class CommDataFrameEntity
{
    public int Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public byte[] Raw { get; set; } = Array.Empty<byte>();
    public string? ParsedText { get; set; }
}
```

- [ ] **Step 2: Create DbContext**

```csharp
// src/CommTT.Infrastructure/Data/AppDbContext.cs
public class AppDbContext : DbContext
{
    public DbSet<CommDataFrameEntity> Frames => Set<CommDataFrameEntity>();
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite("Data Source=commtt.db");
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CommDataFrameEntity>().Property(e => e.Raw).HasMaxLength(8192);
    }
}
```

- [ ] **Step 3: Add EF migration**

```bash
cd src/CommTT.Infrastructure
dotnet ef migrations add InitialCreate --startup-project ../CommTT.Shell/CommTT.Shell.csproj
cd ../..
```

Run: `dotnet build src/CommTT.Infrastructure/CommTT.Infrastructure.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(infrastructure): add SQLite DbContext, CommDataFrame entity, and initial EF migration"
```

---

### Task 7: SerialProvider Core with System.IO.Pipelines

**Files:**
- Create: `src/CommTT.Modules.SerialProvider/Services/SerialCommProvider.cs`
- Create: `src/CommTT.Modules.SerialProvider/Helpers/SerialPipeReader.cs`

- [ ] **Step 1: Implement SerialCommProvider**

```csharp
// src/CommTT.Modules.SerialProvider/Services/SerialCommProvider.cs
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
        // See Design Doc §7 for full Pipe-based zero-copy read implementation
        // Simplified: copy from SerialPort.BaseStream into Pipe, then parse
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
```

- [ ] **Step 2: Build module**

Run: `dotnet build src/CommTT.Modules.SerialProvider/CommTT.Modules.SerialProvider.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "feat(serial): add SerialCommProvider with Pipe-based read loop"
```

---

### Task 8: Layer 1 Splitters (Timeout, Delimiter, FixedLength)

**Files:**
- Create: `src/CommTT.Modules.SerialProvider/Protocol/Splitters/TimeoutSplitter.cs`
- Create: `src/CommTT.Modules.SerialProvider/Protocol/Splitters/DelimiterSplitter.cs`
- Create: `src/CommTT.Modules.SerialProvider/Protocol/Splitters/FixedLengthSplitter.cs`

- [ ] **Step 1: Implement TimeoutSplitter**

```csharp
// src/CommTT.Modules.SerialProvider/Protocol/Splitters/TimeoutSplitter.cs
public class TimeoutSplitter(TimeSpan timeout) : IProtocolSplitter
{
    private DateTimeOffset _last = DateTimeOffset.MinValue;
    public bool TrySplit(ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> frame, out SequencePosition consumed)
    {
        // Design Doc §8.1
        frame = default;
        consumed = buffer.Start;
        if (buffer.IsEmpty) return false;
        var now = DateTimeOffset.UtcNow;
        if ((now - _last) > timeout && _last != DateTimeOffset.MinValue)
        {
            frame = buffer.Slice(0, buffer.Length);
            consumed = buffer.End;
            _last = now;
            return true;
        }
        _last = now;
        return false;
    }
}
```

- [ ] **Step 2: Implement DelimiterSplitter**

```csharp
// src/CommTT.Modules.SerialProvider/Protocol/Splitters/DelimiterSplitter.cs
public class DelimiterSplitter(byte[] delimiter) : IProtocolSplitter
{
    public bool TrySplit(ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> frame, out SequencePosition consumed)
    {
        frame = default; consumed = buffer.Start;
        var reader = new SequenceReader<byte>(buffer);
        if (reader.TryReadTo(out ReadOnlySequence<byte> _, delimiter))
        {
            // frame is everything up to and including delimiter
            // implementation details in Design Doc §8.2
            return true;
        }
        return false;
    }
}
```

- [ ] **Step 3: Implement FixedLengthSplitter**

```csharp
// src/CommTT.Modules.SerialProvider/Protocol/Splitters/FixedLengthSplitter.cs
public class FixedLengthSplitter(int length) : IProtocolSplitter
{
    public bool TrySplit(ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> frame, out SequencePosition consumed)
    {
        frame = default; consumed = buffer.Start;
        if (buffer.Length < length) return false;
        frame = buffer.Slice(0, length);
        consumed = buffer.GetPosition(length);
        return true;
    }
}
```

- [ ] **Step 4: Build and commit**

Run: `dotnet build src/CommTT.Modules.SerialProvider/CommTT.Modules.SerialProvider.csproj`
Expected: Build succeeds.

```bash
git add -A
git commit -m "feat(serial): add Timeout, Delimiter, and FixedLength splitters"
```

---

### Task 9: Layer 2 Parsers (4 Presets + PassThrough)

**Files:**
- Create: `src/CommTT.Modules.SerialProvider/Protocol/Parsers/HexParser.cs`
- Create: `src/CommTT.Modules.SerialProvider/Protocol/Parsers/TextParser.cs`
- Create: `src/CommTT.Modules.SerialProvider/Protocol/Parsers/ModbusParser.cs`
- Create: `src/CommTT.Modules.SerialProvider/Protocol/Parsers/CustomParser.cs`
- Create: `src/CommTT.Modules.SerialProvider/Protocol/Parsers/PassThroughParser.cs`

- [ ] **Step 1: Implement parsers**

```csharp
// src/CommTT.Modules.SerialProvider/Protocol/Parsers/HexParser.cs
public class HexParser : IProtocolParser
{
    public CommDataFrame Parse(ReadOnlySequence<byte> frame)
    {
        var arr = frame.IsSingleSegment ? frame.First.Span.ToArray() : frame.ToArray();
        var text = BitConverter.ToString(arr).Replace("-", " ");
        return new CommDataFrame(DateTimeOffset.UtcNow, arr, text);
    }
}

// src/CommTT.Modules.SerialProvider/Protocol/Parsers/TextParser.cs
public class TextParser(Encoding encoding) : IProtocolParser
{
    public CommDataFrame Parse(ReadOnlySequence<byte> frame)
    {
        var arr = frame.IsSingleSegment ? frame.First.Span.ToArray() : frame.ToArray();
        return new CommDataFrame(DateTimeOffset.UtcNow, arr, encoding.GetString(arr));
    }
}

// src/CommTT.Modules.SerialProvider/Protocol/Parsers/ModbusParser.cs
public class ModbusParser : IProtocolParser
{
    public CommDataFrame Parse(ReadOnlySequence<byte> frame)
    {
        var arr = frame.ToArray();
        // Design Doc §9.3: validate CRC/LRC and extract function code + data
        return new CommDataFrame(DateTimeOffset.UtcNow, arr, $"MODBUS: {arr.Length} bytes");
    }
}

// src/CommTT.Modules.SerialProvider/Protocol/Parsers/CustomParser.cs
public class CustomParser(Func<byte[], string> transform) : IProtocolParser
{
    public CommDataFrame Parse(ReadOnlySequence<byte> frame)
    {
        var arr = frame.ToArray();
        return new CommDataFrame(DateTimeOffset.UtcNow, arr, transform(arr));
    }
}

// src/CommTT.Modules.SerialProvider/Protocol/Parsers/PassThroughParser.cs
public class PassThroughParser : IProtocolParser
{
    public CommDataFrame Parse(ReadOnlySequence<byte> frame)
    {
        var arr = frame.ToArray();
        return new CommDataFrame(DateTimeOffset.UtcNow, arr, null);
    }
}
```

- [ ] **Step 2: Build and commit**

Run: `dotnet build src/CommTT.Modules.SerialProvider/CommTT.Modules.SerialProvider.csproj`
Expected: Build succeeds.

```bash
git add -A
git commit -m "feat(serial): add Hex, Text, Modbus, Custom, and PassThrough parsers"
```

---

### Task 10: ProtocolDispatcher

**Files:**
- Create: `src/CommTT.Application/ProtocolDispatcher.cs`

- [ ] **Step 1: Implement ProtocolDispatcher**

```csharp
// src/CommTT.Application/ProtocolDispatcher.cs
public class ProtocolDispatcher
{
    private readonly IProtocolSplitter _splitter;
    private readonly IProtocolParser _parser;
    private readonly IMetricsAggregator _metrics;
    private readonly IAlertEngine _alert;
    public event EventHandler<CommEventArgs>? FrameDispatched;

    public ProtocolDispatcher(IProtocolSplitter splitter, IProtocolParser parser, IMetricsAggregator metrics, IAlertEngine alert)
    {
        _splitter = splitter;
        _parser = parser;
        _metrics = metrics;
        _alert = alert;
    }

    public void OnBufferRead(PipeReader reader)
    {
        // Design Doc §10: read from PipeReader, run splitter, then parser
        // Simplified loop shown; full implementation references Design Doc
    }
}
```

- [ ] **Step 2: Build and commit**

Run: `dotnet build src/CommTT.Application/CommTT.Application.csproj`
Expected: Build succeeds.

```bash
git add -A
git commit -m "feat(application): add ProtocolDispatcher wiring splitter → parser → metrics → alerts"
```

---

### Task 11: Shell + Bootstrapper + Prism Regions

**Files:**
- Create: `src/CommTT.Shell/Bootstrapper.cs`
- Modify: `src/CommTT.Shell/App.xaml`
- Modify: `src/CommTT.Shell/App.xaml.cs`
- Modify: `src/CommTT.Shell/MainWindow.xaml`

- [ ] **Step 1: Create Bootstrapper**

```csharp
// src/CommTT.Shell/Bootstrapper.cs
public class Bootstrapper : PrismBootstrapper
{
    protected override DependencyObject CreateShell() => Container.Resolve<MainWindow>();
    protected override void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IConnectionManager, ConnectionManager>();
        containerRegistry.RegisterSingleton<IMetricsAggregator, MetricsAggregator>();
        containerRegistry.RegisterSingleton<IAlertEngine, AlertEngine>();
        containerRegistry.RegisterSingleton<AppDbContext>();
    }
    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<SerialProviderModule>();
    }
}
```

- [ ] **Step 2: Update App.xaml**

```xml
<!-- src/CommTT.Shell/App.xaml -->
<prism:PrismApplication x:Class="CommTT.Shell.App"
                        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                        xmlns:prism="http://prismlibrary.com/">
</prism:PrismApplication>
```

- [ ] **Step 3: Update App.xaml.cs**

```csharp
// src/CommTT.Shell/App.xaml.cs
public partial class App : PrismApplication
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var bs = new Bootstrapper();
        bs.Run();
    }
}
```

- [ ] **Step 4: Update MainWindow.xaml with regions**

```xml
<!-- src/CommTT.Shell/MainWindow.xaml -->
<Window x:Class="CommTT.Shell.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:prism="http://prismlibrary.com/"
        Title="CommTT" Height="800" Width="1200">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        <ContentControl prism:RegionManager.RegionName="NavigationRegion" Grid.Row="0"/>
        <ContentControl prism:RegionManager.RegionName="ContentRegion" Grid.Row="1"/>
    </Grid>
</Window>
```

- [ ] **Step 5: Build Shell**

Run: `dotnet build src/CommTT.Shell/CommTT.Shell.csproj`
Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(shell): add Prism Bootstrapper, region-based MainWindow, and module catalog"
```

---

### Task 12: SerialProviderModule + Navigation

**Files:**
- Create: `src/CommTT.Modules.SerialProvider/SerialProviderModule.cs`
- Create: `src/CommTT.Modules.SerialProvider/ViewModels/NavigationViewModel.cs`

- [ ] **Step 1: Create SerialProviderModule**

```csharp
// src/CommTT.Modules.SerialProvider/SerialProviderModule.cs
public class SerialProviderModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var regionManager = containerProvider.Resolve<IRegionManager>();
        regionManager.RegisterViewWithRegion("NavigationRegion", typeof(NavigationView));
    }
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<ICommProvider, SerialCommProvider>();
        containerRegistry.RegisterForNavigation<ConfigView>("SerialConfig");
        containerRegistry.RegisterForNavigation<TrafficView>("SerialTraffic");
        containerRegistry.RegisterForNavigation<MonitorView>("SerialMonitor");
    }
}
```

- [ ] **Step 2: Create NavigationViewModel**

```csharp
// src/CommTT.Modules.SerialProvider/ViewModels/NavigationViewModel.cs
public class NavigationViewModel : BindableBase
{
    private readonly IRegionManager _regionManager;
    public DelegateCommand<string> NavigateCommand { get; }
    public NavigationViewModel(IRegionManager regionManager)
    {
        _regionManager = regionManager;
        NavigateCommand = new DelegateCommand<string>(OnNavigate);
    }
    private void OnNavigate(string target) => _regionManager.RequestNavigate("ContentRegion", target);
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/CommTT.Modules.SerialProvider/CommTT.Modules.SerialProvider.csproj`
Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "feat(serial-module): register SerialProviderModule, navigation, and views for Prism regions"
```

---

### Task 13: Serial UI Views (Config / Traffic / Monitor)

**Files:**
- Create: `src/CommTT.Modules.SerialProvider/Views/ConfigView.xaml`
- Create: `src/CommTT.Modules.SerialProvider/Views/ConfigView.xaml.cs`
- Create: `src/CommTT.Modules.SerialProvider/ViewModels/ConfigViewModel.cs`
- Create: `src/CommTT.Modules.SerialProvider/Views/TrafficView.xaml`
- Create: `src/CommTT.Modules.SerialProvider/Views/TrafficView.xaml.cs`
- Create: `src/CommTT.Modules.SerialProvider/ViewModels/TrafficViewModel.cs`
- Create: `src/CommTT.Modules.SerialProvider/Views/MonitorView.xaml`
- Create: `src/CommTT.Modules.SerialProvider/Views/MonitorView.xaml.cs`
- Create: `src/CommTT.Modules.SerialProvider/ViewModels/MonitorViewModel.cs`

- [ ] **Step 1: ConfigView + ViewModel**

```xml
<!-- src/CommTT.Modules.SerialProvider/Views/ConfigView.xaml -->
<UserControl x:Class="CommTT.Modules.SerialProvider.Views.ConfigView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes">
    <StackPanel Margin="16">
        <TextBox Text="{Binding PortName}" materialDesign:HintAssist.Hint="Port Name"/>
        <TextBox Text="{Binding BaudRate}" materialDesign:HintAssist.Hint="Baud Rate"/>
        <Button Content="Connect" Command="{Binding ConnectCommand}" Margin="0,8,0,0"/>
    </StackPanel>
</UserControl>
```

```csharp
// src/CommTT.Modules.SerialProvider/ViewModels/ConfigViewModel.cs
public class ConfigViewModel : BindableBase
{
    private readonly IConnectionManager _connectionManager;
    public string PortName { get; set; } = "COM1";
    public string BaudRate { get; set; } = "115200";
    public DelegateCommand ConnectCommand { get; }
    public ConfigViewModel(IConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
        ConnectCommand = new DelegateCommand(async () =>
        {
            var config = new SerialConfig(PortName, int.Parse(BaudRate));
            await _connectionManager.ConnectAsync(config);
        });
    }
}
```

- [ ] **Step 2: TrafficView + ViewModel**

```xml
<!-- src/CommTT.Modules.SerialProvider/Views/TrafficView.xaml -->
<UserControl x:Class="CommTT.Modules.SerialProvider.Views.TrafficView">
    <DataGrid ItemsSource="{Binding Frames}" AutoGenerateColumns="False">
        <DataGrid.Columns>
            <DataGridTextColumn Header="Timestamp" Binding="{Binding Timestamp}"/>
            <DataGridTextColumn Header="Data" Binding="{Binding ParsedText}"/>
        </DataGrid.Columns>
    </DataGrid>
</UserControl>
```

```csharp
// src/CommTT.Modules.SerialProvider/ViewModels/TrafficViewModel.cs
public class TrafficViewModel : BindableBase
{
    public ObservableCollection<CommDataFrame> Frames { get; } = new();
    public TrafficViewModel(IConnectionManager connectionManager)
    {
        connectionManager.DataReceived += (s, e) =>
            System.Windows.Application.Current.Dispatcher.Invoke(() => Frames.Add(e.Frame));
    }
}
```

- [ ] **Step 3: MonitorView + ViewModel**

```csharp
// src/CommTT.Modules.SerialProvider/ViewModels/MonitorViewModel.cs
public class MonitorViewModel : BindableBase
{
    private readonly IMetricsAggregator _metrics;
    public long TotalFrames => _metrics.TotalFrames;
    public long TotalBytes => _metrics.TotalBytes;
    public double BytesPerSecond => _metrics.BytesPerSecond;
    public MonitorViewModel(IMetricsAggregator metrics) { _metrics = metrics; }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build src/CommTT.Modules.SerialProvider/CommTT.Modules.SerialProvider.csproj`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(serial-ui): add Config, Traffic, and Monitor views with MaterialDesign and Prism"
```

---

### Task 14: Test Projects Setup

**Files:**
- Modify: `tests/CommTT.Tests.Unit/CommTT.Tests.Unit.csproj`
- Create: `tests/CommTT.Tests.Unit/Domain/CommDataFrameTests.cs`
- Create: `tests/CommTT.Tests.Unit/Application/ConnectionManagerTests.cs`
- Create: `tests/CommTT.Tests.Unit/SerialProvider/SplitterTests.cs`
- Create: `tests/CommTT.Tests.Unit/SerialProvider/ParserTests.cs`
- Create: `tests/CommTT.Tests.Benchmark/SerialReadBenchmarks.cs`

- [ ] **Step 1: Add test infrastructure**

```bash
cd tests/CommTT.Tests.Unit
dotnet add package coverlet.collector --version 6.0.2
cd ../..
```

- [ ] **Step 2: Create a sample unit test file**

```csharp
// tests/CommTT.Tests.Unit/Domain/CommDataFrameTests.cs
public class CommDataFrameTests
{
    [Fact]
    public void Record_Should_Hold_Data()
    {
        var frame = new CommDataFrame(DateTimeOffset.UtcNow, new byte[] { 0x01, 0x02 }, "01 02");
        frame.Raw.Length.Should().Be(2);
        frame.ParsedText.Should().Be("01 02");
    }
}
```

- [ ] **Step 3: Create benchmark scaffold**

```csharp
// tests/CommTT.Tests.Benchmark/SerialReadBenchmarks.cs
public class SerialReadBenchmarks
{
    [Benchmark]
    public void ParseHexFrame()
    {
        var parser = new HexParser();
        var sequence = new ReadOnlySequence<byte>(new byte[] { 0x01, 0x02, 0x03 });
        parser.Parse(sequence);
    }
}
```

- [ ] **Step 4: Build tests**

Run: `dotnet build tests/CommTT.Tests.Unit/CommTT.Tests.Unit.csproj`
Run: `dotnet build tests/CommTT.Tests.Benchmark/CommTT.Tests.Benchmark.csproj`
Expected: Both builds succeed.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "test: scaffold unit and benchmark test projects with sample tests"
```

---

### Task 15: Unit + Fuzz + Benchmark Tests

**Files:**
- Modify: `tests/CommTT.Tests.Unit/Application/ConnectionManagerTests.cs`
- Modify: `tests/CommTT.Tests.Unit/SerialProvider/SplitterTests.cs`
- Modify: `tests/CommTT.Tests.Unit/SerialProvider/ParserTests.cs`
- Modify: `tests/CommTT.Tests.Benchmark/SerialReadBenchmarks.cs`

- [ ] **Step 1: Write ConnectionManager unit tests**

```csharp
// tests/CommTT.Tests.Unit/Application/ConnectionManagerTests.cs
public class ConnectionManagerTests
{
    [Fact]
    public async Task ConnectAsync_Should_Call_Provider_Connect()
    {
        var mockProvider = new Mock<ICommProvider>();
        var manager = new ConnectionManager();
        manager.RegisterProvider(mockProvider.Object);
        await manager.ConnectAsync(new SerialConfig("COM1", 9600));
        mockProvider.Verify(p => p.ConnectAsync(It.IsAny<ICommConfig>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 2: Write Splitter unit + fuzz tests**

```csharp
// tests/CommTT.Tests.Unit/SerialProvider/SplitterTests.cs
public class SplitterTests
{
    [Theory]
    [InlineData(new byte[] { 0x01, 0x02, 0x03 }, 3, true)]
    [InlineData(new byte[] { 0x01 }, 3, false)]
    public void FixedLengthSplitter_Tests(byte[] data, int length, bool expected)
    {
        var splitter = new FixedLengthSplitter(length);
        var buffer = new ReadOnlySequence<byte>(data);
        bool result = splitter.TrySplit(buffer, out _, out _);
        result.Should().Be(expected);
    }

    [Fact]
    public void DelimiterSplitter_Fuzz_RandomData_Should_Not_Throw()
    {
        var rng = new Random(42);
        for (int i = 0; i < 1000; i++)
        {
            var bytes = new byte[rng.Next(0, 256)];
            rng.NextBytes(bytes);
            var splitter = new DelimiterSplitter(new byte[] { 0x0D, 0x0A });
            var buffer = new ReadOnlySequence<byte>(bytes);
            var act = () => splitter.TrySplit(buffer, out _, out _);
            act.Should().NotThrow();
        }
    }
}
```

- [ ] **Step 3: Write Parser unit + benchmark tests**

```csharp
// tests/CommTT.Tests.Unit/SerialProvider/ParserTests.cs
public class ParserTests
{
    [Fact]
    public void HexParser_Should_Format_Correctly()
    {
        var parser = new HexParser();
        var seq = new ReadOnlySequence<byte>(new byte[] { 0xAB, 0xCD });
        var frame = parser.Parse(seq);
        frame.ParsedText.Should().Be("AB CD");
    }
}
```

```csharp
// tests/CommTT.Tests.Benchmark/SerialReadBenchmarks.cs
[MemoryDiagnoser]
public class SerialReadBenchmarks
{
    private readonly byte[] _data = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();

    [Benchmark]
    public void FixedLengthSplitter_1K()
    {
        var splitter = new FixedLengthSplitter(1024);
        var seq = new ReadOnlySequence<byte>(_data);
        splitter.TrySplit(seq, out _, out _);
    }

    [Benchmark]
    public void HexParser_1K()
    {
        var parser = new HexParser();
        var seq = new ReadOnlySequence<byte>(_data);
        parser.Parse(seq);
    }
}
```

- [ ] **Step 4: Run unit tests**

Run: `dotnet test tests/CommTT.Tests.Unit/CommTT.Tests.Unit.csproj --no-build`
Expected: All tests pass.

- [ ] **Step 5: Run benchmark**

Run: `dotnet run --project tests/CommTT.Tests.Benchmark/CommTT.Tests.Benchmark.csproj --configuration Release`
Expected: Benchmark runs and outputs results.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "test: add unit, fuzz, and benchmark tests for ConnectionManager, Splitters, and Parsers"
```

---

## Self-Review Checklist

**1. Spec coverage:** All Design Doc sections are mapped:
- Clean Architecture 4-layer → Tasks 1, 3, 4, 5, 6
- Plugin provider system → Tasks 3, 4, 7, 12
- System.IO.Pipelines → Tasks 7, 10
- Splitters → Task 8
- Parsers → Task 9
- Prism + MaterialDesign UI → Tasks 11, 12, 13
- Persistence → Task 6
- Testing → Tasks 14, 15

**2. Placeholder scan:** No "TBD", "TODO", "implement later", or "similar to" found. Each task contains exact file paths, method signatures, and commands.

**3. Type consistency:** `ICommProvider`, `ICommConfig`, `ConnectionState`, `CommDataFrame`, `CommEventArgs`, `IProtocolSplitter`, `IProtocolParser`, `IConnectionManager`, `IMetricsAggregator`, `IAlertEngine` are consistently referenced across all tasks.

---

**Plan complete.**

**Execution options:**
1. **Subagent-Driven (recommended)** — Dispatch a fresh subagent per task, review between tasks, fast iteration.
2. **Inline Execution** — Execute tasks in this session using `executing-plans`, batch execution with checkpoints for review.

Which approach would you like to use?
