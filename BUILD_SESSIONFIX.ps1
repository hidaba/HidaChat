# Build and test only. This script does NOT replace the installed app or its data.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Install the .NET 9 SDK on Windows first.'
}
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    throw 'Install Node.js 20 or newer to run the JavaScript regression tests.'
}
node --test Tests/session-health.test.cjs
if ($LASTEXITCODE -ne 0) { throw 'JavaScript tests failed.' }
dotnet run --project Tests/HidaChat.SessionTests.vbproj --configuration Release
if ($LASTEXITCODE -ne 0) { throw '.NET regression tests failed.' }
dotnet build HidaChat.sln --configuration Release
if ($LASTEXITCODE -ne 0) { throw 'Application build failed.' }
dotnet publish HidaChat.vbproj --configuration Release --runtime win-x64 --self-contained true --output artifacts/sessionfix-win-x64
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
Write-Host 'Output: artifacts/sessionfix-win-x64'
Write-Host 'Back up the installed app AND data before deploying. Do not overwrite or delete data.'
