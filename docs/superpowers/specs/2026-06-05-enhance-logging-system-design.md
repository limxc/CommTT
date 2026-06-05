---
comet_change: enhance-logging-system
role: technical-design
canonical_spec: openspec
---

# Design Doc: Enhance Logging System for CommTT

## 1. 目标与范围

为 CommTT 构建一套**零数据库、无查询服务、纯人类可读文本**的轻量级日志系统，满足以下需求：
- 人工调试时可追溯故障根因。
- AI agent 自动诊断时可消费异常与状态上下文。
- 审查各协议在不同时间点的通讯健康状况。

**范围**：
- 引入 Serilog + M.E.L 基础设施，输出软件运行时观测日志。
- 建立两类自定义审计日志（协议异常、通讯状态变化）。
- 按日期分目录存放，纯文本格式，不自动删除。

**非目标**：
- 不引入数据库（SQLite、EF Core）。
- 不实现查询服务或日志检索 API。
- 不使用 JSON、XML、CSV 等结构化格式。
- 不在正常帧收发路径记录日志。

## 2. 日志分类与目录结构

所有日志统一存放在**软件可执行文件所在目录**（`AppContext.BaseDirectory`）下的 `Logs/` 文件夹中，按日期分子目录：

```
Logs/
├── 20260504/
│   ├── runtime.txt               ← 软件运行错误及其他（Serilog 输出）
│   ├── protocol-exception.txt    ← 协议解析相关错误异常
│   └── protocol-state.txt        ← 通讯状态变化
├── 20260505/
│   ├── runtime.txt
│   ├── protocol-exception.txt
│   └── protocol-state.txt
```

- **日期目录格式**：`YYYYMMDD`（如 `20260504`）。
- **不自动删除**：所有历史日志保留，由用户手动管理。

## 3. 日志格式规范

### 3.1 runtime.txt（Serilog File Sink）

```
2026-06-05 20:45:00 [INF] Bootstrapper: Application started
2026-06-05 20:45:01 [ERR] SerialCommProvider: Failed to open COM3 — System.IO.IOException: ...
```

- **字段**：时间戳、级别缩写、类别（Logger 名）、消息。
- **实现**：Serilog `File` sink + 自定义 `outputTemplate`。
- **路径动态生成**：每日首次写入时，检查并创建 `Logs/<YYYYMMDD>/` 目录，然后初始化该日的 `runtime.txt`。

### 3.2 protocol-exception.txt（ProtocolExceptionLogger）

```
2026-06-05 20:45:00 | Serial | COM3 | IOException | Port unavailable | PortName=COM3,BaudRate=9600 | Raw: AA BB CC ...
```

- **字段**：时间戳、协议类型、连接标识（端口名）、异常类型、异常消息、上下文（键值对）、原始数据快照（十六进制，截断 128 字节）。
- **分隔符**：` | `（空格-竖线-空格），便于人类阅读，也便于简单文本分割。

### 3.3 protocol-state.txt（ProtocolStateLogger）

```
2026-06-05 20:45:00 | Serial | COM3 | Disconnected → Connected | UserAction | Port opened successfully
```

- **字段**：时间戳、协议类型、连接标识、旧状态 → 新状态、触发原因、详情。
- **分隔符**：` | `。

## 4. 组件设计

### 4.1 依赖

```xml
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
<PackageReference Include="Serilog" Version="3.1.1" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
<PackageReference Include="Serilog.Extensions.Logging" Version="8.0.0" />
```

### 4.2 Serilog 运行时日志配置（Bootstrapper）

```csharp
var logDir = Path.Combine(AppContext.BaseDirectory, "Logs", DateTime.Now.ToString("yyyyMMdd"));
Directory.CreateDirectory(logDir);

var logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        path: Path.Combine(logDir, "runtime.txt"),
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}")
    .CreateLogger();

// Register with Microsoft.Extensions.Logging
services.AddLogging(builder => builder.AddSerilog(logger));
```

**注意**：由于 Serilog File Sink 的内置滚动机制与 `YYYYMMDD` 目录要求不完全匹配，建议自定义一个 `PathGenerator` 委托或每日重新初始化 Logger（WPF 应用通常长运行，可在每日首次日志写入时切换文件句柄）。

### 4.3 ProtocolExceptionLogger

