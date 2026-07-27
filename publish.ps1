param(
    [ValidateSet("major", "minor", "patch", "none")]
    [string]$Bump = "none",
    [switch]$SkipGitHub,
    [switch]$Beta
)

$constantsPath = Join-Path $PSScriptRoot "Constants.vb"
$changelogPath = Join-Path $PSScriptRoot "CHANGELOG.md"
$sourceDir = "$PSScriptRoot\bin\Release\net9.0-windows10.0.19041.0"

# --- Read Constants.vb for Version ---
$content = Get-Content $constantsPath -Raw
$match = [regex]::Match($content, 'AppVersion As String = "(\d+)\.(\d+)\.(\d+)"')
if (-not $match.Success) { throw "Cannot parse version from Constants.vb" }

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
    Write-Host "Version bump: $oldVersion -> $newVersion"
    # Check CHANGELOG.md
    $cl = Get-Content $changelogPath -Raw
    if ($cl -notmatch "## \[$newVersion\]") {
        Write-Host "ERROR: CHANGELOG.md has no entry for version $newVersion !"
        Write-Host "Add it before publishing."
        exit 1
    }
    # Update Constants.vb
    $newContent = $content -replace 'AppVersion As String = "[\d\.]+"', "AppVersion As String = `"$newVersion`""
    Set-Content $constantsPath $newContent -NoNewline
} else {
    Write-Host "Publishing Version: $newVersion (no bump)"
}

# --- Build ---
Write-Host "Building Release..."
dotnet build -c Release --nologo $PSScriptRoot 2>&1 | Out-Host
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# --- Create Staging & Zip Package ---
$stagingDir = Join-Path $PSScriptRoot "bin\Release\staging"
$zipPath = Join-Path $PSScriptRoot "bin\Release\WhatsappH-v$newVersion.zip"

if (Test-Path $stagingDir) { Remove-Item -Path $stagingDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item -Path $zipPath -Force }

New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

# Copy release binaries to staging (excluding settings.json, cache, pdbs)
Get-ChildItem $sourceDir -File | Where-Object {
    $_.Name -notin @("settings.json", "translations_cache.json", "version.txt", ".app_version") -and
    $_.Extension -notin @(".pdb", ".xml")
} | Copy-Item -Destination $stagingDir -Force

foreach ($folder in @("images", "runtimes")) {
    $src = Join-Path $sourceDir $folder
    $dst = Join-Path $stagingDir $folder
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $dst -Recurse -Force
    }
}

# Zip staging directory
Write-Host "Creating ZIP package: $zipPath ..."
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipPath -Force

# --- Create GitHub Release ---
if (-not $SkipGitHub) {
    Write-Host "Publishing GitHub Release v$newVersion..."
    $tagName = "v$newVersion"
    $title = "WhatsappH v$newVersion"
    $betaFlag = if ($Beta) { "--prerelease" } else { "" }
    
    # Check if release tag already exists
    $existingRelease = gh release view $tagName --repo hidaba/WhatsAppH 2>$null
    if ($existingRelease) {
        Write-Host "Updating existing GitHub Release $tagName..."
        gh release upload $tagName "$zipPath#WhatsappH-v$newVersion.zip" --repo hidaba/WhatsAppH --clobber
    } else {
        Write-Host "Creating new GitHub Release $tagName..."
        gh release create $tagName $zipPath --repo hidaba/WhatsAppH --title $title -F $changelogPath $betaFlag
    }
}

Write-Host "Publish complete for WhatsappH v$newVersion !"