param(
    [ValidateSet("major", "minor", "patch", "none")]
    [string]$Bump = "patch",
    [string]$OtaDest = "\\fs1\Annoni-New\IT\OTARepository\Whatsapp",
    [switch]$Beta
)

if ($Beta) {
    $OtaDest = "\\fs1\Annoni-New\IT\OTARepository\WhatsappBeta"
}

$constantsPath = Join-Path $PSScriptRoot "Constants.vb"
$changelogPath = Join-Path $PSScriptRoot "CHANGELOG.md"
$sourceDir = "$PSScriptRoot\bin\Release\net9.0-windows10.0.19041.0"
$otaDest = $OtaDest
$versionFile = Join-Path $otaDest "version.txt"

# --- Parse & bump version ---
$content = Get-Content $constantsPath -Raw
$match = [regex]::Match($content, 'AppVersion As String = "(\d+)\.(\d+)\.(\d+)"')
if (-not $match.Success) { throw "Cannot parse version" }

$major = [int]$match.Groups[1].Value
$minor = [int]$match.Groups[2].Value
$patch = [int]$match.Groups[3].Value

if ($Bump -ne "none") {
    switch ($Bump) {
        "major" { $major++; $minor = 0; $patch = 0 }
        "minor" { $minor++; $patch = 0 }
        "patch" { $patch++ }
    }
}

$newVersion = "$major.$minor.$patch"
$oldVersion = "$($match.Groups[1].Value).$($match.Groups[2].Value).$($match.Groups[3].Value)"

if ($Bump -ne "none") {
    Write-Host "Version: $oldVersion -> $newVersion"
    # Check changelog has entry for new version
    $cl = Get-Content $changelogPath -Raw
    if ($cl -notmatch "## \[$newVersion\]") {
        Write-Host "ERROR: CHANGELOG.md has no entry for version $newVersion !"
        Write-Host "Add it before publishing."
        exit 1
    }
    # Write bumped version to Constants.vb
    $newContent = $content -replace 'AppVersion As String = "[\d\.]+"', "AppVersion As String = `"$newVersion`""
    Set-Content $constantsPath $newContent -NoNewline
} else {
    Write-Host "Version: $newVersion (no bump)"
}

# --- Build ---
Write-Host "Building Release..."
dotnet build -c Release --nologo $PSScriptRoot 2>&1 | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# --- Copy to OTA ---
Write-Host "Copying to OTA..."

# Ensure destination exists
if (-not (Test-Path $otaDest)) { New-Item -ItemType Directory -Path $otaDest -Force | Out-Null }

# Copy root files (exclude user/data files + unnecessary build artifacts)
Get-ChildItem $sourceDir -File | Where-Object {
    $_.Name -notin @("settings.json", "translations_cache.json", "version.txt") -and
    $_.Extension -notin @(".pdb", ".xml")
} | Copy-Item -Destination $otaDest -Force

# Copy subdirectories (images, runtimes)
foreach ($folder in @("images", "runtimes")) {
    $src = Join-Path $sourceDir $folder
    $dst = Join-Path $otaDest $folder
    if (Test-Path $src) {
        if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Path $dst -Force | Out-Null }
        Copy-Item -Path "$src\*" -Destination $dst -Recurse -Force
    }
}

# Post-copy cleanup: remove any stray .pdb / .xml at root
Get-ChildItem $otaDest -File | Where-Object { $_.Extension -in @(".pdb", ".xml") } | Remove-Item -Force

# --- Update version.txt ---
$newVersion | Set-Content $versionFile -NoNewline

Write-Host "OTA publish complete: $newVersion"