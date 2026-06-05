---
archived-with: 2026-06-05-enhance-logging-system
status: final
---
# Enhance Logging System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 CommTT 引入零数据库、纯文本、按日期分目录的轻量级日志系统，包含 Serilog 运行时观测日志、协议异常审计日志、通讯状态审计日志。

**Architecture:** 使用 `Microsoft.Extensions.Logging` + Serilog 作为运行时日志基础设施；自定义 `ProtocolExceptionLogger` 和 `ProtocolStateLogger` 作为审计日志写入器，直接追加到纯文本 TXT 文件；所有日志按 `Logs/YYYYMMDD/` 分目录存放，不自动删除。

**Tech Stack:** C# 12, .NET 8, WPF, Prism, Serilog, Serilog.Sinks.File, Microsoft.Extensions.Logging.Abstractions

**Base Ref:** b29e2979001ec9903ba18cff093f791d5c1fa428

---

## Task 1: 引入 NuGet 包依赖

**Files:**
- Modify: `src/CommTT.Infrastructure/CommTT.Infrastructure.csproj`
- Modify: `src/CommTT.Application/CommTT.Application.csproj`
- Modify: `src/CommTT.Shell/CommTT.Shell.csproj`

- [ ] **Step 1: 向 Infrastructure 添加 Serilog 包**

```xml
<!-- CommTT.Infrastructure.csproj -->
<PackageReference Include="Serilog" Version="3.1.1" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
```

- [ ] **Step 2: 向 Application 添加 M.E.L 抽象包**

```xml
<!-- CommTT.Application.csproj -->
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
```

- [ ] **Step 3: 向 Shell 添加 Serilog.Extensions.Logging**

```xml
<!-- CommTT.Shell.csproj -->
<PackageReference Include="Serilog.Extensions.Logging" Version="8.0.0" />
```

- [ ] **Step 4: 编译验证**

Run: `dotnet restore && dotnet build`
Expected: 构建成功，无新增编译错误。

- [ ] **Step 5: Commit**

```bash
git add *.csproj
git commit -m "build: add Serilog and M.E.L logging dependencies"
```

---

## Task 2: 创建 ProtocolExceptionLogger

**Files:**
- Create: `src/CommTT.Infrastructure/Logging/ProtocolExceptionLogger.cs`

- [ ] **Step 1: 实现 ProtocolExceptionLogger**

```csharp
using System.Text;

namespace CommTT.Infrastructure.Logging;

public class ProtocolExceptionLogger
{
    private readonly string _baseDir;
    private readonly Lock _lock = new();
    private string? _currentFile;
    private StreamWriter? _writer;

    public ProtocolExceptionLogger(string baseDir = "")
    {
        _baseDir = string.IsNullOrEmpty(baseDir)
            ? Path.Combine(AppContext.BaseDirectory, "Logs")
            : baseDir;
    }

    public void Write(string protocolType, string connectionId,
        string exceptionType, string message, string context, string rawDataSnapshot)
    {
        EnsureWriter();
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {protocolType} | {connectionId} | {exceptionType} | {message} | {context} | Raw: {rawDataSnapshot}";
        lock (_lock)
        {
            _writer!.WriteLine(line);
            _writer.Flush();
        }
    }

    private void EnsureWriter()
    {
        var dir = Path.Combine(_baseDir, DateTime.Now.ToString("yyyyMMdd"));
        var file = Path.Combine(dir, "protocol-exception.txt");
        if (file != _currentFile)
        {
            Directory.CreateDirectory(dir);
            _writer?.Dispose();
            _writer = new StreamWriter(file, append: true, encoding: Encoding.UTF8);
            _currentFile = file;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/CommTT.Infrastructure/Logging/ProtocolExceptionLogger.cs
git commit -m "feat: add ProtocolExceptionLogger for plain-text audit logs"
```

---

## Task 3: 创建 ProtocolStateLogger

**Files:**
- Create: `src/CommTT.Infrastructure/Logging/ProtocolStateLogger.cs`

- [ ] **Step 1: 实现 ProtocolStateLogger**

