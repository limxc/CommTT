# Comet Design Handoff

- Change: enhance-logging-system
- Phase: design
- Mode: compact
- Context hash: 212c782f89d4d85e8bae4d200ef622729bdc4e05f98d406e988ecd3b5da53a5d

Generated-by: comet-handoff.sh

OpenSpec remains the canonical capability spec. This handoff is a deterministic, source-traceable context pack, not an agent-authored summary.

## openspec/changes/enhance-logging-system/proposal.md

- Source: openspec/changes/enhance-logging-system/proposal.md
- Lines: 1-36
- SHA256: 15122adc58d709a53969460972d46e89e714ace7af04be24793ff3d8edf768ac

```md
## Why

当前 CommTT 缺乏结构化的日志记录机制，异常信息仅通过抛出或事件零散传播，通讯状态变化无持久化记录。这导致：(1) 人工调试时难以追溯故障根因；(2) AI agent 自动诊断时缺少可消费的异常与状态上下文；(3) 无法审查各协议在不同时间点的通讯健康状况。

## What Changes

- 引入基于 `Microsoft.Extensions.Logging` + `Serilog` 的轻量级日志基础设施，**仅用于软件运行时观测日志输出到纯文本 TXT 文件**。
- 建立 **软件运行错误及其他日志**：记录软件级异常、启动/关闭事件、配置错误等，供人工及 agent 诊断。输出到软件根目录 `Logs/YYYYMMDD/runtime.txt`（纯文本）。
- 建立 **协议解析相关错误异常日志**：在协议解析、串口 I/O、连接管理等**关键流程**捕获并记录异常，包含异常类型、消息、上下文快照、时间戳。输出到 `Logs/YYYYMMDD/protocol-exception.txt`（纯文本）。
- 建立 **通讯状态变化日志**：记录各协议连接状态机变化事件（`Disconnected → Connecting → Connected → Error`），包含旧状态、新状态、触发原因、时间戳，用于通讯状况统计与审查。输出到 `Logs/YYYYMMDD/protocol-state.txt`（纯文本）。
- **零数据库引入，无查询服务**：所有日志均为纯文本 TXT，人工直接打开阅读，不引入任何查询接口或服务。
- **所有日志统一为纯文本格式**，不使用 JSON，便于人类直接阅读。

## Capabilities

### New Capabilities
- `structured-logging`: 日志基础设施（软件运行时观测日志纯文本输出）
- `runtime-error-logging`: 软件运行错误及其他纯文本日志记录
- `protocol-exception-logging`: 协议解析相关错误异常纯文本日志记录
- `protocol-state-logging`: 通讯状态变化纯文本日志记录

### Modified Capabilities
- *(无现有 spec 需要修改)*

## Impact

- **新增依赖**：`Serilog`、`Serilog.Sinks.File`、`Microsoft.Extensions.Logging.Abstractions`
- **修改文件**：
  - `ProtocolDispatcher.cs`（关键异常捕获与日志注入）
  - `SerialCommProvider.cs`（状态变化与关键 I/O 异常日志）
  - `ConnectionManager.cs`（状态桥接与日志）
  - `Bootstrapper.cs`（Serilog 配置）
  - 新增 `CommTT.Infrastructure/Logging/ProtocolExceptionLogger.cs`
  - 新增 `CommTT.Infrastructure/Logging/ProtocolStateLogger.cs`
- **无 Breaking Change**：现有事件机制保留，日志为新增观测面，不阻塞通讯主路径。
- **删除/不引入**：不引入 `IDebugLogQueryService`、`DebugLogQueryService` 及任何查询接口。
```

## openspec/changes/enhance-logging-system/design.md

- Source: openspec/changes/enhance-logging-system/design.md
- Lines: 1-121
- SHA256: b4177f9afde1198df0b1e820826c09cac867aaf26d368dedd166380e2fc164ca

[TRUNCATED]

```md
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

```

Full source: openspec/changes/enhance-logging-system/design.md

## openspec/changes/enhance-logging-system/tasks.md

- Source: openspec/changes/enhance-logging-system/tasks.md
- Lines: 1-11
- SHA256: 9e6ba1f2635b13ba8b0a71556d8a0b350079ccbcc631acd2392f25a38db8c162

