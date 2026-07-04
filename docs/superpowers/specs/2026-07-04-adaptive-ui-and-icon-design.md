# Adaptive UI And Icon Design

## Purpose

Fix the current WinForms layout bugs and give the application a real icon. The app remains a lightweight Chinese Windows tray tool, but the manager window must behave like a normal resizable desktop application.

## Problems To Fix

- The default window height can cut off the bottom action buttons.
- Enlarging the window leaves awkward proportions and does not improve the form layout enough.
- Closing and reopening the manager returns to the hard-coded default size.
- The right-side details panel is a single vertical stack with no scrolling or bottom action bar.
- The empty project list shows a large gray grid area instead of an intentional empty state.
- The app has no custom icon; window and tray still use the default system application icon.

## Goals

- Keep WinForms and the current project architecture.
- Make the manager window self-adjusting and usable at default size.
- Preserve the left project list / right details structure.
- Add a minimum window size to prevent unusable layouts.
- Save and restore window size, position, and splitter distance.
- Make the right details section scroll when space is limited.
- Keep action buttons visible at the bottom of the details side.
- Add an empty-state message for the project list.
- Add a custom application icon used by the executable, manager window, and tray icon.

## Non-Goals

- No switch from WinForms to WPF, Avalonia, WinUI, or MAUI.
- No new third-party UI framework.
- No dashboard redesign or multi-page navigation.
- No installer packaging changes.
- No dark mode in this pass.

## Layout Design

The manager window uses a three-level structure:

1. Root vertical layout.
2. Header area with title and global status.
3. Main split area with left project list and right details.

### Root Window

- Title: `管理员自启动器`.
- Default size: `1120 x 720`.
- Minimum size: `920 x 600`.
- Start position: center screen on first launch.
- Restore last saved size, position, and splitter distance when available.
- If saved bounds are off-screen, fall back to centered default size.

### Header Area

The top header is a fixed-height panel.

Left side:

- App icon, 32x32.
- App name: `管理员自启动器`.
- Subtitle: `登录后以管理员权限启动脚本和程序`.

Right side:

- Permission label: `当前权限：管理员` or `当前权限：普通用户`.
- Self-start label: `管理员开机自启已配置` or `尚未配置管理员开机自启`.
- `配置管理员自启` button only appears when self-start is not configured.

### Left Pane

The left pane contains:

- Section title: `启动项目`.
- Toolbar row: `新增脚本`, `新增程序`, `删除`.
- `DataGridView` filling the remaining area.
- Empty-state overlay or placeholder label: `还没有启动项目。点击“新增脚本”或“新增程序”添加。`

DataGridView behavior:

- Read-only.
- Full-row selection.
- No row headers.
- No user add/delete rows.
- Columns: `名称`, `类型`, `启用`, `最近状态`.
- The `最近状态` column fills remaining width.

### Right Pane

The right pane contains:

- Section title: `项目详情`.
- A scrollable details form panel.
- A fixed bottom action bar.

The scrollable form panel contains:

- Setup card for administrator self-start status.
- Fields: `名称`, `类型`, `路径`, `参数`, `工作目录`, `启用此项目`.
- Status fields: `最近启动`, `最近结束`, `退出码`, `状态`, `最后错误`.

The bottom action bar contains:

- `保存`.
- `立即运行`.
- `停止`.
- `打开日志`.

Bottom actions stay visible when the form panel scrolls.

## Window State Persistence

Extend `StartupConfig` with:

- `ManagerWindowWidth`.
- `ManagerWindowHeight`.
- `ManagerWindowLeft`.
- `ManagerWindowTop`.
- `ManagerSplitterDistance`.

Save these values when the manager form closes.

Restore rules:

- Use saved width/height only when they are at least the minimum size.
- Use saved position only when the saved rectangle intersects a current screen working area.
- Use saved splitter distance only when it leaves both panes at least 280 pixels wide.
- If saved values are invalid, use defaults.

## Icon Design

Create `src/AutoElevateLauncher/Assets/app.ico`.

Visual direction:

- Deep blue rounded shield as the base shape.
- White upward arrow / launch mark in the center.
- Small golden lightning accent to suggest quick elevated startup.
- Simple high-contrast shapes that remain readable at 16x16 tray size.

Icon usage:

- Set `<ApplicationIcon>Assets\app.ico</ApplicationIcon>` in the project file.
- Use the icon for `MainForm.Icon`.
- Use the icon for `NotifyIcon.Icon`.

## Error Handling

- If icon loading fails, fall back to `SystemIcons.Application`.
- If saving window state fails, do not block app closing.
- If restored bounds are invalid, use default bounds and continue.

## Testing Plan

Unit tests:

- Window layout state rejects sizes smaller than the minimum.
- Window layout state rejects off-screen bounds.
- Window layout state accepts valid saved bounds.
- Splitter distance restore keeps both panes above the minimum width.

Manual checks:

- Default window shows all bottom buttons without clipping.
- Resizing wider expands the list and detail panes naturally.
- Resizing shorter keeps bottom actions visible and scrolls details.
- Closing and reopening restores window size, position, and splitter distance.
- Empty list shows the empty-state text.
- EXE, window, and tray all show the custom icon.

## Documentation

README should mention that the app stores UI window state in `%AppData%\AutoElevateLauncher\config.json` together with startup item config.
