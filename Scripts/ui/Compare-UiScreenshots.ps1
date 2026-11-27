<#
.SYNOPSIS
    Performs pixel-level before/after visual comparison between two PNG screenshots and outputs a diff image.
.EXAMPLE
    .\Compare-UiScreenshots.ps1 -BeforePath .\before.png -AfterPath .\after.png -DiffOutputPath .\diff.png
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BeforePath,

    [Parameter(Mandatory = $true)]
    [string]$AfterPath,

    [string]$DiffOutputPath = "",

    [int]$ChannelThreshold = 8
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $BeforePath)) { throw "Before image not found: $BeforePath" }
if (-not (Test-Path $AfterPath)) { throw "After image not found: $AfterPath" }

$imgA = New-Object System.Drawing.Bitmap($BeforePath)
$imgB = New-Object System.Drawing.Bitmap($AfterPath)
try {
    $width = [Math]::Min($imgA.Width, $imgB.Width)
    $height = [Math]::Min($imgA.Height, $imgB.Height)
    $totalPixels = $width * $height
    $diffPixels = 0

    $diffBitmap = if ($DiffOutputPath) { New-Object System.Drawing.Bitmap($width, $height) } else { $null }
    try {
        for ($y = 0; $y -lt $height; $y++) {
            for ($x = 0; $x -lt $width; $x++) {
                $pA = $imgA.GetPixel($x, $y)
                $pB = $imgB.GetPixel($x, $y)
                $delta = [Math]::Abs($pA.R - $pB.R) + [Math]::Abs($pA.G - $pB.G) + [Math]::Abs($pA.B - $pB.B)
                if ($delta -gt ($ChannelThreshold * 3)) {
                    $diffPixels++
                    if ($diffBitmap) { $diffBitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 220, 38, 38)) }
                } elseif ($diffBitmap) {
                    $gray = [int](($pB.R + $pB.G + $pB.B) / 3)
                    $diffBitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $gray, $gray, $gray))
                }
            }
        }

        if ($diffBitmap -and $DiffOutputPath) {
            $diffBitmap.Save($DiffOutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
    }
    finally {
        if ($diffBitmap) { $diffBitmap.Dispose() }
    }

    $diffPct = if ($totalPixels -gt 0) { [Math]::Round(($diffPixels / $totalPixels) * 100.0, 3) } else { 0 }
    [PSCustomObject]@{
        Dimensions       = "${width}x${height}"
        ChangedPixels    = $diffPixels
        TotalPixels      = $totalPixels
        DifferencePct    = "$diffPct%"
        DiffImageSaved   = if ($DiffOutputPath) { $DiffOutputPath } else { "(none)" }
    } | Format-List
}
finally {
    $imgA.Dispose()
    $imgB.Dispose()
}