```md
- [ ] 引入日志 NuGet 包依赖（Serilog、Serilog.Sinks.File、Microsoft.Extensions.Logging.Abstractions）
- [ ] 创建 `ProtocolExceptionLogger`（Infrastructure 层），负责向 `Logs/<YYYYMMDD>/protocol-exception.txt` 写入纯文本
- [ ] 创建 `ProtocolStateLogger`（Infrastructure 层），负责向 `Logs/<YYYYMMDD>/protocol-state.txt` 写入纯文本
- [ ] 在 Bootstrapper 中配置 Serilog：软件运行时观测日志输出到 `Logs/<YYYYMMDD>/runtime.txt`，按天自动创建目录
- [ ] 在 `SerialCommProvider` 注入 `ILogger<SerialCommProvider>`（runtime 日志）并调用 `ProtocolExceptionLogger` / `ProtocolStateLogger` 记录关键异常与状态变化
- [ ] 在 `ProtocolDispatcher` 注入 `ILogger<ProtocolDispatcher>`（runtime 日志）并在解析异常时调用 `ProtocolExceptionLogger`
- [ ] 在 `ConnectionManager` 桥接 `StateChanged` 事件并调用 `ProtocolStateLogger`
- [ ] 在 `Bootstrapper` / `App.xaml.cs` 中配置全局未捕获异常处理器，将异常写入 runtime.txt
- [ ] 确保审计日志写入不阻塞通讯线程（使用 `StreamWriter` 异步锁或 `FileStream` 缓冲）
- [ ] 运行应用并验证目录结构：`Logs/<YYYYMMDD>/` 下生成 `runtime.txt`、`protocol-exception.txt`、`protocol-state.txt`，且均为纯文本格式
- [ ] 验证纯文本可读性：直接用记事本打开三类日志文件，确认人类可直接阅读
```

## openspec/changes/enhance-logging-system/specs/protocol-exception-logging/spec.md

- Source: openspec/changes/enhance-logging-system/specs/protocol-exception-logging/spec.md
- Lines: 1-26
- SHA256: b844e687ba58b0ad3dcc2cfcc902191151f34ae29746d3afe7d574bbdf796a2d

```md
## ADDED Requirements

### Requirement: Protocol exception capture at critical boundaries only
The system SHALL record exceptions occurring in `ProtocolDispatcher.OnBufferRead`, `SerialCommProvider.ConnectAsync`, `SerialCommProvider.SendAsync`, and `SerialCommProvider.ReadLoopAsync` into the protocol exception log. Normal frame dispatch SHALL NOT trigger protocol exception logging.

#### Scenario: Protocol parse failure
- **WHEN** `ProtocolDispatcher` encounters a parsing exception
- **THEN** a protocol exception log line is written to `Logs/<YYYYMMDD>/protocol-exception.txt` in plain text format, containing timestamp, protocol type, port name, exception type, message, context, and truncated raw data snapshot

#### Scenario: Serial connect failure
- **WHEN** `SerialCommProvider.ConnectAsync` throws due to port unavailable
- **THEN** a protocol exception log line is created with the exception details and current serial config context

### Requirement: Protocol exception log file format and location
The system SHALL persist protocol exception logs to daily TXT files at `<BaseDirectory>/Logs/<YYYYMMDD>/protocol-exception.txt`, using plain text format with fixed delimiter layout (timestamp | protocol | port | exception type | message | context | raw data), with daily directory creation and no automatic deletion.

#### Scenario: File creation on first exception
- **WHEN** the first exception of a new day is logged
- **THEN** the system creates the `Logs/<YYYYMMDD>/` directory and `protocol-exception.txt`, then appends the plain text line

### Requirement: Context in plain text protocol exception logs
The system SHALL include ambient contextual properties (e.g., current connection config snapshot) in plain text format within the protocol exception log line.

#### Scenario: Serial port config context
- **WHEN** an exception occurs during serial communication
- **THEN** the log line includes context such as `PortName=COM3,BaudRate=9600,Parity=None,DataBits=8`
```

## openspec/changes/enhance-logging-system/specs/protocol-state-logging/spec.md

- Source: openspec/changes/enhance-logging-system/specs/protocol-state-logging/spec.md
- Lines: 1-26
- SHA256: 1d67c8ccb40f10626e16ead4cf6888414eccdaa4626d8c5c25bcdcbfd061014a

