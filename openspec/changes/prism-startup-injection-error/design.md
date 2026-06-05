## Design

### 问题根因

`App.xaml.cs` 的 `OnStartup` 方法中：
1. `base.OnStartup(e)` 首先被调用，触发 Prism 初始化（`RegisterTypes` → `Bootstrapper.RegisterTypes`）
2. 日志工厂在 `base.OnStartup(e)` 之后才初始化
3. 异常处理器在 `base.OnStartup(e)` 之后才注册

结果：Prism 初始化期间的任何异常都不会被捕获或记录。

### 修复方案

**修改 `App.xaml.cs`**：

1. 将日志初始化移到 `base.OnStartup(e)` 之前
2. 将异常处理器注册移到 `base.OnStartup(e)` 之前
3. 将 `_staticLoggerFactory` 改为静态属性，供 `Bootstrapper` 访问

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    // 1. 先初始化日志
    var logDir = Path.Combine(AppContext.BaseDirectory, "Logs", DateTime.Now.ToString("yyyyMMdd"));
    Directory.CreateDirectory(logDir);
    var loggerConfig = new LoggerConfiguration()
        .MinimumLevel.Information()
        .WriteTo.File(
            path: Path.Combine(logDir, "runtime.txt"),
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {SourceContext}: {Message}{NewLine}{Exception}");
    var serilogLogger = loggerConfig.CreateLogger();
    LoggerFactory = new LoggerFactory();
    LoggerFactory.AddSerilog(serilogLogger);

    // 2. 注册异常处理器
    DispatcherUnhandledException += OnDispatcherUnhandledException;
    AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
    TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

    // 3. 最后调用 base.OnStartup (触发 Prism 初始化)
    base.OnStartup(e);
}
```

**修改 `Bootstrapper.cs`**：

1. 在 `RegisterTypes` 中使用 `App.LoggerFactory` 而不是创建新的日志工厂
2. 移除重复的日志配置代码

### 文件变更范围

- `src/CommTT.Shell/App.xaml.cs`：调整代码顺序，暴露静态日志工厂
- `src/CommTT.Shell/Bootstrapper.cs`：使用 `App.LoggerFactory`，移除重复配置

### 验证要点

1. 启动时日志文件被创建
2. Prism 初始化期间的异常能被记录
3. 现有异常处理器正常工作
4. 容器中的 `ILoggerFactory` 与静态日志工厂是同一实例
