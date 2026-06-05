## Context

当前应用使用 Serilog 作为日志框架，在 `Bootstrapper.RegisterTypes()` 中配置。全局异常处理器在 `App.OnStartup()` 中注册，试图从 DI 容器解析 `ILogger<App>` 来记录异常。

问题：如果异常发生在 `RegisterTypes()` 之前（如 XAML 解析错误、静态构造函数异常），容器尚未初始化，`Container?.Resolve<ILogger<App>>()` 返回 null，导致日志丢失。

## Goals / Non-Goals

**Goals:**
- 确保应用启动的任何阶段都能记录异常日志
- 保持现有日志格式和输出路径一致
- 最小化代码变更

**Non-Goals:**
- 不改变日志框架（继续使用 Serilog）
- 不添加新的日志输出目标
- 不修改其他模块的日志逻辑

## Decisions

### Decision 1: 使用静态 Serilog logger 作为后备

**选择**: 在 `App` 类中添加静态 `ILogger` 字段，在应用启动时立即初始化。

**理由**:
- 简单直接，不引入新的依赖
- 静态字段在类加载时即可使用，不依赖容器
- 与现有 Bootstrapper 中的配置保持一致

**备选方案**:
- 使用 `System.Diagnostics.Trace` 作为后备 → 需要额外配置，且格式不一致
- 在异常处理器中直接创建临时 logger → 每次异常都创建新实例，性能差

### Decision 2: 异常处理程序优先级

**选择**: 优先使用静态 logger，如果为 null 则尝试从容器解析，最后使用 `System.Diagnostics.Trace.WriteLine` 作为最终后备。

**理由**:
- 静态 logger 在 `RegisterTypes` 之前就可用
- 容器解析在容器初始化后可用
- `Trace.WriteLine` 是最终保障，确保异常至少能输出到调试窗口

## Risks / Trade-offs

- [Risk] 静态 logger 和容器中的 logger 可能输出到不同文件 → Mitigation: 使用相同的配置模板，输出到同一目录
- [Risk] 静态 logger 未 dispose → Mitigation: 在应用关闭时 dispose
