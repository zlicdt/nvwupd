# set-version.ps1

**[中文](set-version-zh_CN.md)**

Updates the project version fields in a .csproj file.

## What it updates

- Version
- AssemblyVersion
- FileVersion

All three are set to the same value you provide.

## Usage

```powershell
# From repo root
.\scripts\set-version.ps1 -Version 1.2.3
```

Optionally point to a different project file:

```powershell
.\scripts\set-version.ps1 -Version 1.2.3 -Project path\to\YourProject.csproj
```

## Notes

- The script inserts missing properties into the first <PropertyGroup>.
- The script writes the updated file using UTF-8 encoding.
