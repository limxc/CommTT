## 架构决策

### 1. 技术栈选型：.NET 10 WPF + Prism + MaterialDesignInXamlToolkit + SQLite

**决策**：后端采用 .NET 10，UI 采用 WPF + Prism 框架 + MaterialDesignInXamlToolkit，本地数据使用 SQLite。

**理由**：
- **Prism**：提供模块化（Modularity）、导航（Navigation）、事件聚合（EventAggregator）、依赖注入（DI），天然支持插件式架构
- **MaterialDesignInXamlToolkit**：提供现代化的 Material Design 风格控件，减少自定义样式工作量，UI 专业度高
- **WPF**：与 Prism 深度集成，数据绑定（MVVM）适合实时监控场景
- **.NET 10**：原生支持 Serial、TCP/UDP，MQTT 有成熟库（MQTTnet），性能足以支撑万级并发压力测试
- **SQLite**：零部署、单文件，适合现场工程师直接拷贝使用，无需安装数据库服务
- **整洁架构 + 插件式**：Domain / Application / Infrastructure / UI 分层，Provider 作为独立模块动态加载

**替代方案考虑**：
- MAUI/WinUI 3：跨平台但串口支持较弱，Prism 对 WPF 支持最成熟
- Electron + Node.js：跨平台但性能不足，压力测试场景 CPU 占用高
- Python + PyQt：开发快但部署困难，长期运行稳定性不如 .NET
- 纯 WPF MVVM（无 Prism）：缺乏模块化机制，难以实现运行时加载 Provider 插件

### 2. 整洁架构（Clean Architecture）+ 插件式 Provider 架构

**决策**：采用整洁架构分层 + Prism 模块系统，实现 Provider 的物理隔离与运行时动态加载。

**分层结构**：

```
┌─────────────────────────────────────────┐
│        UI Layer (Presentation)          │
│   WPF + Prism Modules + MaterialDesign  │
│   ├─ Shell (主窗口、导航、区域管理)      │
│   ├─ SerialModule (串口配置/收发/监控)   │
│   └─ CommonModule (共享控件、主题)        │
├─────────────────────────────────────────┤
│      Application Layer (Prism Core)     │
│   ├─ 调度引擎 (ConnectionManager)       │
│   ├─ 性能指标采集 (MetricsAggregator)   │
│   ├─ 告警引擎 (AlertEngine)             │
│   └─ 用例服务 (调试/测试/监控)           │
├─────────────────────────────────────────┤
│      Domain Layer (Pure .NET)           │
│   ├─ ICommProvider 接口契约             │
│   ├─ CommMetrics / ProviderConfig 模型  │
│   ├─ AlertRule 领域规则                 │
│   └─ 领域事件 (ConnectionStateChanged)  │
├─────────────────────────────────────────┤
│    Infrastructure Layer (Providers)     │
│   ├─ SerialProvider (本期实现)          │
│   ├─ TcpProvider    (预留，后续迭代)     │
│   ├─ UdpProvider    (预留，后续迭代)     │
│   ├─ MqttProvider   (预留，后续迭代)     │
│   ├─ WsProvider     (预留，后续迭代)     │
│   ├─ CanProvider    (预留，后续迭代)     │
│   └─ DataPersistence (SQLite + EF Core) │
└─────────────────────────────────────────┘
```

**依赖规则**：
- 箭头永远向下：UI → Application → Domain，Infrastructure → Domain（通过接口依赖反转）
- Domain 层纯 .NET，不依赖任何 UI 框架或数据库
- Application 层协调用例，但不直接操作硬件或数据库
- Infrastructure 层实现所有外部依赖：串口、数据库、文件系统

**Provider 插件化机制**：
- 每个 Provider 是一个独立的 **Prism Module**（`IModule`）+ .NET 类库
- `ICommProvider` 接口定义在 **Domain 层**，所有 Provider 在 Infrastructure 层实现
- Prism `ModuleCatalog` 从配置或目录加载 Provider DLL，实现运行时动态加载
- UI 层通过 Prism `RegionManager` 动态注入 Provider 专属的配置面板和监控视图
- **本期范围**：仅实现 `SerialProviderModule`，其他 Provider 预留接口位置，后续版本迭代实现

