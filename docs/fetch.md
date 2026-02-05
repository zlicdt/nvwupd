# Fetch API Documentation

**[中文](fetch-zh_CN.md)**

This document describes how to use NVIDIA's official API to get the driver list for a specific GPU model.

## API Overview

NVIDIA provides public APIs for their driver download page. We can call these APIs directly to fetch driver information.

### API Endpoints

| Endpoint | Purpose |
|----------|---------|
| `lookupValueSearch.aspx?TypeID=2` | Get product series list (psid) |
| `lookupValueSearch.aspx?TypeID=3` | Get product list (pfid) |
| `lookupValueSearch.aspx?TypeID=4` | Get OS list (osid) |
| `processFind.aspx` | Query driver list with parameters |

## Getting Parameter Values

First, test what format the API returns using PowerShell commands.

### Get osid for Windows 11
```pwsh
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; (Invoke-RestMethod -Uri "https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=4" -Headers @{"User-Agent"="Mozilla/5.0"}).LookupValueSearch.LookupValues.LookupValue | ? { $_.Name -eq "Windows 11" -or $_.Name -match '^Windows\s*11(\s*64-?bit)?$' }
```

It returns:
```text
Code Name       Value
---- ----       -----
10.0 Windows 11 135
```

Seems `osid` is **135**.

### Get psid for RTX 40 Series Notebooks
```pwsh
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; (Invoke-RestMethod -Uri "https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=2" -Headers @{"User-Agent"="Mozilla/5.0"}).LookupValueSearch.LookupValues.LookupValue | ? { $_.Name -match 'GeForce' -and $_.Name -match 'RTX\s*40' -and $_.Name -match 'Notebook' } | select -First 1 -ExpandProperty Value
```

It returns:
```text
129
```
Thus the `psid` is **129**.

### Get pfid for GeForce RTX 4060 Laptop GPU

> note: sometimes, the laptop GPUs name string have no NVIDIA prefix, but desktop edition have

```pwsh
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; (Invoke-RestMethod -Uri "https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3" -Headers @{"User-Agent"="Mozilla/5.0"}).LookupValueSearch.LookupValues.LookupValue | ? { $_.Name -eq "GeForce RTX 4060 Laptop GPU" } | select -First 1 -ExpandProperty Value
```

Result is:
```text
1007
```

Thus the `pfid` is **1007**.

## Query Driver List

Construct the URL with the parameters:

```
https://www.nvidia.com/Download/processFind.aspx?dtcid=1&lang=zh-hans&lid=1&osid=135&pfid=1007&psid=129
```

### Parameters

| Parameter | Description | Example |
|-----------|-------------|---------|
| `dtcid` | Driver type: 1=Game Ready, 0=Studio | `1` |
| `lang` | Language code | `en-us`, `zh-hans` |
| `lid` | Language ID | `1` |
| `osid` | Operating System ID | `135` (Windows 11) |
| `pfid` | Product ID | `1007` (RTX 4060 Laptop) |
| `psid` | Product Series ID | `129` (RTX 40 Notebooks) |

### Response Format

The API returns an HTML table with available drivers:

![webpage](image.png)

Click the first link to enter the `https://www.nvidia.com/en-us/drivers/details/xxxx/` page, where you can download the driver by clicking the "Download" button.

## Download URL Format

Driver download URLs follow this pattern:

**Notebook version:**
```
https://us.download.nvidia.com/Windows/{version}/{version}-notebook-win10-win11-64bit-international-dch-whql.exe
```

**Desktop version:**
```
https://us.download.nvidia.com/Windows/{version}/{version}-desktop-win10-win11-64bit-international-dch-whql.exe
```

### Example

```
https://us.download.nvidia.com/Windows/591.86/591.86-notebook-win10-win11-64bit-international-dch-whql.exe
```

## Code Implementation

In NvwUpd, `DriverFetcher.cs` implements the above API calls:

1. First fetch psid and pfid from `TypeID=2` and `TypeID=3`
2. Get osid for current Windows version from `TypeID=4`
3. Call `processFind.aspx` to get driver list
4. Parse HTML response to extract version and driver ID
5. Construct download URL

This approach eliminates the need for hardcoded product IDs - the code works automatically when NVIDIA releases new GPUs.