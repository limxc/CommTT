## Design Overview

为 CommTT 构建一套**零数据库、无查询服务、纯人类可读文本**的轻量级日志系统。设计核心原则：
1. **不阻塞通讯主线程**：所有日志写入均为异步缓冲或轻量文件追加。
2. **仅记录关键流程**：Connect/Disconnect、ReadLoop 异常、ProtocolDispatcher 异常、Send 异常、软件启动/关闭。正常帧收发不记录。
3. **纯文本格式**：所有日志均为人类直接可读的 TXT，不使用 JSON 或结构化格式。
4. **按日期分目录**：统一存放在软件根目录 `Logs/YYYYMMDD/` 下，不自动删除。

## Architecture Decision

### 1. 日志分类与目录结构

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
- **不自动删除**：所有历史日志保留。

### 2. 三类日志均为纯文本格式

| 日志类型 | 实现方式 | 格式示例 |
|---------|---------|---------|
| **runtime.txt** | Serilog File Sink | `2026-06-05 20:45:00 [ERR] Bootstrapper: Configuration load failed — System.IO.FileNotFoundException: ...` |
| **protocol-exception.txt** | 自定义 `ProtocolExceptionLogger` | `2026-06-05 20:45:00 | Serial | COM3 | IOException | Port unavailable | Context: PortName=COM3,BaudRate=9600 | Raw: AA BB CC...` |
| **protocol-state.txt** | 自定义 `ProtocolStateLogger` | `2026-06-05 20:45:00 | Serial | COM3 | Disconnected → Connected | UserAction | Port opened successfully` |

- **纯文本原则**：不使用 JSON、不使用键值对序列化，所有字段以固定分隔符（`|` 或空格）排列，便于人类直接阅读，也便于简单文本工具（grep、awk、Excel）处理。

### 3. 关键日志注入点

```
关键异常捕获点（仅异常，正常帧不记录）
────────────────────────────────────────
ProtocolDispatcher.OnBufferRead()
  ├── splitter/解析异常 → ProtocolExceptionLogger.Write(...)
  └── reader.Complete(ex) 前记录

SerialCommProvider
  ├── ConnectAsync() 失败 → ProtocolExceptionLogger.Write(...)
  ├── SendAsync() 失败 → ProtocolExceptionLogger.Write(...)
  └── ReadLoopAsync() 异常 → ProtocolExceptionLogger.Write(...)

ConnectionManager / SerialCommProvider
  └── 状态变化事件 → ProtocolStateLogger.Write(...)

Bootstrapper / Shell
  └── 启动/关闭/未捕获异常 → Serilog runtime.txt
```

### 4. Serilog 配置（仅 runtime.txt）

- **路径**：`<BaseDirectory>/Logs/<YYYYMMDD>/runtime.txt`
- **最小级别**：`Information`
- **格式**：纯文本，包含时间、级别、类别、消息。例如：
  ```
  2026-06-05 20:45:00 [INF] Bootstrapper: Application started
  2026-06-05 20:45:01 [ERR] SerialCommProvider: Failed to open COM3 — System.IO.IOException: ...
  ```
- **滚动**：不按 Serilog 内置滚动，而是自定义路径生成逻辑（每日首次写入时创建 `YYYYMMDD` 目录）。

### 5. 自定义审计 Logger 设计

- `ProtocolExceptionLogger` 和 `ProtocolStateLogger` 均为 Infrastructure 层单例服务：
  - 内部使用 `StreamWriter` 带 `AutoFlush=false`，通过 `lock` 保证线程安全。
  - 写入方法为同步或异步轻量追加，调用方使用 `Task.Run(...)` 避免阻塞通讯线程。
  - 文件路径每日动态生成：`Path.Combine(AppContext.BaseDirectory, "Logs", DateTime.Now.ToString("yyyyMMdd"), "protocol-exception.txt")`

### 6. 无查询服务

- **不引入 `IDebugLogQueryService` 或任何查询接口**。
- 日志设计目标为**人类直接打开 TXT 文件阅读**。
- 若未来需要程序化处理，可直接用文本工具（grep、PowerShell `Select-String`）处理纯文本，无需专用查询服务。

### 7. 通讯性能保障

- **无同步数据库 I/O**：完全去掉 EF Core / SQLite / SaveChanges。
- **无查询服务开销**：去掉 IDebugLogQueryService 及其内存过滤逻辑。
- **异步文件追加**：Serilog 自带异步队列；自定义审计 Logger 使用 `StreamWriter` 缓冲追加。
- **不记录正常帧**：避免 I/O 膨胀。
- **截断策略**：`RawDataSnapshot` 截断至 128 字节，以十六进制字符串形式记录。
- **无频繁 LogContext**：仅在 `ConnectAsync` 作用域内 Push 一次 `ProtocolType`/`PortName`。

## Data Flow

```
┌─────────────────┐     ┌────────────────────────────┐
│  SerialComm     │────▶│ ProtocolExceptionLogger    │──▶ Logs/YYYYMMDD/protocol-exception.txt
│  ProtocolDispatcher  │ │ (纯文本，固定分隔符格式)    │
│  ConnectionManager   │ └────────────────────────────┘
│                     │     ┌────────────────────────────┐
│  StateChanged 事件   │────▶│ ProtocolStateLogger        │──▶ Logs/YYYYMMDD/protocol-state.txt
│                     │     │ (纯文本，固定分隔符格式)     │
└─────────────────────┘     └────────────────────────────┘
           │
           │   ILogger<T> (仅软件运行时观测)
           ▼
      ┌────────────┐
      │  Serilog   │──▶ Logs/YYYYMMDD/runtime.txt
      │  File Sink │      (纯文本)
      └────────────┘
```

## Non-Goals

- 不引入数据库（SQLite、EF Core 均不引入）。
- 不实现查询服务或日志检索 API。
- 不实现远程日志中心（如 ELK、Loki）集成。
- 不实现日志自动清理（由用户手动管理历史日志目录）。
- 不在正常帧收发路径记录日志（避免 I/O 膨胀）。
- 不使用 JSON、XML、CSV 等结构化格式（纯文本人类可读优先）。
