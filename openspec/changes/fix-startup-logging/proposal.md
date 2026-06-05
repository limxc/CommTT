## Why

应用启动时发生的异常无法被记录到日志文件中。全局异常处理器（`OnDispatcherUnhandledException`、`OnAppDomainUnhandledException`、`OnUnobservedTaskException`）依赖从DI容器解析 `ILogger<App>`，但在启动早期（`RegisterTypes` 之前），容器尚未初始化，导致日志记录失败。这使得调试启动问题变得极其困难。

## What Changes

- 在 `App` 类中添加静态 Serilog logger，用于在容器初始化之前记录日志
- 修改全局异常处理程序，优先使用静态 logger，如果不存在则尝试从容器解析
- 确保在应用启动的任何阶段都能记录异常日志

## Capabilities

### New Capabilities

### Modified Capabilities

## Impact

- `CommTT.Shell\App.xaml.cs`：添加静态 logger，修改异常处理逻辑
- 依赖：Serilog（已在项目中使用）
