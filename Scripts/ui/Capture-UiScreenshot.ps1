<#
.SYNOPSIS
    Captures a PNG screenshot of a running HidaChat window (or the primary screen) into artifacts/ui-screenshots/.
.EXAMPLE
    .\Capture-UiScreenshot.ps1 -Name "showcase-dark" -LaunchShowcase
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Name = "capture",

    [switch]$LaunchShowcase,

    [int]$WaitSeconds = 2,

    [string]$ProjectRoot = ""
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $ProjectRoot "artifacts\ui-screenshots"
if (-not (Test-Path $outDir)) {
    New-Item -Path $outDir -ItemType Directory -Force | Out-Null
}

$proc = $null
if ($LaunchShowcase) {
    $exePath = Join-Path $ProjectRoot "bin\Debug\net9.0-windows10.0.19041.0\HidaChat.exe"
    if (-not (Test-Path $exePath)) {
        throw "Executable not found at $exePath. Build the project first with 'dotnet build'."
    }
    $proc = Start-Process -FilePath $exePath -ArgumentList "--ui-showcase" -PassThru
    Start-Sleep -Seconds $WaitSeconds
}

try {
    $bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $outFile = Join-Path $outDir "$Name-$timestamp.png"
        $bitmap.Save($outFile, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "Screenshot saved: $outFile" -ForegroundColor Green
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}
finally {
    if ($proc -and -not $proc.HasExited) {
        $proc.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 500
        if (-not $proc.HasExited) { $proc.Kill() }
    }
}
