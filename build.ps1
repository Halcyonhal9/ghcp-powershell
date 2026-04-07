#!/usr/bin/env pwsh
[CmdletBinding(DefaultParameterSetName = 'Build')]
param(
    [Parameter(ParameterSetName = 'Release')]
    [switch]$Release,

    [Parameter(ParameterSetName = 'Release')]
    [switch]$PublishToGallery
)

$ErrorActionPreference = 'Stop'

$repoRoot   = $PSScriptRoot
$manifest   = Join-Path $repoRoot 'CopilotCmdlets.psd1'
$outDir     = Join-Path $repoRoot 'out'

function Invoke-Build {
    dotnet publish (Join-Path $repoRoot 'src/CopilotCmdlets.csproj') -c Release -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
}

function Step-MinorVersion {
    $content = Get-Content $manifest -Raw
    if ($content -notmatch "ModuleVersion\s*=\s*'(\d+)\.(\d+)\.(\d+)'") {
        throw "Could not parse ModuleVersion in $manifest"
    }
    $major = [int]$Matches[1]
    $minor = [int]$Matches[2] + 1
    $patch = [int]$Matches[3]
    $newVersion = "$major.$minor.$patch"
    $updated = $content -replace "ModuleVersion\s*=\s*'\d+\.\d+\.\d+'", "ModuleVersion     = '$newVersion'"
    Set-Content -Path $manifest -Value $updated -NoNewline
    Write-Host "Bumped ModuleVersion to $newVersion"
    return $newVersion
}

function Get-DotEnvValue {
    param([string]$Key)
    $envFile = Join-Path $repoRoot '.env'
    if (-not (Test-Path $envFile)) { throw ".env file not found at $envFile" }
    foreach ($line in Get-Content $envFile) {
        if ($line -match '^\s*#') { continue }
        if ($line -match "^\s*$([regex]::Escape($Key))\s*=\s*(.+?)\s*$") {
            return $Matches[1].Trim('"').Trim("'")
        }
    }
    throw "Key '$Key' not found in .env"
}

function New-ReleaseZip {
    param([string]$Version)
    $zipPath = Join-Path $repoRoot "CopilotCmdlets-$Version.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Copy-Item $manifest -Destination $outDir -Force
    Compress-Archive -Path (Join-Path $outDir '*') -DestinationPath $zipPath -Force
    Write-Host "Created $zipPath"
    return $zipPath
}

function Publish-GitHubRelease {
    param([string]$Version, [string]$ZipPath)
    $tag = "v$Version"
    git -C $repoRoot add $manifest
    git -C $repoRoot commit -m "Release $tag" | Out-Null
    git -C $repoRoot tag $tag
    git -C $repoRoot push origin HEAD
    git -C $repoRoot push origin $tag
    gh release create $tag $ZipPath --title $tag --notes "Release $tag"
}

function Publish-ToGallery {
    param([string]$Version)
    $apiKey = Get-DotEnvValue -Key 'POWERSHELL_GALLERY_API_KEY'
    Copy-Item $manifest -Destination $outDir -Force
    Publish-Module -Path $outDir -NuGetApiKey $apiKey
    Write-Host "Published CopilotCmdlets $Version to PowerShell Gallery"
}

if ($Release) {
    $version = Step-MinorVersion
    Invoke-Build
    $zip = New-ReleaseZip -Version $version
    Publish-GitHubRelease -Version $version -ZipPath $zip
    if ($PublishToGallery) {
        Publish-ToGallery -Version $version
    }
}
else {
    Invoke-Build
    Write-Host "Import with: Import-Module ./out/CopilotCmdlets.psd1"
}
