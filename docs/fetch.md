# Fetch part notes
This is official api from NVIDIA, we can get the specific GPU codename's driver list.

Test what format it returns first, by powershell commands.

We get `osid` of Windows 11 by this:
```pwsh
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; (Invoke-RestMethod -Uri "https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=4" -Headers @{"User-Agent"="Mozilla/5.0"}).LookupValueSearch.LookupValues.LookupValue | ? { $_.Name -eq "Windows 11" -or $_.Name -match '^Windows\s*11(\s*64-?bit)?$' }
```

It returns:
```text
Code Name       Value
---- ----       -----
10.0 Windows 11 135
```

Seems `osid` is 135.

Get `psid` of RTX 40 series by this:
```pwsh
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; (Invoke-RestMethod -Uri "https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=2" -Headers @{"User-Agent"="Mozilla/5.0"}).LookupValueSearch.LookupValues.LookupValue | ? { $_.Name -match 'GeForce' -and $_.Name -match 'RTX\s*40' -and $_.Name -match 'Notebook' } | select -First 1 -ExpandProperty Value
```

It returns:
```text
129
```
Thus the `psid` is 129.

And the `pfid` for `GeForce RTX 4060 Laptop GPU`

> note: sometimes, the laptop GPUs name string have no NVIDIA prefix, but desktop edition have

```pwsh
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; (Invoke-RestMethod -Uri "https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3" -Headers @{"User-Agent"="Mozilla/5.0"}).LookupValueSearch.LookupValues.LookupValue | ? { $_.Name -eq "GeForce RTX 4060 Laptop GPU" } | select -First 1 -ExpandProperty Value
```

Result is:
```text
1007
```

Construct URL like this:
```
https://www.nvidia.com/Download/processFind.aspx?dtcid=1&lang=zh-hans&lid=1&osid=135&pfid=1007&psid=129
```

![webpage](image.png)

A html table, click first link, enter `https://www.nvidia.com/en-us/drivers/details/xxxx/` page, can download driver by click `Download` button.