<!-- Comet Handoff: design phase -->
<!-- Change: comm-test-platform -->
<!-- Generated: 2026-06-04 -->
<!-- Mode: compact -->

# OpenSpec → Superpowers 交接包（Design 阶段）

## 上游事实源（Upstream Sources）

| 文件 | 路径 | SHA256 | 模式 |
|------|------|--------|------|
| proposal.md | openspec/changes/comm-test-platform/proposal.md | AC58FB67...8B38F3F | compact |
| design.md | openspec/changes/comm-test-platform/design.md | EEE93915...A54845 | compact |
| tasks.md | openspec/changes/comm-test-platform/tasks.md | 1D3FBDAF...D7BB3F | compact |

---

## 确定性摘录（Deterministic Excerpts）

### proposal.md — Why + What + Capabilities

**Why**: 工业自动化和物联网开发中，工程师需要与多种通讯协议（串口、TCP/UDP、MQTT、WebSocket、CAN）进行交互测试。缺乏统一测试平台。

**What（本期 v1.0）**:
- 基于 .NET 10 WPF + Prism + MaterialDesignInXamlToolkit 的统一测试工具
- **整洁架构（Clean Architecture）** + **插件式架构（Prism Module）**
- **本期范围**：仅实现 **Serial（串口）** 完整测试功能
- 核心引擎：ConnectionManager、MetricsAggregator、AlertEngine
- 三种工作模式（Serial 场景）：开发调试、压力测试、生产监控
- 数据持久化：SQLite + EF Core

**Capabilities（本期）**:
- `core-engine`: 调度引擎、指标采集、告警
- `provider-serial`: 串口异步 IO、多端口并发
- `ui-desktop`: WPF + Prism + MaterialDesign Shell + Serial 面板
- `data-persistence`: SQLite 配置/日志/告警存储

**Capabilities（后续预留）**:
- `provider-tcp-udp`, `provider-mqtt`, `provider-websocket`, `provider-can`

**Impact**: 新项目初始化；依赖 Prism.Wpf、MaterialDesignInXamlToolkit、System.IO.Ports、EF Core SQLite；Provider 零侵入扩展。

### design.md — 高层架构决策

**1. 技术栈**: .NET 10 WPF + Prism + MaterialDesignInXamlToolkit + SQLite + DryIoc

**2. 整洁架构 + 插件式 Provider**:
- 四层：Domain → Application → Infrastructure → Presentation
- 依赖方向永远向下；Infrastructure 通过接口依赖反转
- Provider = 独立 Prism Module（`IModule`），运行时通过 `ModuleCatalog` 加载
- 本期仅实现 `SerialProviderModule`，其他预留接口位置

**3. 性能指标采集**: 原子计数器（`Interlocked`）+ 后台 100ms 聚合线程 → UI 绑定

**4. 三种工作模式**: 开发调试（会话级数据）、压力测试（完整报告）、生产监控（7天历史）

**5. 告警引擎**: 简单表达式规则（如 `latency_ms > 100`）

**6. Provider 异步 IO + 多连接**:
- 纯异步 API（`async/await` + `CancellationToken`）
- Serial: `SerialPort.BaseStream.ReadAsync/WriteAsync` + 后台 `Task` + `Channel<T>`
- 多连接：`Dictionary<string, ICommProvider>`，支持同时打开系统所有 COM 端口

**7. UI 架构**: Prism `Region` 导航；Shell 左侧 NavigationRegion + 右侧 ContentRegion；Material Design 暗色/亮色主题

**关键非功能性决策**:
- Serial 支持同时打开系统所有可用 COM 端口
- 延迟精度：亚毫秒采样，UI 显示 1ms
- UI 刷新：数据面板 10Hz，图表 5Hz
- 部署：单文件 exe + SQLite，xcopy

### tasks.md — 任务边界

共 15 项任务，按 Clean Architecture 分层组织：
1. 项目初始化（四层 Solution）
2. Prism 框架配置
3. MaterialDesign 主题配置
4. Domain 层抽象（ICommProvider、CommMetrics、AlertRule）
5. Application 层引擎（ConnectionManager、MetricsAggregator、AlertEngine）
6. Infrastructure 层数据持久化（SQLite + EF Core）
7. Serial Provider 实现（异步 IO + 模块封装）
8. UI Shell（Prism Region）
9. Serial UI 模块（配置/收发/监控视图）
10. 开发调试模式
11. 压力测试模式（Serial 场景）
12. 生产监控模式（Serial 场景）
13. 测试验证（单元/集成/压力）
14. 打包与文档

---

## 设计边界与约束

- **不要重新定义需求**：OpenSpec 产物是上游事实源
- **不要重写 proposal/spec**：基于交接包做深度技术设计
- **Spec Patch 规则**：如发现 delta spec 缺少验收场景，只能提出 Spec Patch 并回写 OpenSpec delta spec；不要在 Design Doc 中创建第二份需求 spec
- **范围收缩已确认**：本期只实现 Serial，其他协议仅预留接口

## 需要深度设计的技术领域

1. **Serial Provider 详细实现**：Channel 容量与背压、数据包边界（流式协议分帧）、异常处理与重连、发送队列设计、十六进制/文本转换边界
2. **ICommProvider 接口设计**：API 签名、状态机、配置契约、事件模型
3. **ConnectionManager 并发模型**：连接池线程安全、生命周期状态机、 Dispose 模式
4. **MetricsAggregator 精度与性能**：100ms 聚合算法、原子计数器溢出保护、UI 绑定线程模型
5. **AlertEngine 规则解析**：表达式 AST、运算符优先级、短路求值
6. **Prism Module 加载机制**：ModuleCatalog 配置格式、失败回退、热加载 vs 冷加载
7. **SQLite 与 EF Core**：Code First 迁移、连接字符串配置、并发访问（WPF 多线程）
8. **UI 数据流**：Prism EventAggregator vs 直接绑定、大数据量日志的虚拟化/节流

<!-- END HANDOFF -->