```csharp
using System.Text;
using CommTT.Domain.Models;

namespace CommTT.Infrastructure.Logging;

public class ProtocolStateLogger
{
    private readonly string _baseDir;
    private readonly Lock _lock = new();
    private string? _currentFile;
    private StreamWriter? _writer;

    public ProtocolStateLogger(string baseDir = "")
    {
        _baseDir = string.IsNullOrEmpty(baseDir)
            ? Path.Combine(AppContext.BaseDirectory, "Logs")
            : baseDir;
    }

    public void Write(string protocolType, string connectionId,
        ConnectionState oldState, ConnectionState newState, string triggerReason, string details)
    {
        EnsureWriter();
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {protocolType} | {connectionId} | {oldState} → {newState} | {triggerReason} | {details}";
        lock (_lock)
        {
            _writer!.WriteLine(line);
            _writer.Flush();
        }
    }

    private void EnsureWriter()
    {
        var dir = Path.Combine(_baseDir, DateTime.Now.ToString("yyyyMMdd"));
        var file = Path.Combine(dir, "protocol-state.txt");
        if (file != _currentFile)
        {
            Directory.CreateDirectory(dir);
            _writer?.Dispose();
            _writer = new StreamWriter(file, append: true, encoding: Encoding.UTF8);
            _currentFile = file;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/CommTT.Infrastructure/Logging/ProtocolStateLogger.cs
git commit -m "feat: add ProtocolStateLogger for plain-text state audit logs"
```

---

## Task 4: 配置 Bootstrapper Serilog 运行时日志

**Files:**
- Modify: `src/CommTT.Shell/Bootstrapper.cs`

- [ ] **Step 1: 修改 Bootstrapper 注册 Serilog 和自定义 Logger**

```csharp
using CommTT.Application.Interfaces;
using CommTT.Application.Services;
using CommTT.Infrastructure.Data;
using CommTT.Infrastructure.Logging;
using CommTT.Modules.SerialProvider;
using Microsoft.Extensions.Logging;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;

namespace CommTT.Shell;

public class Bootstrapper
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IConnectionManager, ConnectionManager>();
        containerRegistry.RegisterSingleton<IMetricsAggregator, MetricsAggregator>();
        containerRegistry.RegisterSingleton<IAlertEngine, AlertEngine>();
        containerRegistry.RegisterSingleton<AppDbContext>();

        // Configure Serilog runtime log
        var logDir = Path.Combine(AppContext.BaseDirectory, "Logs", DateTime.Now.ToString("yyyyMMdd"));
        Directory.CreateDirectory(logDir);

        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine(logDir, "runtime.txt"),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}")
            .CreateLogger();

        // Register M.E.L factory with Serilog
        containerRegistry.RegisterInstance<ILoggerFactory>(new LoggerFactory().AddSerilog(logger));

        // Register custom audit loggers
        containerRegistry.RegisterSingleton<ProtocolExceptionLogger>();
        containerRegistry.RegisterSingleton<ProtocolStateLogger>();
    }

    public void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<SerialProviderModule>();
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/CommTT.Shell/Bootstrapper.cs
git commit -m "feat: configure Serilog runtime logging and register audit loggers in Bootstrapper"
```

---

## Task 5: 修改 SerialCommProvider 记录异常与状态

**Files:**
- Modify: `src/CommTT.Modules.SerialProvider/Services/SerialCommProvider.cs`

- [ ] **Step 1: 注入 Logger 和审计 Logger，添加关键日志**

```csharp
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
            State = ConnectionState.Connected;
            StateChanged?.Invoke(this, State);
            _stateLogger.Write("Serial", sc.PortName, ConnectionState.Disconnected, ConnectionState.Connected, "UserAction", $"Port opened at {sc.BaudRate} baud");
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
            _exceptionLogger.Write("Serial", _port.PortName, ex.GetType().Name, ex.Message,
                $"PortName={_port.PortName}", Convert.ToHexString(data.Span.Slice(0, Math.Min(data.Length, 64))));
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
```

- [ ] **Step 2: Commit**

```bash
git add src/CommTT.Modules.SerialProvider/Services/SerialCommProvider.cs
git commit -m "feat: add exception and state audit logging to SerialCommProvider"
```

---

## Task 6: 修改 ProtocolDispatcher 记录解析异常

**Files:**
- Modify: `src/CommTT.Application/ProtocolDispatcher.cs`

- [ ] **Step 1: 注入 Logger 和 ExceptionLogger**

