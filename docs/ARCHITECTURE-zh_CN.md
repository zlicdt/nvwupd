# NvwUpd 项目架构文档

本文档提供 NvwUpd 项目架构的全面概述，旨在帮助 AI 助手和开发者快速理解代码库。

## 目录

- [项目概述](#项目概述)
- [技术栈](#技术栈)
- [项目结构](#项目结构)
- [核心组件](#核心组件)
- [数据流](#数据流)
- [API 集成](#api-集成)
- [构建说明](#构建说明)
- [关键设计决策](#关键设计决策)

## 项目概述

NvwUpd 是一个 Windows 桌面应用程序，允许用户在不需要 GeForce Experience 的情况下检查和更新 NVIDIA GPU 驱动程序。它使用 NVIDIA 官方公开 API 获取驱动程序信息。

### 主要功能
- 通过 WMI 自动检测 GPU
- 从 NVIDIA API 动态查询产品 ID（无硬编码映射）
- 支持 Game Ready 和 Studio 驱动
- Windows 通知提醒
- 基于 WinUI 3 的现代 Fluent Design UI

## 技术栈

| 组件 | 技术 |
|------|------|
| 框架 | .NET 8.0 |
| UI | WinUI 3 (Windows App SDK 1.6) |
| 目标平台 | Windows 10 1809+ / Windows 11 |
| 架构 | 仅 x64 |
| 设计模式 | MVVM + 依赖注入 |
| MVVM 工具包 | CommunityToolkit.Mvvm 8.4.0 |
| 系统托盘 | H.NotifyIcon.WinUI |

### 项目文件关键设置

```xml
<TargetFramework>net8.0-windows10.0.22621.0</TargetFramework>
<Platforms>x64</Platforms>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
<WindowsPackageType>None</WindowsPackageType>
<UseWinUI>true</UseWinUI>
```

## 项目结构

```
NvwUpd/
├── Core/                      # 核心业务逻辑
│   ├── Interfaces.cs          # 所有核心接口
│   ├── GpuDetector.cs         # 通过 WMI 检测 GPU
│   ├── DriverFetcher.cs       # NVIDIA API 集成
│   ├── DriverDownloader.cs    # HTTP 下载（带进度）
│   └── DriverInstaller.cs     # 静默安装驱动
├── Models/
│   └── DriverModels.cs        # GpuInfo, DriverInfo, DriverType
├── Services/
│   ├── IServices.cs           # 服务接口
│   ├── NotificationService.cs # Windows 通知
│   └── UpdateChecker.cs       # 定期更新检查
├── ViewModels/
│   ├── ViewModelBase.cs       # ViewModel 基类
│   ├── MainViewModel.cs       # 主窗口 ViewModel
│   └── UpdateDialogViewModel.cs
├── App.xaml / App.xaml.cs     # 应用入口，DI 配置
├── MainWindow.xaml / .cs      # 主 UI 窗口
└── docs/                      # 文档
```

## 核心组件

### 1. GpuDetector (`Core/GpuDetector.cs`)

**用途**：从系统检测 NVIDIA GPU 信息。

**方法**：使用 WMI（Windows 管理规范）查询：
```csharp
"SELECT * FROM Win32_VideoController WHERE AdapterCompatibility LIKE '%NVIDIA%'"
```

**关键逻辑**：
- 解析 PNP 设备 ID 以提取 VEN_xxxx 和 DEV_xxxx
- 将 Windows 驱动版本格式转换为 NVIDIA 格式：
  - Windows: `31.0.15.9144` → NVIDIA: `591.44`（取最后5位数字，格式化为 xxx.xx）
- 通过关键词检测笔记本 GPU："Laptop"、"Mobile"、"Max-Q"

**返回**：包含名称、驱动版本、设备 ID 和笔记本标志的 `GpuInfo` 对象。

### 2. DriverFetcher (`Core/DriverFetcher.cs`)

**用途**：从 NVIDIA 官方 API 获取最新驱动信息。

**使用的 API 端点**：
| 端点 | 用途 |
|------|------|
| `lookupValueSearch.aspx?TypeID=2` | 获取产品系列 (psid) |
| `lookupValueSearch.aspx?TypeID=3` | 获取产品 ID (pfid) |
| `lookupValueSearch.aspx?TypeID=4` | 获取操作系统 ID (osid) |
| `processFind.aspx` | 获取驱动列表 |

**流程**：
1. 获取并缓存产品系列列表（TypeID=2）
2. 获取并缓存产品列表（TypeID=3）
3. 动态匹配 GPU 名称以查找 psid 和 pfid
4. 使用参数查询 `processFind.aspx`
5. 解析 HTML 响应以提取驱动信息
6. 构造下载 URL

**下载 URL 模式**：
```
https://us.download.nvidia.com/Windows/{版本}/{版本}-{notebook|desktop}-win10-win11-64bit-international-dch-whql.exe
```

**关键设计**：所有产品 ID 都是从 NVIDIA API 动态获取的，没有硬编码映射。这确保了当 NVIDIA 发布新 GPU 时代码可以自动工作。

### 3. DriverDownloader (`Core/DriverDownloader.cs`)

**用途**：下载驱动文件并报告进度。

**特性**：
- 使用 `HttpClient` 进行 HTTP 下载
- 通过 `IProgress<double>` 报告进度
- 下载到临时目录
- 返回下载文件的路径

### 4. DriverInstaller (`Core/DriverInstaller.cs`)

**用途**：安装下载的驱动程序。

**方法**：使用静默标志运行 NVIDIA 安装程序：
```csharp
ProcessStartInfo {
    FileName = installerPath,
    Arguments = "-s -noreboot",  // 静默安装，不重启
    UseShellExecute = true,
    Verb = "runas"  // 请求管理员权限提升
}
```

## 数据流

```
┌─────────────────┐
│   应用程序      │
│   启动          │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  GpuDetector    │  ──► WMI 查询 ──► GpuInfo
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
│  版本比较       │  当前版本 vs 最新版本
└────────┬────────┘
         │
         ▼ (如果有更新)
┌─────────────────┐
│DriverDownloader │ ──► NVIDIA CDN ──► .exe 文件
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│DriverInstaller  │ ──► 运行安装程序 ──► 驱动已更新
└─────────────────┘
```

## API 集成

详细 API 文档请参阅 [fetch-zh_CN.md](fetch-zh_CN.md)。

### 关键参数

| 参数 | 描述 | 获取方式 |
|------|------|----------|
| `psid` | 产品系列 ID | 将 GPU 名称与 TypeID=2 响应匹配 |
| `pfid` | 产品 ID | 将 GPU 名称与 TypeID=3 响应匹配 |
| `osid` | 操作系统 ID | TypeID=4 响应（Windows 11 = 135） |
| `dtcid` | 驱动类型 | 1 = Game Ready, 0 = Studio |
| `lid` | 语言 ID | 1 = 英语 |

### 产品匹配逻辑

对于名为 "NVIDIA GeForce RTX 4060 Laptop GPU" 的 GPU：

1. **系列匹配**：在 TypeID=2 中搜索 "GeForce" + "RTX 40" + "Notebook"
   - 结果："GeForce RTX 40 Series (Notebooks)" → psid = 129

2. **产品匹配**：在 TypeID=3 中搜索 "RTX 4060 Laptop GPU"
   - 结果："GeForce RTX 4060 Laptop GPU" → pfid = 1007

3. **笔记本检测**：GPU 名称包含 "Laptop" → 下载 URL 使用 `notebook`

## 构建说明

### 要求
- Visual Studio 2022 或 VS Code
- .NET 8.0 SDK
- Windows 10 SDK (10.0.22621.0)

### 构建命令

```powershell
# 调试构建
dotnet build -c Debug -p:Platform=x64

# 发布构建
dotnet build -c Release -p:Platform=x64

# 运行
.\bin\x64\Debug\net8.0-windows10.0.22621.0\NvwUpd.exe
```

### 重要构建说明

1. **平台必须是 x64**：项目使用 `WindowsAppSDKSelfContained`，需要指定平台。
2. **WindowsPackageType=None**：作为未打包的 Win32 应用运行，而非 MSIX。
3. **调试日志**：调试构建会将日志写入应用目录下的 `debug.log`。

## 关键设计决策

### 1. 无硬编码产品 ID

早期版本使用硬编码的 psid/pfid 映射。现已改为动态 API 查询，原因是：
- 自动支持未来的 GPU
- 减少维护负担
- 确保准确性

### 2. 使用 WMI 检测 GPU

选择 WMI 而非 NVAPI，因为：
- 不需要原生依赖
- 即使未安装 NVIDIA 驱动也能工作
- 实现更简单

### 3. HTML 解析 vs JSON API

`processFind.aspx` 端点返回 HTML（而非 JSON）。我们使用正则表达式解析它，因为：
- 这是 nvidia.com 使用的官方公开 API
- 不需要身份验证
- 返回完整的驱动列表

### 4. 下载 URL 构造

我们直接构造下载 URL，而不是从网页抓取：
- 模式一致且有文档记录
- 避免需要 JavaScript 渲染
- 更快更可靠

### 5. 依赖注入

使用 Microsoft.Extensions.DependencyInjection：
- 组件间松耦合
- 便于测试/模拟
- 一致的服务生命周期

## 调试

### 启用调试日志

调试日志在 `App.xaml.cs` 中默认启用：
```csharp
var logPath = Path.Combine(AppContext.BaseDirectory, "debug.log");
_logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
Console.SetOut(_logWriter);
```

### 常见问题

1. **"未找到 NVIDIA GPU"**：检查是否已安装 NVIDIA 驱动
2. **"找不到产品"**：GPU 名称与 API 不匹配 - 查看 `debug.log` 了解详情
3. **平台构建错误**：始终使用 `-p:Platform=x64`

## 相关文档

- [ARCHITECTURE.md](ARCHITECTURE.md) - 项目架构（英文）
- [fetch.md](fetch.md) - NVIDIA API 详情（英文）
- [fetch-zh_CN.md](fetch-zh_CN.md) - NVIDIA API 详情（中文）
- [README-zh_CN.md](README-zh_CN.md) - 中文 README
