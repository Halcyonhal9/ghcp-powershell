#!/usr/bin/env pwsh
# ============================================================================
# LOCAL DEV BUILD SCRIPT
#
# This script is for ad-hoc local builds only. Official releases and
# publishing to GitHub Releases / PowerShell Gallery are handled by the
# GitHub Actions workflow in .github/workflows/release.yml.
# ============================================================================
param()

$ErrorActionPreference = 'Stop'

Write-Warning "This script is for local development builds only. Releases are handled by the GitHub Actions workflow (.github/workflows/release.yml)."

$repoRoot   = $PSScriptRoot
$outDir     = Join-Path $repoRoot 'out'
$runtimes   = @('win-x64', 'osx-arm64')

$project = Join-Path $repoRoot 'src/CopilotCmdlets.csproj'

foreach ($rid in $runtimes) {
    $ridOut = Join-Path $outDir $rid
    Write-Host "Publishing $rid -> $ridOut"
    dotnet publish $project -c Release -r $rid --self-contained false -o $ridOut
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid" }
}

$currentRid = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
$matchRid = $runtimes | Where-Object { $currentRid -like "$_*" } | Select-Object -First 1
if (-not $matchRid) { $matchRid = $runtimes[0] }
Write-Host "Import with: Import-Module ./out/$matchRid/CopilotCmdlets.psd1"