```md
## ADDED Requirements

### Requirement: Protocol state transition recording
The system SHALL record every `ConnectionState` transition event for each registered `ICommProvider` into the protocol state log, capturing old state, new state, trigger reason, and timestamp.

#### Scenario: Serial connect transition
- **WHEN** `SerialCommProvider.ConnectAsync` successfully opens the port
- **THEN** a state log line is appended to `Logs/<YYYYMMDD>/protocol-state.txt` in plain text format with timestamp, protocol type, connection ID, old state, new state, trigger reason, and details

#### Scenario: Disconnect transition
- **WHEN** `SerialCommProvider.DisconnectAsync` is invoked or the read loop terminates
- **THEN** a state log line is appended with `NewState=Disconnected` and `TriggerReason` equal to `UserAction` or `IoException` accordingly

### Requirement: Protocol state log file format and location
The system SHALL persist protocol state logs to daily TXT files at `<BaseDirectory>/Logs/<YYYYMMDD>/protocol-state.txt`, using plain text format with fixed delimiter layout (timestamp | protocol | connection ID | old state → new state | trigger reason | details), with daily directory creation and no automatic deletion.

#### Scenario: File creation on first state change
- **WHEN** the first state transition of a new day occurs
- **THEN** the system creates the `Logs/<YYYYMMDD>/` directory and `protocol-state.txt`, then appends the plain text line

### Requirement: Trigger reason classification
The system SHALL classify every state transition with a `TriggerReason` enum value: `UserAction`, `IoException`, `ProtocolError`, `Timeout`, or `GracefulClose`.

#### Scenario: Read loop failure
- **WHEN** the serial read loop terminates due to an `IOException`
- **THEN** the resulting state log line uses `TriggerReason=IoException`
```

## openspec/changes/enhance-logging-system/specs/runtime-error-logging/spec.md

- Source: openspec/changes/enhance-logging-system/specs/runtime-error-logging/spec.md
- Lines: 1-15
- SHA256: 95019132562e654bb828c750464c54e7cb7f9f935397b43a0012bc74c46dd1d5

```md
## ADDED Requirements

### Requirement: Runtime error logging
The system SHALL record software runtime errors, unhandled exceptions, startup/shutdown events, and configuration errors to the runtime log file.

#### Scenario: Unhandled exception in Shell
- **WHEN** an unhandled exception occurs in the WPF Shell main thread
- **THEN** the exception details are written to `Logs/<YYYYMMDD>/runtime.txt` via Serilog

### Requirement: Runtime log human-readable format
The system SHALL write runtime logs in plain text format, each line containing timestamp, level, category, and message, suitable for direct human reading without parsing tools.

#### Scenario: Reading runtime log
- **WHEN** a developer opens `Logs/20260504/runtime.txt` in a text editor
- **THEN** each line is human-readable, e.g. `2026-06-05 20:45:00 [ERR] Bootstrapper: Configuration load failed — System.IO.FileNotFoundException: ...`
```

## openspec/changes/enhance-logging-system/specs/structured-logging/spec.md

- Source: openspec/changes/enhance-logging-system/specs/structured-logging/spec.md
- Lines: 1-22
- SHA256: 9bc8c725b8acc867281de5f4a4341bd029d63dc094cc9f8d0dbfccaed63f30ea

```md
## ADDED Requirements

### Requirement: Logging abstraction based on M.E.L
The system SHALL use `Microsoft.Extensions.Logging.ILogger<T>` as the primary logging abstraction for all application and infrastructure components.

#### Scenario: Constructor injection
- **WHEN** a service is instantiated via DI container
- **THEN** the constructor receives a non-null `ILogger<T>` instance specific to its type

### Requirement: Serilog backend for runtime logs only
The system SHALL configure Serilog as the concrete logging backend during application bootstrap, outputting software runtime observation logs to `Logs/<YYYYMMDD>/runtime.txt` under the application base directory. Serilog SHALL NOT be used for protocol audit logs.

#### Scenario: Bootstrap registration
- **WHEN** the Bootstrapper initializes the Shell module
- **THEN** Serilog is registered as the logging provider and outputs runtime logs to `<BaseDirectory>/Logs/<YYYYMMDD>/runtime.txt`

### Requirement: Runtime log file path and rotation
The system SHALL write runtime logs to a daily TXT file under `Logs/<YYYYMMDD>/runtime.txt`, where `<YYYYMMDD>` is the current local date in `yyyyMMdd` format. The system SHALL NOT automatically delete historical runtime log files.

#### Scenario: Daily directory creation
- **WHEN** the first log event of a new day is emitted
- **THEN** a new directory `Logs/<YYYYMMDD>/` is created and `runtime.txt` is placed inside it
```

