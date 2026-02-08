# Command Line Arguments

NvwUpd supports the following command line arguments:

## Usage

```
NvwUpd.exe [options]
```

## Options

| Argument | Value | Description |
|---|---|---|
| `--background` | *(none)* | Launch in background mode. The main window is hidden on startup and the app runs silently in the system tray, performing periodic update checks. |
| `--lang` | `<culture>` | Override the UI language. Accepts a BCP 47 culture code (e.g., `en-US`, `zh-CN`). |

## Examples

### Start in background mode

```powershell
NvwUpd.exe --background
```

The application starts without showing the main window. It sits in the system tray and periodically checks for NVIDIA driver updates based on the configured interval. 
Right-click the tray icon to open the window, check for updates, or exit.

### Start with English UI

```powershell
NvwUpd.exe --lang en-US
```

### Start in background mode with a specific language

```powershell
NvwUpd.exe --background --lang en-US
```

## Notes

- Arguments are **case-insensitive**.
- The `--background` flag is also used by the **auto-start on login** feature. When enabled in Settings, a registry entry is created that launches `NvwUpd.exe --background` at user login.
- The `--lang` argument is used to debug by overrides the system locale for the current session only. It does not persist across restarts.
