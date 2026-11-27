<#
.SYNOPSIS
    Scans XAML and VB.NET UI code-behind files for hardcoded hex colors and styling inconsistencies.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = "",
    [switch]$IncludeDesignSystem
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

$files = Get-ChildItem -Path $ProjectRoot -Include "*.xaml", "*.xaml.vb" -Recurse |
    Where-Object {
        $_.FullName -notmatch '\\(bin|obj|data|publish|artifacts)\\' -and
        ($IncludeDesignSystem -or $_.FullName -notmatch '\\UI\\Themes\\')
    }

$hexPattern = '#[0-9a-fA-F]{6,8}\b'
$results = foreach ($file in $files) {
    $content = Get-Content -Path $file.FullName -Raw
    $hexMatches = [regex]::Matches($content, $hexPattern)
    $staticResMatches = [regex]::Matches($content, '\{StaticResource\s+\w+Brush\}')
    $inlineDropShadow = ([regex]::Matches($content, '<DropShadowEffect\b')).Count
    $focusableFalse = ([regex]::Matches($content, 'Focusable="False"')).Count

    if ($hexMatches.Count -gt 0 -or $inlineDropShadow -gt 0 -or $focusableFalse -gt 0) {
        [PSCustomObject]@{
            File             = $file.FullName.Substring($ProjectRoot.Length + 1)
            HardcodedColors  = $hexMatches.Count
            UniqueColors     = ($hexMatches.Value | Sort-Object -Unique).Count
            DropShadows      = $inlineDropShadow
            FocusableFalse   = $focusableFalse
            StaticBrushRefs  = $staticResMatches.Count
        }
    }
}

Write-Host "=== Hardcoded Colors & UI Inconsistency Report ===" -ForegroundColor Cyan
$results | Sort-Object HardcodedColors -Descending | Format-Table -AutoSize
$totalColors = ($results | Measure-Object -Property HardcodedColors -Sum).Sum
Write-Host "Total hardcoded hex color occurrences: $totalColors" -ForegroundColor Yellow
