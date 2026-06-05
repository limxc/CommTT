# CommTT

CommTT（Communication Test Tool）是一个面向工业自动化和物联网开发的通讯协议测试平台桌面应用，基于 .NET 10 WPF + Prism + MaterialDesignInXamlToolkit 构建，采用 Clean Architecture + 插件式架构。目标是统一串口、TCP/UDP、MQTT、WebSocket、CAN 等多种通讯协议的交互测试，支持开发调试、压力性能测试以及生产环境长期监控，并提供统一的性能度量基准与故障诊断能力。

## 功能特性

- **Serial（串口）通讯测试**：支持 RS232/RS485/USB-COM，配置波特率/校验位/数据位/停止位，单条/批量发送，实时原始报文查看（文本/十六进制）
- **多实例并发连接**：同时打开多个 COM 端口，连接管理器独立生命周期管理
- **实时性能指标**：吞吐量（字节/秒）、发送/接收帧数、连接状态、错误计数等实时计算与可视化
- **告警引擎**：简单表达式规则解析与阈值触发（如错误帧率过高）
- **本地数据持久化**：SQLite 存储串口配置、收发日志、告警历史
- **三种工作模式**：开发调试模式、压力测试模式、生产监控模式
- **结构化日志系统**：
  - 软件运行时观测日志（`Logs/<YYYYMMDD>/runtime.txt`）
  - 协议解析相关错误异常日志（`Logs/<YYYYMMDD>/protocol-exception.txt`）
  - 通讯状态变化审计日志（`Logs/<YYYYMMDD>/protocol-state.txt`）
  - 纯文本格式，按日期分目录，不自动删除，人类直接可读
  - 异步文件写入，不阻塞通讯主线程
- **全局异常捕获**：Dispatcher/AppDomain/Task 未捕获异常统一记录到运行时日志

## 技术栈

- .NET 10 / WPF
- Prism（模块化、导航、DI）
- MaterialDesignInXamlToolkit（UI 主题）
- Serilog + Microsoft.Extensions.Logging（日志）
- Entity Framework Core + SQLite（持久化）
- System.IO.Ports / System.IO.Pipelines（串口与流处理）

## 路线图

### 已实现

- ✅ 核心调度引擎（ConnectionManager、MetricsAggregator、AlertEngine）
- ✅ 串口通讯 Provider（RS232/RS485/USB-COM，多端口并发）
- ✅ WPF 桌面应用主界面（Serial 配置/收发/监控面板）
- ✅ SQLite 本地数据持久化（配置、收发日志、告警历史）
- ✅ 结构化日志系统（runtime / protocol-exception / protocol-state）

### 规划中

- ⏳ TCP 客户端/服务器、UDP 单播/组播 Provider
- ⏳ MQTT 客户端（发布/订阅、QoS 支持）
- ⏳ WebSocket 客户端/服务器 Provider
- ⏳ CAN 总线通讯支持
- ⏳ 日志自动归档/清理策略

## 项目结构

```
src/
├── CommTT.Shell/              # WPF Shell 与 Bootstrapper
├── CommTT.Application/        # 应用层（调度、日志、服务接口）
├── CommTT.Domain/             # 领域层（模型、事件、接口）
├── CommTT.Infrastructure/     # 基础设施层（持久化）
└── CommTT.Modules.SerialProvider/  # 串口 Provider 模块（Prism Module）
```

## 日志目录

应用运行时会在可执行文件所在目录生成 `Logs/<YYYYMMDD>/` 目录，包含：

| 文件 | 用途 |
|------|------|
| `runtime.txt` | 软件运行错误、启动/关闭、配置错误 |
| `protocol-exception.txt` | 协议解析与串口 I/O 关键异常 |
| `protocol-state.txt` | 通讯连接状态机变化 |

所有日志为纯文本格式，历史日志永久保留（不自动删除）。
