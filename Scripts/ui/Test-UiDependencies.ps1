<#
.SYNOPSIS
    Audits NuGet PackageReferences in HidaChat .vbproj files and checks target frameworks and UI dependencies.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

$knownLicenses = @{
    "ExcelDataReader"                     = "MIT"
    "ExcelDataReader.DataSet"             = "MIT"
    "Microsoft.Toolkit.Uwp.Notifications" = "MIT"
    "Microsoft.Web.WebView2"              = "BSD-3-Clause / Microsoft Software License"
    "WPF-UI"                              = "MIT"
    "ScottPlot.WPF"                       = "MIT"
    "LiveChartsCore.SkiaSharpView.WPF"    = "MIT"
    "FlaUI.UIA3"                          = "MIT"
}

$projects = Get-ChildItem -Path $ProjectRoot -Filter "*.vbproj" -Recurse |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|publish)\\' }

Write-Host "=== NuGet Dependency & License Audit ===" -ForegroundColor Cyan
foreach ($proj in $projects) {
    [xml]$xml = Get-Content -Path $proj.FullName
    $tfm = $xml.Project.PropertyGroup.TargetFramework | Select-Object -First 1
    Write-Host "`nProject: $($proj.Name) (TargetFramework: $tfm)" -ForegroundColor Green

    $refs = $xml.SelectNodes("//PackageReference")
    if ($refs.Count -eq 0) {
        Write-Host "  (No external NuGet PackageReferences)" -ForegroundColor Gray
        continue
    }

    $rows = foreach ($r in $refs) {
        $pkg = $r.GetAttribute("Include")
        $ver = $r.GetAttribute("Version")
        $lic = if ($knownLicenses.ContainsKey($pkg)) { $knownLicenses[$pkg] } else { "Verify License" }
        [PSCustomObject]@{
            Package = $pkg
            Version = $ver
            License = $lic
        }
    }
    $rows | Format-Table -AutoSize
}
