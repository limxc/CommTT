## Tasks

- [x] 修改 `App.xaml.cs`：将日志初始化移到 `base.OnStartup(e)` 之前
- [x] 修改 `App.xaml.cs`：将异常处理器注册移到 `base.OnStartup(e)` 之前
- [x] 修改 `App.xaml.cs`：将 `_staticLoggerFactory` 改为静态属性 `LoggerFactory`
- [x] 修改 `Bootstrapper.cs`：使用 `App.LoggerFactory` 而不是创建新实例
- [x] 运行格式化命令（如适用）
- [x] 运行测试确认修复