```csharp
using System.IO.Pipelines;
using CommTT.Application.Interfaces;
using CommTT.Domain.Events;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;
using CommTT.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace CommTT.Application;

public class ProtocolDispatcher
{
    private readonly IProtocolSplitter _splitter;
    private readonly IProtocolParser _parser;
    private readonly IMetricsAggregator _metrics;
    private readonly IAlertEngine _alert;
    private readonly ILogger<ProtocolDispatcher> _logger;
    private readonly ProtocolExceptionLogger _exceptionLogger;

    public event EventHandler<CommEventArgs>? FrameDispatched;

    public ProtocolDispatcher(
        IProtocolSplitter splitter,
        IProtocolParser parser,
        IMetricsAggregator metrics,
        IAlertEngine alert,
        ILogger<ProtocolDispatcher> logger,
        ProtocolExceptionLogger exceptionLogger)
    {
        _splitter = splitter;
        _parser = parser;
        _metrics = metrics;
        _alert = alert;
        _logger = logger;
        _exceptionLogger = exceptionLogger;
    }

    public void OnBufferRead(PipeReader reader)
    {
        try
        {
            while (reader.TryRead(out var readResult))
            {
                var buffer = readResult.Buffer;

                while (_splitter.TrySplit(buffer, out var frame, out var consumed))
                {
                    try
                    {
                        var dataFrame = _parser.Parse(frame);
                        _metrics.RecordFrame(dataFrame);
                        _alert.Check(dataFrame);
                        FrameDispatched?.Invoke(this, new CommEventArgs { Frame = dataFrame });
                    }
                    catch (Exception ex)
                    {
                        var rawHex = Convert.ToHexString(frame.Slice(0, Math.Min(frame.Length, 128)).ToArray());
                        _exceptionLogger.Write("Unknown", "", ex.GetType().Name, ex.Message, "ProtocolDispatcher.Parse", rawHex);
                        _logger.LogError(ex, "Frame parse error");
                        throw;
                    }

                    reader.AdvanceTo(consumed, buffer.End);
                    buffer = buffer.Slice(consumed);
                }

                reader.AdvanceTo(buffer.End);
            }
        }
        catch (Exception ex)
        {
            _exceptionLogger.Write("Unknown", "", ex.GetType().Name, ex.Message, "ProtocolDispatcher.OnBufferRead", "");
            _logger.LogError(ex, "Buffer read error");
            reader.Complete(ex);
            throw;
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/CommTT.Application/ProtocolDispatcher.cs
git commit -m "feat: add exception audit logging to ProtocolDispatcher"
```

---

## Task 7: 修改 ConnectionManager 桥接状态变化日志

**Files:**
- Modify: `src/CommTT.Application/Services/ConnectionManager.cs`

- [ ] **Step 1: 注入 Logger 和 StateLogger**

```csharp
using CommTT.Application.Interfaces;
using CommTT.Domain.Events;
using CommTT.Domain.Interfaces;
using CommTT.Domain.Models;
using CommTT.Infrastructure.Logging;
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
```

- [ ] **Step 2: Commit**

```bash
git add src/CommTT.Application/Services/ConnectionManager.cs
git commit -m "feat: bridge state change audit logging in ConnectionManager"
```

---

## Task 8: 配置全局未捕获异常处理器

**Files:**
- Modify: `src/CommTT.Shell/App.xaml.cs`

- [ ] **Step 1: 在 App 启动时注册 DispatcherUnhandledException**

```csharp
using System.Windows;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CommTT.Shell;

public partial class App : Application
{
    private ILogger<App>? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public void SetLogger(ILogger<App> logger) => _logger = logger;

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unhandled UI exception");
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            _logger?.LogError(ex, "Unhandled AppDomain exception");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger?.LogError(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add src/CommTT.Shell/App.xaml.cs
git commit -m "feat: add global unhandled exception handlers for runtime logging"
```

---

## Task 9: 验证日志文件生成与格式

**Files:**
- 运行应用：启动 CommTT.Shell
- 检查目录：`Logs/<YYYYMMDD>/`

- [ ] **Step 1: 启动应用并执行连接操作**

操作：启动应用 → 尝试连接一个可用串口 → 观察 `Logs/<date>/` 目录。

- [ ] **Step 2: 验证 runtime.txt**

Expected content:
```
2026-06-05 20:45:00 [INF] Bootstrapper: Application started
2026-06-05 20:45:01 [INF] SerialCommProvider: Serial port COM3 opened at 9600 baud
```

- [ ] **Step 3: 验证 protocol-state.txt**

Expected content:
```
2026-06-05 20:45:01 | Serial | COM3 | Disconnected → Connected | UserAction | Port opened at 9600 baud
```

- [ ] **Step 4: 断开连接并验证 protocol-exception.txt（如需）**

若连接过程中发生异常，验证 `protocol-exception.txt` 包含纯文本异常记录。

- [ ] **Step 5: Commit（如有临时测试代码，不要提交）**

---

## Task 10: 补充单元测试

**Files:**
- Create: `tests/CommTT.Tests.Unit/Infrastructure/ProtocolExceptionLoggerTests.cs`
- Create: `tests/CommTT.Tests.Unit/Infrastructure/ProtocolStateLoggerTests.cs`
- Create: `tests/CommTT.Tests.Unit/Shell/SerilogBootstrapTests.cs`

