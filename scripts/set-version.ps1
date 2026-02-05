param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Project = "NvwUpd.csproj"
)

if (-not (Test-Path -Path $Project)) {
    throw "Project file not found: $Project"
}

$xml = Get-Content -Path $Project -Raw

function Set-Or-InsertProperty {
    param(
        [string]$Name,
        [string]$Value
    )

    $pattern = "(<{0}>)([^<]*)(</{0}>)" -f [regex]::Escape($Name)
    if ($xml -match $pattern) {
        $script:xml = [regex]::Replace($script:xml, $pattern, "`${1}$Value`${3}")
        return
    }

    $versionPattern = "<Version>[^<]*</Version>"
    if ($script:xml -match $versionPattern) {
        $replacement = "`$&`r`n    <${Name}>${Value}</${Name}>"
        $script:xml = [regex]::Replace($script:xml, $versionPattern, $replacement, 1)
        return
    }

    $propertyGroupPattern = "(<PropertyGroup>\s*)"
    if ($script:xml -match $propertyGroupPattern) {
        $replacement = "`${1}    <${Name}>${Value}</${Name}>`r`n"
        $script:xml = [regex]::Replace($script:xml, $propertyGroupPattern, $replacement, 1)
        return
    }

    throw "Could not find <PropertyGroup> to insert ${Name}."
}

Set-Or-InsertProperty -Name "Version" -Value $Version
Set-Or-InsertProperty -Name "AssemblyVersion" -Value $Version
Set-Or-InsertProperty -Name "FileVersion" -Value $Version

Set-Content -Path $Project -Value $xml -Encoding UTF8
Write-Host "Updated ${Project}: Version=$Version, AssemblyVersion=$Version, FileVersion=$Version"