```csharp
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

    public async Task WriteAsync(string protocolType, string connectionId,
        string exceptionType, string message, string context, string rawDataSnapshot)
    {
        var file = EnsureWriter();
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {protocolType} | {connectionId} | {exceptionType} | {message} | {context} | Raw: {rawDataSnapshot}";
        lock (_lock) // or use SemaphoreSlim for async
        {
            _writer!.WriteLine(line);
            _writer.Flush(); // or batch flush periodically
        }
        await Task.CompletedTask;
    }

    private string EnsureWriter()
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
        return file;
    }
}
```

### 4.4 ProtocolStateLogger

与 `ProtocolExceptionLogger` 结构一致，输出到 `protocol-state.txt`，格式：

```csharp
var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {protocolType} | {connectionId} | {oldState} → {newState} | {triggerReason} | {details}";
```

### 4.5 关键注入点

| 组件 | 注入内容 | 记录行为 |
|------|---------|---------|
| `SerialCommProvider` | `ILogger<SerialCommProvider>`, `ProtocolExceptionLogger`, `ProtocolStateLogger` | Connect/Send/ReadLoop 异常写入 `protocol-exception.txt`；Connect/Disconnect/Error 状态变化写入 `protocol-state.txt`；运行时信息写入 `runtime.txt` |
| `ProtocolDispatcher` | `ILogger<ProtocolDispatcher>`, `ProtocolExceptionLogger` | 解析/分帧异常写入 `protocol-exception.txt` |
| `ConnectionManager` | `ILogger<ConnectionManager>`, `ProtocolStateLogger` | 桥接 `StateChanged` 事件，写入 `protocol-state.txt` |
| `Bootstrapper` / `App` | `ILogger<Bootstrapper>` | 启动/关闭/未捕获异常写入 `runtime.txt` |

## 5. 通讯性能保障

1. **无同步数据库 I/O**：完全去掉 EF Core / SQLite。
2. **无查询服务开销**：去掉 `IDebugLogQueryService`。
3. **异步缓冲**：Serilog 自带异步队列；自定义 Logger 使用 `StreamWriter` 缓冲追加，可定期批量 `Flush`。
4. **不记录正常帧**：仅在异常和状态变化时记录。
5. **截断策略**：`RawDataSnapshot` 截断至 128 字节，以十六进制字符串记录。
6. **线程安全**：自定义 Logger 内部使用 `lock` 或 `SemaphoreSlim` 保证多线程安全。

## 6. 测试策略

1. **SerilogBootstrapTest**：验证应用启动后 `Logs/<date>/runtime.txt` 存在且包含启动日志。
2. **ProtocolExceptionLoggerTest**：模拟写入后读取文件，验证格式与字段完整性。
3. **ProtocolStateLoggerTest**：模拟状态变化写入后读取文件，验证 `→` 分隔符和 `TriggerReason` 正确。
4. **IntegrationTest**：启动实际连接，触发异常和状态变化，验证三类日志文件均生成且内容符合预期。

## 7. 风险与缓解

| 风险 | 影响 | 缓解 |
|------|------|------|
| 日志文件无上限增长 | 磁盘空间耗尽 | 本设计不自动删除，但文件仅记录关键异常和状态变化，数据量可控；未来可由外部脚本清理 |
| `StreamWriter` 同步锁阻塞 ReadLoop | 通讯延迟 | 使用 `SemaphoreSlim` + `Task.Run` 将写入放到线程池，ReadLoop 只触发异步调用 |
| Serilog 文件句柄跨日未切换 | 日志写入旧日期文件 | 每日首次写入前检查日期并重新初始化 Logger / StreamWriter |

## 8. 交付清单

- [ ] `CommTT.Infrastructure/Logging/ProtocolExceptionLogger.cs`
- [ ] `CommTT.Infrastructure/Logging/ProtocolStateLogger.cs`
- [ ] 修改 `CommTT.Shell/Bootstrapper.cs`（Serilog 配置）
- [ ] 修改 `CommTT.Modules.SerialProvider/Services/SerialCommProvider.cs`
- [ ] 修改 `CommTT.Application/ProtocolDispatcher.cs`
- [ ] 修改 `CommTT.Application/Services/ConnectionManager.cs`
- [ ] `CommTT.Tests.Unit/` 新增 Logger 相关单元测试

---
*关联 OpenSpec change: [enhance-logging-system](../openspec/changes/enhance-logging-system/)*