- [ ] **Step 1: 编写 ProtocolExceptionLoggerTests**

```csharp
using CommTT.Infrastructure.Logging;

namespace CommTT.Tests.Unit.Infrastructure;

public class ProtocolExceptionLoggerTests : IDisposable
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ProtocolExceptionLoggerTests()
    {
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Write_CreatesFileWithCorrectFormat()
    {
        var logger = new ProtocolExceptionLogger(_testDir);
        logger.Write("Serial", "COM3", "IOException", "Port unavailable",
            "PortName=COM3,BaudRate=9600", "AABBCC");

        var dir = Path.Combine(_testDir, DateTime.Now.ToString("yyyyMMdd"));
        var file = Path.Combine(dir, "protocol-exception.txt");
        Assert.True(File.Exists(file));

        var lines = File.ReadAllLines(file);
        Assert.Single(lines);
        Assert.Contains("Serial", lines[0]);
        Assert.Contains("COM3", lines[0]);
        Assert.Contains("IOException", lines[0]);
        Assert.Contains("Raw: AABBCC", lines[0]);
    }
}
```

- [ ] **Step 2: 编写 ProtocolStateLoggerTests**

```csharp
using CommTT.Domain.Models;
using CommTT.Infrastructure.Logging;

namespace CommTT.Tests.Unit.Infrastructure;

public class ProtocolStateLoggerTests : IDisposable
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ProtocolStateLoggerTests() => Directory.CreateDirectory(_testDir);
    public void Dispose()
    {
        if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
    }

    [Fact]
    public void Write_CreatesFileWithArrowSeparator()
    {
        var logger = new ProtocolStateLogger(_testDir);
        logger.Write("Serial", "COM3", ConnectionState.Disconnected, ConnectionState.Connected, "UserAction", "Opened");

        var dir = Path.Combine(_testDir, DateTime.Now.ToString("yyyyMMdd"));
        var file = Path.Combine(dir, "protocol-state.txt");
        Assert.True(File.Exists(file));

        var lines = File.ReadAllLines(file);
        Assert.Single(lines);
        Assert.Contains("Disconnected → Connected", lines[0]);
        Assert.Contains("UserAction", lines[0]);
    }
}
```

- [ ] **Step 3: 编写 SerilogBootstrapTests**

```csharp
using CommTT.Shell;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CommTT.Tests.Unit.Shell;

public class SerilogBootstrapTests : IDisposable
{
    private readonly string _testLogDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public void Dispose()
    {
        if (Directory.Exists(_testLogDir))
            Directory.Delete(_testLogDir, true);
    }

    [Fact]
    public void SerilogFileSink_CreatesRuntimeLog()
    {
        var logFile = Path.Combine(_testLogDir, "runtime.txt");
        Directory.CreateDirectory(_testLogDir);

        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(logFile, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}")
            .CreateLogger();

        var factory = new LoggerFactory();
        factory.AddSerilog(logger);
        var appLogger = factory.CreateLogger<SerilogBootstrapTests>();
        appLogger.LogInformation("Test bootstrap log");

        logger.Dispose();
        factory.Dispose();

        Assert.True(File.Exists(logFile));
        var content = File.ReadAllText(logFile);
        Assert.Contains("Test bootstrap log", content);
    }
}
```

- [ ] **Step 4: 运行所有测试**

Run: `dotnet test tests/CommTT.Tests.Unit`
Expected: 全部通过。

- [ ] **Step 5: Commit**

```bash
git add tests/
git commit -m "test: add ProtocolExceptionLogger, ProtocolStateLogger, and Serilog bootstrap tests"
```

---

## Spec Coverage Checklist

| OpenSpec Requirement | 对应 Task |
|----------------------|-----------|
| structured-logging: M.E.L + Serilog 运行时日志 | Task 1, 4 |
| runtime-error-logging: 软件运行错误 TXT 记录 | Task 4, 8 |
| protocol-exception-logging: 关键异常纯文本记录 | Task 2, 5, 6 |
| protocol-state-logging: 状态变化纯文本记录 | Task 3, 5, 7 |
| 无查询服务 | N/A（已删除 capability） |
| 按日期分目录、不自动删除 | Task 2, 3, 9 |
| 性能保障（无阻塞） | Task 5, 6, 7 |

## Placeholder Scan

- [x] 无 TBD/TODO
- [x] 无 "add appropriate error handling" 模糊描述
- [x] 所有步骤包含完整代码
- [x] 文件路径精确
- [x] 类型/方法名前后一致
