# 命令行参数

NvwUpd 支持以下命令行参数：

## 用法

```
NvwUpd.exe [选项]
```

## 选项

| 参数 | 值 | 说明 |
|---|---|---|
| `--background` | *（无）* | 以后台模式启动。主窗口在启动时隐藏，应用在系统托盘中静默运行，定期检查驱动更新。 |
| `--lang` | `<语言代码>` | 覆盖界面语言。接受 BCP 47 语言代码（如 `en-US`、`zh-CN`）。 |

## 示例

### 以后台模式启动

```powershell
NvwUpd.exe --background
```

应用启动后不显示主窗口，驻留在系统托盘中，根据设定的间隔定期检查 NVIDIA 驱动更新。
右键点击托盘图标可打开主窗口、检查更新、退出程序。

### 以英文界面启动

```powershell
NvwUpd.exe --lang en-US
```

### 以后台模式启动并指定语言

```powershell
NvwUpd.exe --background --lang en-US
```

## 备注

- 参数**不区分大小写**。
- `--background` 参数也被**开机自启动**功能使用。在设置中启用自启动后，会在注册表中创建一条以 `NvwUpd.exe --background` 启动的条目。
- `--lang` 参数是用覆盖当前会话的系统区域设置的方式来作为 debug 参数使用，不会持久保存。
