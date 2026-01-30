# NvwUpd - NVIDIA Driver Updater

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-blue.svg)](https://www.microsoft.com/windows)

**[中文文档](docs/README-zh_CN.md)**

A lightweight Windows application to check and update NVIDIA GPU drivers without requiring GeForce Experience.

![Screenshot](docs/screenshot.png)

## Features

- 🔍 **Automatic GPU Detection** - Detects your NVIDIA GPU via WMI
- 📡 **Official NVIDIA API** - Uses the same API as nvidia.com/Download
- 🎮 **Driver Type Selection** - Choose between Game Ready Driver and Studio Driver
- 🔔 **Windows Notifications** - Get notified when updates are available
- ⏰ **Periodic Update Checks** - Automatic background checking
- 🎨 **Modern UI** - Built with WinUI 3 and Fluent Design
- 📦 **Standalone** - No GeForce Experience required

## Requirements

- Windows 10 version 1809 or later / Windows 11
- NVIDIA GeForce GPU (GTX 10 series or newer recommended)
- .NET 8.0 Runtime

## Installation

### From Release

1. Download the latest release from [Releases](https://github.com/yourusername/nvwupd/releases)
2. Extract the archive
3. Run `NvwUpd.exe`

### Build from Source

```powershell
# Clone the repository
git clone https://github.com/yourusername/nvwupd.git
cd nvwupd

# Build the project
dotnet build -c Release -p:Platform=x64

# Run the application
.\bin\x64\Release\net8.0-windows10.0.22621.0\NvwUpd.exe
```

## Usage

1. Launch NvwUpd
2. The app will automatically detect your GPU and current driver version
3. Click "Check for Updates" to check for the latest driver
4. Select your preferred driver type (Game Ready or Studio)
5. Click "Download & Install" to update your driver

## How It Works

NvwUpd uses NVIDIA's official download API to fetch driver information:

1. **GPU Detection** - Uses WMI to detect installed NVIDIA GPU
2. **Product Lookup** - Queries NVIDIA API to get product series ID (psid) and product ID (pfid)
3. **Driver Fetch** - Requests driver list from `processFind.aspx` endpoint
4. **Download** - Downloads driver from NVIDIA's CDN
5. **Installation** - Runs silent installation with `-s -noreboot` flags

For technical details, see [Fetch API Documentation](docs/fetch.md).

## Architecture

```
NvwUpd/
├── Core/                   # Core business logic
│   ├── GpuDetector.cs     # GPU detection via WMI
│   ├── DriverFetcher.cs   # NVIDIA API integration
│   ├── DriverDownloader.cs # Download with progress
│   └── DriverInstaller.cs # Silent installation
├── Services/              # Background services
│   ├── NotificationService.cs
│   └── UpdateChecker.cs
├── ViewModels/            # MVVM ViewModels
└── Models/                # Data models
```

## Configuration

Settings are stored in `%APPDATA%\NvwUpd\settings.json`:

```json
{
  "preferredDriverType": "GameReady",
  "checkIntervalHours": 24,
  "autoDownload": false,
  "showNotifications": true
}
```

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

This project is not affiliated with, endorsed by, or sponsored by NVIDIA Corporation. NVIDIA, GeForce, and related marks are trademarks of NVIDIA Corporation.

## Acknowledgments

- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK)
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet)
- [H.NotifyIcon](https://github.com/HavenDV/H.NotifyIcon)
