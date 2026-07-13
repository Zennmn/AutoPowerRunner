# AutoPowerRunner

轻量的 Windows 托盘工具，用来在当前用户登录后，以管理员权限自动运行你配置的 PowerShell 脚本和 EXE 程序。

## 功能

- 导入 `.ps1` 脚本或 `.exe` 程序
- 使用 Windows 任务计划程序配置管理员权限开机自启
- 开机自启时静默启动，不显示主窗口
- 脚本默认使用隐藏窗口运行
- EXE 和脚本默认继承本程序的管理员权限
- 提供中文窗口界面和系统托盘菜单
- 支持启用/禁用任务、手动运行、停止运行中的任务
- 保存任务配置、最近运行状态和日志

## 管理员自启说明

程序通过 Windows Task Scheduler 创建名为 `AutoPowerRunner` 的计划任务：

- 触发器：当前用户登录
- 运行级别：最高权限
- 启动参数：`--silent-startup`

首次开启管理员自启时需要确认一次 UAC。配置成功后，之后登录 Windows 时不需要每次手动确认 UAC。

## 数据位置

- 配置：`%APPDATA%\AutoPowerRunner\config.json`
- 日志：`%LOCALAPPDATA%\AutoPowerRunner\logs\app.log`

## 从源码构建

需要 Windows 和 .NET 8 SDK。

```powershell
dotnet test AutoPowerRunner.sln
dotnet build AutoPowerRunner.sln -c Release
```

发布单文件自包含便携版：

```powershell
dotnet publish src\AutoPowerRunner\AutoPowerRunner.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist\AutoPowerRunner-Portable-win-x64
```

## 许可证

MIT License