```
Provider 动态加载流程：
┌──────────────┐    ┌──────────────────┐    ┌────────────────┐
│   启动时     │───▶│ 读取 provider.cfg │───▶│ ModuleCatalog  │
│              │    │ (激活的Provider列表)│    │ 加载对应 DLL   │
└──────────────┘    └──────────────────┘    └───────┬────────┘
                                                    │
                      ┌─────────────────────────────┘
                      ▼
               ┌──────────────┐
               │ 注册到容器    │
               │ ICommProvider │
               │ + 注册UI视图  │
               └──────┬───────┘
                      │
                      ▼
               ┌──────────────┐
               │ 导航菜单动态  │
               │ 添加串口入口  │
               └──────────────┘
```

**理由**：
- **整洁架构**：核心逻辑独立于 UI 和基础设施，测试无需启动 WPF 窗口
- **Prism 模块化**：Serial 的所有功能（Provider + Views + ViewModels）打包在一个 Module 中，后续 TCP/MQTT 等以相同模式追加，零侵入现有代码
- **依赖注入**：Provider 注册为接口，Application 层通过 `ICommProvider` 操作，不关心具体实现
- **范围可控**：本期专注 Serial，架构已为后续协议预留扩展点

### 3. 性能指标采集架构

**决策**：采用无锁环形缓冲区（Ring Buffer）+ 后台聚合线程，避免高频通讯时锁竞争。

```
┌──────────────────────────────────────────────┐
│              指标采集流程                     │
├──────────────────────────────────────────────┤
│                                               │
│   Provider 层                                  │
│   ┌─────────┐   原子操作                      │
│   │ Send()  │──▶ tx_bytes += len               │
│   │ Recv()  │──▶ rx_bytes += len               │
│   │ 定时戳   │──▶ latency_samples[]           │
│   └─────────┘                                  │
│        │                                      │
│        ▼ 每 100ms 聚合                         │
│   ┌─────────────┐                             │
│   │ MetricsAgg  │──▶ throughput_bps           │
│   │  (后台线程)  │──▶ avg_latency_ms          │
│   └─────────────┘──▶ packet_loss_rate         │
│        │                                      │
│        ▼                                      │
│   UI 绑定 (INotifyPropertyChanged)             │
│                                               │
└──────────────────────────────────────────────┘
```

**理由**：
- 高频收发（如压力测试 10k+ msg/s）时，锁竞争会导致性能抖动
- 原子计数器 + 后台聚合，保证采集精度同时不影响通讯吞吐
- 100ms 聚合周期兼顾 UI 刷新流畅度与统计精度

### 4. 三种工作模式设计

**决策**：在核心引擎之上封装三种独立但共享底层能力的运行模式。

| 模式 | 核心行为 | 典型用户 | 数据保留策略 |
|------|---------|---------|------------|
| 开发调试 | 手动连接、单条/批量发送、实时原始报文查看 | 固件/协议开发者 | 会话级，可选保存 |
| 压力测试 | 配置并发数、持续时间、自动发送预设载荷 | QA/测试工程师 | 完整测试报告，长期保留 |
| 生产监控 | 长期连接、状态看板、阈值告警 | 运维工程师 | 最近7天高频 + 历史聚合 |

**理由**：
- 三种场景差异大，独立 UI 布局减少认知负担
- 共享底层 Provider 和 Metrics，保证行为一致性
- 数据保留策略不同，避免生产监控数据淹没测试数据库

### 5. 告警引擎规则表达

**决策**：采用简单表达式规则，而非复杂 DSL。

```
示例规则：
- latency_ms > 100
- packet_loss_rate > 0.05 AND connection_count < 10
- throughput_bps < 1024 * 100
```

**理由**：
- 足够覆盖 95% 的工业监控场景
- 解析简单，用户无需学习复杂语法
- 未来可扩展为更复杂的规则引擎

## 数据流

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   UI Layer   │────▶│  Core Engine │────▶│  Providers   │
│  (WPF/XAML)  │     │              │     │              │
│              │◀────│ 调度/指标/告警│◀────│ (Serial/TCP  │
└──────────────┘     └──────────────┘     │  /MQTT/CAN)  │
      │                                    └──────────────┘
      │                                           │
      ▼                                           ▼
