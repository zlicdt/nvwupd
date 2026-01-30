# NvwUpd Architecture Documentation

This document provides a comprehensive overview of the NvwUpd project architecture, designed to help AI assistants and developers understand the codebase quickly.

## Table of Contents

- [Project Overview](#project-overview)
- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [Core Components](#core-components)
- [Data Flow](#data-flow)
- [API Integration](#api-integration)
- [Build Instructions](#build-instructions)
- [Key Design Decisions](#key-design-decisions)

## Project Overview

NvwUpd is a Windows desktop application that allows users to check and update NVIDIA GPU drivers without requiring GeForce Experience. It uses NVIDIA's official public APIs to fetch driver information.

### Key Features
- Automatic GPU detection via WMI
- Dynamic product ID lookup from NVIDIA API (no hardcoded mappings)
- Support for Game Ready and Studio drivers
- Windows toast notifications
- Modern Fluent Design UI with WinUI 3

## Technology Stack

| Component | Technology |
|-----------|------------|
| Framework | .NET 8.0 |
| UI | WinUI 3 (Windows App SDK 1.6) |
| Target | Windows 10 1809+ / Windows 11 |
| Architecture | x64 only |
| Pattern | MVVM with Dependency Injection |
| MVVM Toolkit | CommunityToolkit.Mvvm 8.4.0 |
| System Tray | H.NotifyIcon.WinUI |

### Project File Key Settings

```xml
<TargetFramework>net8.0-windows10.0.22621.0</TargetFramework>
<Platforms>x64</Platforms>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
<WindowsPackageType>None</WindowsPackageType>
<UseWinUI>true</UseWinUI>
```

## Project Structure

```
NvwUpd/
├── Core/                      # Core business logic
│   ├── Interfaces.cs          # All core interfaces
│   ├── GpuDetector.cs         # GPU detection via WMI
│   ├── DriverFetcher.cs       # NVIDIA API integration
│   ├── DriverDownloader.cs    # HTTP download with progress
│   └── DriverInstaller.cs     # Silent driver installation
├── Models/
│   └── DriverModels.cs        # GpuInfo, DriverInfo, DriverType
├── Services/
│   ├── IServices.cs           # Service interfaces
│   ├── NotificationService.cs # Windows toast notifications
│   └── UpdateChecker.cs       # Periodic update checking
├── ViewModels/
│   ├── ViewModelBase.cs       # Base ViewModel class
│   ├── MainViewModel.cs       # Main window ViewModel
│   └── UpdateDialogViewModel.cs
├── App.xaml / App.xaml.cs     # Application entry, DI setup
├── MainWindow.xaml / .cs      # Main UI window
└── docs/                      # Documentation
```

## Core Components

### 1. GpuDetector (`Core/GpuDetector.cs`)

**Purpose**: Detects NVIDIA GPU information from the system.

**Method**: Uses WMI (Windows Management Instrumentation) query:
```csharp
"SELECT * FROM Win32_VideoController WHERE AdapterCompatibility LIKE '%NVIDIA%'"
```

**Key Logic**:
- Parses PNP Device ID to extract VEN_xxxx and DEV_xxxx
- Converts Windows driver version format to NVIDIA format:
  - Windows: `31.0.15.9144` → NVIDIA: `591.44` (last 5 digits as xxx.xx)
- Detects notebook GPUs by keywords: "Laptop", "Mobile", "Max-Q"

**Returns**: `GpuInfo` object with name, driver version, device IDs, and notebook flag.

### 2. DriverFetcher (`Core/DriverFetcher.cs`)

**Purpose**: Fetches latest driver information from NVIDIA's official API.

**API Endpoints Used**:
| Endpoint | Purpose |
|----------|---------|
| `lookupValueSearch.aspx?TypeID=2` | Get product series (psid) |
| `lookupValueSearch.aspx?TypeID=3` | Get product ID (pfid) |
| `lookupValueSearch.aspx?TypeID=4` | Get OS ID (osid) |
| `processFind.aspx` | Get driver list |

**Flow**:
1. Fetch and cache product series list (TypeID=2)
2. Fetch and cache product list (TypeID=3)
3. Match GPU name to find psid and pfid dynamically
4. Query `processFind.aspx` with parameters
5. Parse HTML response to extract driver info
6. Construct download URL

**Download URL Pattern**:
```
https://us.download.nvidia.com/Windows/{version}/{version}-{notebook|desktop}-win10-win11-64bit-international-dch-whql.exe
```

**Key Design**: All product IDs are fetched dynamically from NVIDIA's API. No hardcoded mappings. This ensures the code works automatically when NVIDIA releases new GPUs.

### 3. DriverDownloader (`Core/DriverDownloader.cs`)

**Purpose**: Downloads driver files with progress reporting.

**Features**:
- HTTP download with `HttpClient`
- Progress reporting via `IProgress<double>`
- Downloads to temp directory
- Returns path to downloaded file

### 4. DriverInstaller (`Core/DriverInstaller.cs`)

**Purpose**: Installs downloaded drivers.

**Method**: Runs the NVIDIA installer with silent flags:
```csharp
ProcessStartInfo {
    FileName = installerPath,
    Arguments = "-s -noreboot",  // Silent install, no reboot
    UseShellExecute = true,
    Verb = "runas"  // Request admin elevation
}
```

## Data Flow

```
┌─────────────────┐
│   Application   │
│   Startup       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  GpuDetector    │  ──► WMI Query ──► GpuInfo
└────────┬────────┘
         │
         ▼
┌─────────────────┐     ┌──────────────────────┐
│  DriverFetcher  │ ──► │ NVIDIA API           │
│                 │     │ - lookupValueSearch  │
│                 │     │ - processFind        │
└────────┬────────┘     └──────────────────────┘
         │
         ▼
┌─────────────────┐
│  Compare        │  Current vs Latest Version
│  Versions       │
└────────┬────────┘
         │
         ▼ (if update available)
┌─────────────────┐
│DriverDownloader │ ──► NVIDIA CDN ──► .exe file
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│DriverInstaller  │ ──► Run installer ──► Updated driver
└─────────────────┘
```

## API Integration

See [fetch.md](fetch.md) for detailed API documentation.

### Key Parameters

| Parameter | Description | How to Get |
|-----------|-------------|------------|
| `psid` | Product Series ID | Match GPU name to TypeID=2 response |
| `pfid` | Product ID | Match GPU name to TypeID=3 response |
| `osid` | Operating System ID | TypeID=4 response (Windows 11 = 135) |
| `dtcid` | Driver Type | 1 = Game Ready, 0 = Studio |
| `lid` | Language ID | 1 = English |

### Product Matching Logic

For a GPU named "NVIDIA GeForce RTX 4060 Laptop GPU":

1. **Series Match**: Search TypeID=2 for "GeForce" + "RTX 40" + "Notebook"
   - Result: "GeForce RTX 40 Series (Notebooks)" → psid = 129

2. **Product Match**: Search TypeID=3 for "RTX 4060 Laptop GPU"
   - Result: "GeForce RTX 4060 Laptop GPU" → pfid = 1007

3. **Notebook Detection**: GPU name contains "Laptop" → use `notebook` in download URL

## Build Instructions

### Requirements
- Visual Studio 2022 or VS Code
- .NET 8.0 SDK
- Windows 10 SDK (10.0.22621.0)

### Build Commands

```powershell
# Debug build
dotnet build -c Debug -p:Platform=x64

# Release build
dotnet build -c Release -p:Platform=x64

# Run
.\bin\x64\Debug\net8.0-windows10.0.22621.0\NvwUpd.exe
```

### Important Build Notes

1. **Platform must be x64**: The project uses `WindowsAppSDKSelfContained` which requires a specific platform.
2. **WindowsPackageType=None**: Runs as unpackaged Win32 app, not MSIX.
3. **Debug logging**: Debug builds write to `debug.log` in the app directory.

## Key Design Decisions

### 1. No Hardcoded Product IDs

Previous versions used hardcoded psid/pfid mappings. This was replaced with dynamic API lookup to:
- Support future GPUs automatically
- Reduce maintenance burden
- Ensure accuracy

### 2. WMI for GPU Detection

WMI was chosen over NVAPI because:
- No native dependencies required
- Works without NVIDIA drivers installed
- Simpler implementation

### 3. HTML Parsing vs JSON API

The `processFind.aspx` endpoint returns HTML (not JSON). We parse it with regex because:
- This is the official public API used by nvidia.com
- No authentication required
- Returns complete driver list

### 4. Download URL Construction

Instead of scraping the download URL from web pages, we construct it directly:
- Pattern is consistent and documented
- Avoids need for JavaScript rendering
- Faster and more reliable

### 5. Dependency Injection

Microsoft.Extensions.DependencyInjection is used for:
- Loose coupling between components
- Easy testing/mocking
- Consistent service lifetimes

## Debugging

### Enable Debug Logging

Debug logging is enabled by default in `App.xaml.cs`:
```csharp
var logPath = Path.Combine(AppContext.BaseDirectory, "debug.log");
_logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
Console.SetOut(_logWriter);
```

### Common Issues

1. **"No NVIDIA GPU found"**: Check if NVIDIA drivers are installed
2. **"Could not find product"**: GPU name doesn't match API - check `debug.log` for details
3. **Build error about Platform**: Always use `-p:Platform=x64`

## Related Documentation

- [fetch.md](fetch.md) - NVIDIA API details (English)
- [fetch-zh_CN.md](fetch-zh_CN.md) - NVIDIA API details (Chinese)
- [README-zh_CN.md](README-zh_CN.md) - Chinese README
