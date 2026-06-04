## Why

在工业自动化和物联网开发中，工程师需要频繁与多种通讯协议（串口、TCP/UDP、MQTT、WebSocket、CAN）进行交互测试。目前缺乏一个统一的测试平台来支持开发调试、压力性能测试以及生产环境长期监控。不同协议需要不同的工具，数据无法统一管理，测试流程难以标准化。构建一个集成化的通讯测试平台，能够显著提升调试效率、保证通讯质量，并提供统一的性能度量基准。

## What Changes

- **新建通讯测试平台桌面应用**：基于 .NET 8 WPF + Prism + MaterialDesignInXamlToolkit 的统一测试工具，采用整洁架构（Clean Architecture）+ 插件式架构
- **本期范围（v1.0）**：实现 Serial（串口）通讯测试的完整功能，包括配置、收发、监控
- **架构预留**：定义 `ICommProvider` 统一接口，核心引擎（调度、指标采集、告警）基于接口设计，为 TCP/UDP、MQTT、WebSocket、CAN 等后续协议预留 Provider 模块位置和动态加载机制
- **核心引擎（本期实现）**：
  - 连接管理器：多实例并发生命周期管理（同时打开多个 COM 端口）
  - 性能指标采集：基于 `System.Threading.Interlocked` 原子计数器 + 后台聚合线程
  - 告警引擎：简单表达式规则解析与阈值触发
- **实时性能指标**：Serial 场景下吞吐量（字节/秒）、发送/接收帧数、连接状态、错误计数等实时计算与 Material Design 风格可视化
- **本地数据持久化**：SQLite 存储串口配置、收发日志、告警历史
- **三种工作模式（本期针对 Serial 实现）**：
  - 开发调试模式：手动选择 COM 端口、配置波特率等参数、单条/批量发送、实时原始报文查看（文本/十六进制）
  - 压力测试模式：多个串口同时发送预设载荷、配置发送频率、实时性能图表
  - 生产监控模式：多串口长期连接监控、状态看板、阈值告警（如错误帧率过高）

## Capabilities

### 本期实现 (v1.0)
- `core-engine`: 核心调度引擎（ConnectionManager、MetricsAggregator、AlertEngine）
- `provider-serial`: 串口通讯（RS232/RS485/USB-COM）异步 IO 实现，支持多端口并发
- `ui-desktop`: WPF + Prism + MaterialDesignInXamlToolkit 桌面应用主界面、Serial 配置/收发/监控面板
- `data-persistence`: SQLite 本地数据库、串口配置存储、收发日志、告警历史

### 后续迭代预留（本期定义接口，不实现）
- `provider-tcp-udp`: TCP 客户端/服务器、UDP 单播/组播（架构预留）
- `provider-mqtt`: MQTT 客户端（发布/订阅、QoS 支持）（架构预留）
- `provider-websocket`: WebSocket 客户端/服务器（架构预留）
- `provider-can`: CAN 总线通讯支持（架构预留）

### Modified Capabilities
- 无现有能力修改（本项目为新建项目）

## Impact

- **新项目初始化**：需创建 .NET 8 WPF 解决方案结构，采用 Clean Architecture 分层
- **依赖库**：
  - `Prism.Wpf` + `Prism.DryIoc`（模块化、导航、DI）
  - `MaterialDesignInXamlToolkit`（UI 主题与控件）
  - `System.IO.Ports`（串口，.NET 内置）
  - `Microsoft.EntityFrameworkCore.Sqlite`（数据持久化）
  - 后续 Provider 按需引入：MQTTnet、Kvaser/PEAK CAN 库等
- **架构影响**：
  - 采用 **Clean Architecture + Prism 模块化**，新增协议只需新建一个 Prism Module 实现 `ICommProvider`，零侵入现有代码
  - UI 层通过 Prism Region 动态加载 Provider 专属视图，Shell 主窗口无需修改
- **范围边界**：本期交付完整的 Serial 测试功能，其他协议在架构中预留位置，不引入未实现的依赖
- **长期维护**：Provider 模块独立编译、独立测试、独立发布，符合开闭原则
