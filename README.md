# Auto Elevate Launcher

Lightweight Windows tray app for automatically starting PowerShell scripts and executable programs with administrator privileges after user login.

## Requirements

- Windows
- .NET 8 Desktop Runtime for framework-dependent publish builds

## How It Works

Each startup item maps to a Windows scheduled task configured to run with highest privileges at current user logon. Creating or updating an elevated task can show a UAC prompt once. Later logon-triggered runs should not show UAC again.

## Data Locations

- Config: `%AppData%\AutoElevateLauncher\config.json`
- Logs: `%AppData%\AutoElevateLauncher\logs\`

## Usage

1. Start `AutoElevateLauncher.exe`.
2. Double-click the tray icon to open the manager.
3. Add a PowerShell script or executable program.
4. Fill in startup arguments and working directory if needed.
5. Save the item to create or update its scheduled task.
6. Use `Run now` and logs to verify behavior.

The tray menu has a **Start tray app at login** toggle that registers a separate non-elevated logon task so the manager starts automatically. Toggle it once; creation may show a single UAC prompt.

## Known Limitations (v1)

- **Executable items report exit code 0.** Long-running or detaching `.exe` programs are launched and their process id logged, but their final exit code is not captured; status shows `Succeeded` once the process starts.
- **Run-status may be stale across processes.** The manager (tray/UI) and the runner (invoked by scheduled tasks) are separate processes that each read/write `config.json`. If both save near-simultaneously, one party's run-status update can be overwritten. Core auto-elevate behavior is unaffected; only the displayed last-run time/exit code may lag. A refresh (close and reopen the manager) corrects it.
- **No log cleanup.** Logs accumulate under `%AppData%\AutoElevateLauncher\logs\` per item; manage manually for now.