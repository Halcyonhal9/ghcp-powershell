#!/usr/bin/env pwsh
[CmdletBinding(DefaultParameterSetName = 'Build', SupportsShouldProcess, ConfirmImpact = 'High')]
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
$stageDir   = Join-Path $outDir 'CopilotCmdlets'

function Invoke-Native {
    param([scriptblock]$ScriptBlock, [string]$ErrorMessage)
    & $ScriptBlock
    if ($LASTEXITCODE -ne 0) { throw $ErrorMessage }
}

function Invoke-Build {
    Invoke-Native { dotnet publish (Join-Path $repoRoot 'src/CopilotCmdlets.csproj') -c Release -o $outDir } "dotnet publish failed"
}

function Update-ManifestMinorVersion {
    $content = Get-Content $manifest -Raw
    if ($content -notmatch "ModuleVersion\s*=\s*'(\d+)\.(\d+)\.(\d+)'") {
        throw "Could not parse ModuleVersion in $manifest"
    }
    $major = [int]$Matches[1]
    $minor = [int]$Matches[2] + 1
    $patch = 0
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

function New-ModuleStage {
    if (Test-Path $stageDir) { Remove-Item $stageDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $stageDir | Out-Null
    Get-ChildItem -Path $outDir -Force | Where-Object { $_.FullName -ne $stageDir } |
        Copy-Item -Destination $stageDir -Recurse -Force
    Copy-Item $manifest -Destination $stageDir -Force
}

function New-ReleaseZip {
    param([string]$Version)
    $zipPath = Join-Path $repoRoot "CopilotCmdlets-$Version.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path (Join-Path $stageDir '*') -DestinationPath $zipPath -Force
    Write-Host "Created $zipPath"
    return $zipPath
}

function Publish-GitHubRelease {
    param([string]$Version, [string]$ZipPath)
    $tag = "v$Version"
    if (-not $PSCmdlet.ShouldProcess("GitHub release $tag", "Commit, tag, push and create release")) { return }
    Invoke-Native { git -C $repoRoot add $manifest } "git add failed"
    Invoke-Native { git -C $repoRoot commit -m "Release $tag" } "git commit failed"
    Invoke-Native { git -C $repoRoot tag $tag } "git tag failed"
    Invoke-Native { git -C $repoRoot push origin HEAD } "git push HEAD failed"
    Invoke-Native { git -C $repoRoot push origin $tag } "git push tag failed"
    Invoke-Native { gh release create $tag $ZipPath --title $tag --notes "Release $tag" } "gh release create failed"
}

function Publish-ToGallery {
    param([string]$Version)
    if (-not $PSCmdlet.ShouldProcess("PowerShell Gallery", "Publish CopilotCmdlets $Version")) { return }
    $apiKey = Get-DotEnvValue -Key 'POWERSHELL_GALLERY_API_KEY'
    Publish-Module -Path $stageDir -NuGetApiKey $apiKey
    Write-Host "Published CopilotCmdlets $Version to PowerShell Gallery"
}

if ($Release) {
    $version = Update-ManifestMinorVersion
    Invoke-Build
    New-ModuleStage
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
