# Admin Tray Launcher Refactor Design

## Purpose

Refactor Auto Elevate Launcher into a Chinese Windows tray application that starts itself at user login with administrator privileges, then launches configured scripts and programs from that already-elevated process.

The new model removes per-startup-item scheduled task management. The app becomes an administrator-permission launcher rather than a scheduled-task editor for every item.

## Goals

- Make the entire user-facing application Chinese.
- Replace the current rough WinForms layout with a clear tool-style manager window.
- Use one Windows scheduled task, `AutoElevateLauncher-Manager`, to start the tray app with highest privileges at user login.
- Automatically run all enabled startup items once when the elevated tray app starts after login.
- Keep manual actions for save, delete, run now, stop, and open logs.
- Fix the valuable bugs found in review: misleading self-start state, failed task operation state handling, item deletion leaving hidden scheduled tasks, and fragile config writes.
- Keep the app lightweight and portable.

## Non-Goals

- No installer in this refactor.
- No Windows service.
- No per-item scheduled tasks.
- No plugin system, remote control, or multi-user management.
- No full visual redesign beyond a practical Chinese WinForms tool UI.

## Product Model

The app has one privileged entry point:

1. User enables administrator self-start from the tray/menu or manager window.
2. The app creates or updates `AutoElevateLauncher-Manager` as a logon scheduled task with `RunLevel=HighestAvailable`.
3. At login, Windows starts `AutoElevateLauncher.exe` from that task.
4. The tray app detects that it is running as administrator.
5. The tray app automatically launches every enabled item once for that process lifetime.
6. The tray app remains available for manual management from the tray icon.

If the app is started manually without administrator privileges, it still opens, but it clearly shows that automatic launches will not be elevated unless administrator self-start is enabled or the app is run as administrator.

## Startup Item Model

Startup items remain configuration records, not scheduled tasks.

Each item contains:

- `Id`: stable unique identifier.
- `Name`: Chinese display name entered by the user.
- `Type`: PowerShell script or executable program.
- `Path`: full path to `.ps1` or `.exe`.
- `Arguments`: raw user arguments, preserving spaces and quotes.
- `WorkingDirectory`: working directory used at launch.
- `Enabled`: whether the item is included in automatic startup.
- Run status fields: latest start time, finish time, exit code, status, and latest error.

`TaskName` and `TaskSyncStatus` remain in the model during this refactor only for existing config compatibility. The UI will not show them, and launch logic will ignore them.

## Manager Self-Start

`StartManagerAtLogin` means: the administrator tray app scheduled task is known to be enabled.

Default behavior:

- New config defaults `StartManagerAtLogin` to `false`.
- The UI only shows self-start enabled after `AutoElevateLauncher-Manager` is created successfully.
- Disabling self-start only updates config after the scheduled task deletion succeeds.
- If creation or deletion fails, the UI reverts to the previous state and shows a Chinese error message.

Task creation will use a path that can prompt for administrator approval when needed. If the current process lacks administrator rights, the app will start a helper invocation of itself with `runas` for the self-start task operation. If the elevated helper is cancelled or fails, the app will keep the previous state and show a clear Chinese error message.

## Automatic Launch Behavior

On normal tray startup, the app will automatically launch enabled items once.

Rules:

