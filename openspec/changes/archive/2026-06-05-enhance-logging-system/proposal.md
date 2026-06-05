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
