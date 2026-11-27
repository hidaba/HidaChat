<#
.SYNOPSIS
    Performs static layout, High DPI, and responsive design checks across all WPF Window XAML files.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

$windowFiles = Get-ChildItem -Path $ProjectRoot -Filter "*.xaml" -Recurse |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|data|publish|artifacts)\\' } |
    Where-Object { (Get-Content -Path $_.FullName -Raw) -match '<Window\b' }

$report = foreach ($file in $windowFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    $hasMinSize = ($content -match '\bMinWidth=') -or ($content -match 'ResizeMode="NoResize"')
    $hasLayoutRounding = ($content -match 'UseLayoutRounding="True"')
    $hasSnaps = ($content -match 'SnapsToDevicePixels="True"')
    $usesAllowsTransparency = ($content -match 'AllowsTransparency="True"')
    $usesDropShadow = ($content -match '<DropShadowEffect\b')
    $usesDynamicTheme = ($content -match '\{DynamicResource\s+\w+Brush\}')
    $usesCanvas = ($content -match '<Canvas\b')

    [PSCustomObject]@{
        Window            = $file.FullName.Substring($ProjectRoot.Length + 1)
        MinSizeOrFixed    = $hasMinSize
        LayoutRounding    = $hasLayoutRounding
        SnapsToPixels     = $hasSnaps
        DynamicTheme      = $usesDynamicTheme
        LayeredTranspWarn = $usesAllowsTransparency -and $usesDropShadow
        CanvasWarn        = $usesCanvas
    }
}

Write-Host "=== WPF Layout, DPI & Responsiveness Static Check ===" -ForegroundColor Cyan
$report | Format-Table -AutoSize
