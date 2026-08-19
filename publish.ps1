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
    if ($cl -notmatch "## \[$newVersion(-beta)?\]") {
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
$zipPath = Join-Path $PSScriptRoot "bin\Release\HidaChat-v$newVersion.zip"

if (Test-Path $stagingDir) { Remove-Item -Path $stagingDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item -Path $zipPath -Force }

New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

# Copy release binaries to staging (excluding settings.json, cache, pdbs)
Get-ChildItem $sourceDir -File | Where-Object {
    $_.Name -notin @("settings.json", "translations_cache.json", "version.txt", ".app_version") -and
    $_.Extension -notin @(".pdb", ".xml") -and
    $_.Name -notlike "WhatsAppVB*" -and
    $_.Name -notlike "WhatsappH*"
} | Copy-Item -Destination $stagingDir -Force

foreach ($folder in @("images", "runtimes")) {
    $src = Join-Path $sourceDir $folder
    $dst = Join-Path $stagingDir $folder
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination $dst -Recurse -Force
    }
}

# Compile legacy forwarder WhatsappH.exe to provide seamless transition from 0.3.3 and older versions
$forwarderSource = @"
using System;
using System.Diagnostics;
using System.IO;

namespace WhatsappHForwarder
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string target = Path.Combine(baseDir, "HidaChat.exe");
                if (File.Exists(target))
                {
                    ProcessStartInfo psi = new ProcessStartInfo(target);
                    psi.WorkingDirectory = baseDir;
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
            }
            catch { }
        }
    }
}
"@
$forwarderCsPath = Join-Path $stagingDir "WhatsappH_forwarder.cs"
Set-Content $forwarderCsPath $forwarderSource -Encoding UTF8
$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$iconPath = Join-Path $PSScriptRoot "images\icon.ico"
$forwarderExePath = Join-Path $stagingDir "WhatsappH.exe"
if (Test-Path $cscPath) {
    Write-Host "Compiling legacy WhatsappH.exe forwarder..."
    & $cscPath /target:winexe "/out:$forwarderExePath" "/win32icon:$iconPath" /optimize /nologo $forwarderCsPath
}
if (Test-Path $forwarderCsPath) { Remove-Item $forwarderCsPath -Force }

# Zip staging directory
Write-Host "Creating ZIP package: $zipPath ..."
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipPath -Force

# --- Git Commit & Push ---
if (-not $SkipGitHub) {
    Write-Host "Committing and pushing source code changes to GitHub..."
    git add -A
    $gitStatus = git status --porcelain
    if ($gitStatus) {
        $commitMsg = "v$newVersion - Release $(if ($Beta) { 'Beta' } else { 'Stabile' })"
        git commit -m $commitMsg
        git push origin master
    }
}

# --- Create GitHub Release ---
if (-not $SkipGitHub) {
    Write-Host "Publishing GitHub Release v$newVersion..."
    $tagName = "v$newVersion"
    $title = "HidaChat v$newVersion"
    $betaFlag = if ($Beta) { "--prerelease" } else { "" }
    
    # Check if release tag already exists
    $existingRelease = gh release view $tagName --repo hidaba/HidaChat 2>$null
    if ($existingRelease) {
        Write-Host "Updating existing GitHub Release $tagName..."
        gh release upload $tagName "$zipPath#HidaChat-v$newVersion.zip" --repo hidaba/HidaChat --clobber
    } else {
        Write-Host "Creating new GitHub Release $tagName..."
        if ($Beta) {
            gh release create $tagName $zipPath --repo hidaba/HidaChat --title $title -F $changelogPath --prerelease
        } else {
            gh release create $tagName $zipPath --repo hidaba/HidaChat --title $title -F $changelogPath
        }
    }
}
Write-Host "Publish complete for HidaChat v$newVersion !"