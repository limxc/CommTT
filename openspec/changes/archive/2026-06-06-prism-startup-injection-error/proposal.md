## Why

程序启动时，Prism 框架在 `base.OnStartup(e)` 调用期间可能发生异常（例如依赖注入失败、模块加载错误），但这些异常既不会被捕获也不会写入日志文件，导致启动故障难以诊断。

根本原因：`App.xaml.cs` 中日志初始化和异常处理器注册都在 `base.OnStartup(e)` 之后，而 Prism 的初始化（包括 `RegisterTypes` 和 `ConfigureModuleCatalog`）在 `base.OnStartup(e)` 内部执行。

## What Changes

- 将日志初始化移到 `base.OnStartup(e)` 之前，确保 Prism 初始化期间的日志可以被记录
- 将异常处理器注册移到 `base.OnStartup(e)` 之前，确保启动异常被捕获
- 统一 `App.xaml.cs` 中的静态日志工厂与 `Bootstrapper` 注册到容器的日志工厂

## Capabilities

### New Capabilities

（无新增 capability）

### Modified Capabilities

- `runtime-error-logging`: 确保启动阶段（Prism 初始化期间）的异常也能被记录到运行时日志

## Impact

- 受影响文件：`src/CommTT.Shell/App.xaml.cs`（主要修改）
- 可能涉及：`src/CommTT.Shell/Bootstrapper.cs`（日志工厂统一）
- 无 API 变更，无架构调整
