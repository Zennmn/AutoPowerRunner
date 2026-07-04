# 管理员自启动器

一个轻量级 Windows 托盘工具，用于在用户登录后以管理员权限启动脚本和程序。

适合把需要管理员权限的 PowerShell 脚本、后台工具、开发环境初始化脚本等加入登录后自动运行，而不需要每次手动右键“以管理员身份运行”。

## 主要功能

- 添加 PowerShell 脚本（`.ps1`）或可执行程序（`.exe`）
- 登录后以管理员权限自动运行已启用项目
- 支持手动“立即运行”单个项目
- 支持停止最近启动的主进程
- 保存每个项目的最近运行状态、退出码、错误信息和日志
- 托盘常驻，双击托盘图标打开管理器
- 自动保存窗口大小、位置和左右分栏宽度

## 下载和运行

在 GitHub Releases 下载最新的 `AutoElevateLauncher.exe`。

Release 提供的是单文件自包含版本：

- 只需要下载一个 `AutoElevateLauncher.exe`
- 目标电脑不需要额外安装 .NET Runtime
- 建议放到固定目录，例如 `C:\Tools\AutoElevateLauncher\AutoElevateLauncher.exe`
- 不建议放在下载目录或临时目录，因为管理员开机自启任务会记录这个 exe 的路径

首次运行后，程序会出现在系统托盘。

## 使用方法

1. 启动 `AutoElevateLauncher.exe`。
2. 双击托盘图标，或右键托盘图标选择“打开管理器”。
3. 点击“新增脚本”选择 `.ps1`，或点击“新增程序”选择 `.exe`。
4. 按需填写参数和工作目录。
5. 勾选“启用此项目”。
6. 点击“保存”。
7. 点击“配置管理员自启”，首次配置可能会弹出 UAC 确认。

配置完成后，下次登录 Windows 时，本软件会以管理员权限自动启动，并运行所有已启用项目。

## 管理员开机自启如何工作

软件会创建一个 Windows 计划任务：

`AutoElevateLauncher-Manager`

这个计划任务在当前用户登录时触发，并使用 `HighestAvailable` 运行级别启动本软件。软件启动后再读取配置，运行已启用的脚本和程序。

软件不会为每个启动项创建单独的计划任务，也不会写入注册表 Run 项。

## 停止功能说明

“停止”会结束最近一次由本软件启动并记录的主进程 PID。

为了避免误杀进程，当前版本只停止主进程，不会递归停止子进程。例如：

- 如果启动的是 `.exe`，停止的是这个 `.exe` 的进程
- 如果启动的是 `.ps1`，停止的是承载脚本的 `powershell.exe` 进程
- 如果脚本或程序又启动了其他子进程，子进程不会被自动停止

## 数据位置

- 配置：`%AppData%\AutoElevateLauncher\config.json`
- 日志：`%AppData%\AutoElevateLauncher\logs\`

配置文件保存启动项目、管理员自启状态、窗口大小、窗口位置、左右分栏宽度和最近运行信息。

## 添加脚本时的注意事项

软件保存的是脚本或程序的路径，不会把文件复制进配置里。

因此添加后需要保留原文件：

- `.ps1` 文件不能删除或移动
- `.exe` 文件不能删除或移动
- 如果路径变了，需要在软件里重新选择或修改路径

建议把脚本放在固定目录，例如：

`C:\Users\你的用户名\Documents\AutoElevateLauncher\Scripts\`

## 已知限制

- 可执行程序启动成功后会记录进程 ID，并将启动动作视为成功；当前版本不等待长期运行程序退出，也不记录它最终退出码。
- 停止功能只停止主进程，不停止子进程树。
- 日志不会自动清理，会持续累积在日志目录下。
- 旧版本创建的单项目计划任务不会自动删除；如曾使用旧版本，请在 Windows 任务计划程序中手动清理不需要的旧任务。

## 从源码构建

需要：

- Windows
- .NET 8 SDK

运行测试：

```powershell
dotnet test "AutoElevateLauncher.sln" --nologo
```

编译普通 Release：

```powershell
dotnet build "AutoElevateLauncher.sln" -c Release
```

发布单文件自包含版本：

```powershell
dotnet publish "src\AutoElevateLauncher\AutoElevateLauncher.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "publish\win-x64-single"
```

生成文件：

`publish\win-x64-single\AutoElevateLauncher.exe`

## 许可证

本项目使用 MIT License 开源。详见 [LICENSE](LICENSE)。