┌──────────────┐                          ┌──────────────┐
│  SQLite DB   │                          │ 硬件/网络设备 │
│ 配置/日志/报告│                          │              │
└──────────────┘                          └──────────────┘
```

### 6. Provider 异步 IO 与多连接模型

**决策**：所有 Provider 采用**纯异步 API**（`async/await` + `CancellationToken`），核心引擎通过**连接 ID** 统一管理多实例并发生命周期。

**Serial 异步实现**：
- 不直接使用 `SerialPort.Read/Write`（同步阻塞 API），而是基于 `SerialPort.BaseStream` 的 `ReadAsync` / `WriteAsync` 实现真正的异步 IO
- 发送：`await BaseStream.WriteAsync(buffer, ct)`
- 接收：后台 `Task` 循环 `await BaseStream.ReadAsync(buffer, ct)`，通过 `Channel<T>` 或 `IAsyncEnumerable` 向引擎推送数据
- 优点：不占用线程池线程阻塞等待，UI 线程零阻塞，高吞吐下 CPU 占用低

**多连接支持**：
- 每个 `ICommProvider` 实例代表**一个独立连接**（如一个 TCP 客户端、一个串口 COM3、一个 MQTT 会话）
- 核心引擎维护 `Dictionary<string, ICommProvider>`（key 为连接 ID），支持同时运行：
  - N 个 TCP 客户端连接不同服务器
  - M 个串口（COM3、COM4、COM5...）同时收发
  - K 个 MQTT 连接不同 Broker
- 压力测试模式下，引擎批量创建同一类型的多个 Provider 实例模拟并发

```
核心引擎连接池视图：
┌─────────────────────────────────────────┐
│         ConnectionManager               │
├─────────────────────────────────────────┤
│  conn-1: SerialProvider  (COM3, 115200) │
│  conn-2: TcpProvider     (192.168.1.5)  │
│  conn-3: TcpProvider     (192.168.1.6)  │
│  conn-4: MqttProvider    (broker:1883)  │
│  ...                                    │
│  conn-N: TcpProvider     (压力测试#9998) │
└─────────────────────────────────────────┘
```

**理由**：
- 异步 IO 是高性能桌面应用的必要条件，同步阻塞会导致 UI 卡顿且无法支撑压力测试
- 多连接能力是生产监控的核心需求（同时监测多个设备/端口）
- 连接 ID 抽象让 UI 层无需关心底层协议，统一以「连接」视角操作

### 7. UI 架构：Prism Region + Material Design 主题

**决策**：采用 Prism `Region` 导航系统 + `MaterialDesignInXamlToolkit` 主题。

**布局设计**：
- **Shell 主窗口**：左侧 `NavigationRegion`（动态加载激活的 Provider 导航项），右侧 `ContentRegion`（当前 Provider 的主内容区）
- **Serial 模块视图**：
  - 配置视图：端口选择下拉框（自动扫描）、波特率/数据位/校验/停止位配置卡
  - 收发视图：发送区（文本/十六进制切换、定时发送）、接收区（滚动日志、暂停/清屏）、快捷命令列表
  - 监控视图：吞吐量卡片、连接状态指示灯（Material Design 风格）
- **主题**：支持暗色/亮色切换（Material Design 内置主题管理器）

**理由**：
- Prism Region 机制让 UI 布局与模块解耦：Serial 模块注册自己的视图到指定 Region，Shell 无需硬编码
- Material Design 提供专业的工业软件观感，减少自定义样式开发量
- 暗色主题适合工控现场长时间盯屏，减少视觉疲劳

## 关键非功能性决策

- **并发量目标**：Serial 支持同时打开系统所有可用 COM 端口（典型 16~256 个，取决于 USB 集线器）；TCP/WebSocket 等后续 Provider 目标 10,000 并发
- **延迟精度**：亚毫秒级采样，聚合后 UI 显示 1ms 精度
- **UI 刷新**：数据面板 10Hz（100ms），图表 5Hz（200ms），日志面板根据消息量自适应节流
- **部署方式**：单文件 exe + SQLite db，无需安装，支持 xcopy 部署
- **范围边界**：本期仅实现 Serial Provider 及配套 UI，其他协议（TCP/UDP/MQTT/WebSocket/CAN）在架构中预留接口与模块位置，后续迭代实现
