<#
.SYNOPSIS
    Builds HidaChat and runs the full automated regression suite plus UI static checks.
#>
[CmdletBinding()]
param(
    [string]$ProjectRoot = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}

Write-Host "1/4 Building HidaChat (WPF VB.NET)..." -ForegroundColor Cyan
dotnet build (Join-Path $ProjectRoot "HidaChat.vbproj") -v minimal
if ($LASTEXITCODE -ne 0) { throw "HidaChat build failed." }

Write-Host "`n2/4 Running automated regression suite (HidaChat.SessionTests)..." -ForegroundColor Cyan
dotnet run --project (Join-Path $ProjectRoot "Tests\HidaChat.SessionTests.vbproj")
if ($LASTEXITCODE -ne 0) { throw "Regression test suite failed." }

Write-Host "`n3/4 Running UI layout & DPI static checks..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "Test-UiLayouts.ps1") -ProjectRoot $ProjectRoot

Write-Host "`n4/4 Auditing NuGet dependencies..." -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "Test-UiDependencies.ps1") -ProjectRoot $ProjectRoot

Write-Host "`nAll UI build and regression checks completed successfully." -ForegroundColor Green
