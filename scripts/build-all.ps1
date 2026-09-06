# Publishes self-contained, single-file SemaNami CLI binaries for every supported desktop
# platform, plus a matching install.sh copy alongside each Unix build for distribution.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$rids = @("win-x64", "osx-x64", "osx-arm64", "linux-x64")

foreach ($rid in $rids) {
    Write-Host "=== Publishing $rid ===" -ForegroundColor Cyan
    dotnet publish src/SemaNami.Cli -c Release -r $rid --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o "dist/$rid"
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $rid" }

    if ($rid -ne "win-x64") {
        Copy-Item "scripts/install.sh" "dist/$rid/install.sh" -Force
    }
}

Write-Host ""
Write-Host "Done. Binaries are in dist/<rid>/." -ForegroundColor Green
