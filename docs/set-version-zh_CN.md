# set-version.ps1

**[English](set-version.md)**

用于更新 .csproj 文件中的版本号字段。

## 更新内容

- Version
- AssemblyVersion
- FileVersion

三个字段都会被设置为同一个版本号。

## 用法

```powershell
# 在仓库根目录执行
.\scripts\set-version.ps1 -Version 1.2.3
```

也可以指定其他项目文件：

```powershell
.\scripts\set-version.ps1 -Version 1.2.3 -Project path\to\YourProject.csproj
```

## 说明

- 如果属性不存在，会插入到第一个 <PropertyGroup> 中。
- 脚本会以 UTF-8 编码写回文件。
