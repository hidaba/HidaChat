<#
.SYNOPSIS
    Generates a complete inventory of WPF Windows, UserControls, and UI control counts in HidaChat.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

$xamlFiles = Get-ChildItem -Path $ProjectRoot -Filter "*.xaml" -Recurse |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|data|publish|artifacts)\\' }

$inventory = foreach ($file in $xamlFiles) {
    $content = Get-Content -Path $file.FullName -Raw
    $rootMatch = [regex]::Match($content, '<(Window|UserControl|ResourceDictionary|Application)\b')
    $rootType = if ($rootMatch.Success) { $rootMatch.Groups[1].Value } else { "Unknown" }

    $widthMatch = [regex]::Match($content, '\bWidth="([^"]+)"')
    $heightMatch = [regex]::Match($content, '\bHeight="([^"]+)"')
    $minWMatch = [regex]::Match($content, '\bMinWidth="([^"]+)"')
    $minHMatch = [regex]::Match($content, '\bMinHeight="([^"]+)"')

    $buttons = ([regex]::Matches($content, '<(Button|ui:ModernButton)\b')).Count
    $inputs = ([regex]::Matches($content, '<(TextBox|PasswordBox|ComboBox|CheckBox|ui:ModernTextBox|ui:ModernComboBox|ui:SearchBox)\b')).Count
    $grids = ([regex]::Matches($content, '<(DataGrid|ui:ModernDataGrid)\b')).Count
    $cards = ([regex]::Matches($content, '<ui:ModernCard\b|CardBorderStyle')).Count

    [PSCustomObject]@{
        RelativePath = $file.FullName.Substring($ProjectRoot.Length + 1)
        RootType     = $rootType
        Size         = if ($widthMatch.Success -and $heightMatch.Success) { "$($widthMatch.Groups[1].Value)x$($heightMatch.Groups[1].Value)" } else { "Auto/NA" }
        MinSize      = if ($minWMatch.Success -and $minHMatch.Success) { "$($minWMatch.Groups[1].Value)x$($minHMatch.Groups[1].Value)" } else { "None" }
        Buttons      = $buttons
        Inputs       = $inputs
        DataGrids    = $grids
        Cards        = $cards
        Lines        = ($content -split "`r?`n").Count
    }
}

Write-Host "=== HidaChat WPF UI Inventory ===" -ForegroundColor Cyan
$inventory | Sort-Object RootType, RelativePath | Format-Table -AutoSize
Write-Host "Total XAML files: $($inventory.Count)" -ForegroundColor Green