- Only `Enabled=true` items are launched.
- Launch happens once per process lifetime.
- Manual opening of the manager window must not trigger another automatic launch.
- Items run independently: one failing item must not stop later items from launching.
- Logs are written per item under `%AppData%\AutoElevateLauncher\logs\<item-id>\`.
- PowerShell scripts keep stdout, stderr, and exit code logging.
- Executable items continue to report launch success once the process starts, unless a launch exception occurs.

## Chinese UI Design

The main manager window uses a practical two-pane layout.

Left pane:

- Title: `启动项目`.
- A `DataGridView` list with columns: `名称`, `类型`, `启用`, `最近状态`.
- Actions: `新增脚本`, `新增程序`, `删除`.

Right pane:

- Section title: `项目详情`.
- Fields: `名称`, `类型`, `路径`, `参数`, `工作目录`, `启用此项目`.
- Status fields: `最近启动`, `最近结束`, `退出码`, `状态`, `最后错误`.
- Actions: `保存`, `立即运行`, `停止`, `打开日志`.

Top or bottom status area:

- `当前权限：管理员` or `当前权限：普通用户`.
- Self-start status: `管理员开机自启：已启用` or `管理员开机自启：未启用`.
- Action button: `启用管理员自启` or `关闭管理员自启`.

Tray menu:

- `打开管理器`.
- `启用管理员开机自启` checkable item.
- `立即运行所有启用项`.
- `退出`.

Message boxes, validation errors, status labels, button text, README user instructions, and known limitations will be Chinese.

## Error Handling

- Missing name: show `名称不能为空。`
- Missing path: show `路径不能为空。`
- Missing file: show `文件不存在：<path>`.
- Wrong type: show a Chinese message indicating `.ps1` or `.exe` is required.
- Missing working directory: show `工作目录不存在：<path>`.
- Self-start task creation failure: leave the previous state unchanged and show stderr/stdout in a Chinese dialog.
- Self-start task deletion failure: leave the previous state unchanged and show stderr/stdout in a Chinese dialog.
- Item launch failure: log the exception, set item status to failed, and continue launching other enabled items.
- Corrupted config: preserve the bad file and start with an empty config, but avoid silently overwriting the preserved bad content.

## Config Persistence

Config writes will be atomic for normal local use:

1. Serialize to a temporary file in the same directory.
2. Replace or move it into `config.json` only after the temp write succeeds.
3. Keep existing corrupted-config backup behavior.

The automatic runner now lives in the same manager process, which reduces the previous multi-process config race. Manual operations still save config, so the store must avoid partial writes.

## Code Structure

The refactor will keep small, testable units:

- `MainForm`: Chinese manager UI and user interactions.
- `ManagerContext`: tray lifecycle, self-start menu, startup auto-run trigger.
- `StartupItemLauncher` or renamed `ItemRunner`: direct item launch and log/status updates.
- `StartupOrchestrator`: runs all enabled items once and isolates failures.
- `ScheduledTaskService`: only creates/deletes/runs the manager self-start task, not per-item tasks.
- `ConfigStore`: load/save config with atomic writes and corruption recovery.

Avoid large unrelated abstractions. The main objective is to simplify the existing design.

## Migration

Existing configs can contain `TaskName` and `TaskSyncStatus`. The app will ignore those fields for item launching. No per-item task cleanup is performed automatically because deleting unknown user tasks could be surprising. The UI will not display per-item task sync state.

If a user previously created per-item tasks with older versions, they can remove them manually from Windows Task Scheduler. This refactor will document that migration note but will not implement cleanup tooling.

## Testing Plan

Unit tests:

- `StartupConfig` defaults `StartManagerAtLogin` to `false`.
- The orchestrator launches only enabled items.
- The orchestrator continues after one item fails.
- Automatic launch guard prevents duplicate auto-runs in one process lifetime.
- PowerShell argument construction preserves spaces and quotes.
- Executable start info preserves path, arguments, and working directory.
- Validator returns Chinese validation messages for common invalid inputs.
- `TaskXmlBuilder` creates the manager task XML with `HighestAvailable`.

Integration/manual checks:

- Build and test pass.
- Start app normally and verify the UI shows ordinary-user status when not elevated.
- Enable administrator self-start and verify UAC/admin behavior.
- Re-login or run the task and verify enabled items launch once.
- Disable administrator self-start and verify the task is removed or errors are shown.
- Verify the entire visible UI is Chinese.

## Documentation

README will be rewritten in Chinese to describe:

- What the app does.
- How to enable administrator self-start.
- Where config and logs are stored.
- How automatic launch works.
- Known limitations, including executable exit-code behavior and no automatic log cleanup.
