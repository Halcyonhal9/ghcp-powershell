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
$runtimes   = @('win-x64', 'linux-x64', 'linux-arm64', 'osx-arm64')

function Get-RuntimeOutDir { param([string]$Rid) Join-Path $outDir $Rid }
function Get-RuntimeStageDir { param([string]$Rid) Join-Path (Get-RuntimeOutDir $Rid) 'CopilotCmdlets' }

function Invoke-Native {
    param([scriptblock]$ScriptBlock, [string]$ErrorMessage)
    & $ScriptBlock
    if ($LASTEXITCODE -ne 0) { throw $ErrorMessage }
}

function Invoke-Build {
    $project = Join-Path $repoRoot 'src/CopilotCmdlets.csproj'
    foreach ($rid in $runtimes) {
        $ridOut = Get-RuntimeOutDir $rid
        Write-Host "Publishing $rid -> $ridOut"
        Invoke-Native { dotnet publish $project -c Release -r $rid --self-contained false -o $ridOut } "dotnet publish failed for $rid"
    }
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
    foreach ($rid in $runtimes) {
        $ridOut   = Get-RuntimeOutDir $rid
        $ridStage = Get-RuntimeStageDir $rid
        if (Test-Path $ridStage) { Remove-Item $ridStage -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $ridStage | Out-Null
        Get-ChildItem -Path $ridOut -Force | Where-Object { $_.FullName -ne $ridStage } |
            Copy-Item -Destination $ridStage -Recurse -Force
        Copy-Item $manifest -Destination $ridStage -Force
    }
}

function New-ReleaseZip {
    param([string]$Version)
    $zips = @()
    foreach ($rid in $runtimes) {
        $ridStage = Get-RuntimeStageDir $rid
        $zipPath  = Join-Path $repoRoot "CopilotCmdlets-$Version-$rid.zip"
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Compress-Archive -Path (Join-Path $ridStage '*') -DestinationPath $zipPath -Force
        Write-Host "Created $zipPath"
        $zips += $zipPath
    }
    return $zips
}

function Publish-GitHubRelease {
    param([string]$Version, [string[]]$ZipPaths)
    $tag = "v$Version"
    if (-not $PSCmdlet.ShouldProcess("GitHub release $tag", "Commit, tag, push and create release")) { return }
    Invoke-Native { git -C $repoRoot add $manifest } "git add failed"
    Invoke-Native { git -C $repoRoot commit -m "Release $tag" } "git commit failed"
    Invoke-Native { git -C $repoRoot tag $tag } "git tag failed"
    Invoke-Native { git -C $repoRoot push origin HEAD } "git push HEAD failed"
    Invoke-Native { git -C $repoRoot push origin $tag } "git push tag failed"
    Invoke-Native { gh release create $tag @ZipPaths --title $tag --notes "Release $tag" } "gh release create failed"
}

function Publish-ToGallery {
    param([string]$Version)
    if (-not $PSCmdlet.ShouldProcess("PowerShell Gallery", "Publish CopilotCmdlets $Version")) { return }
    $apiKey = Get-DotEnvValue -Key 'POWERSHELL_GALLERY_API_KEY'
    # PowerShell Gallery hosts a single managed-code package; publish the win-x64 stage as the canonical payload.
    $galleryStage = Get-RuntimeStageDir 'win-x64'
    Publish-Module -Path $galleryStage -NuGetApiKey $apiKey
    Write-Host "Published CopilotCmdlets $Version to PowerShell Gallery"
}

if ($Release) {
    $version = Update-ManifestMinorVersion
    Invoke-Build
    New-ModuleStage
    $zips = New-ReleaseZip -Version $version
    Publish-GitHubRelease -Version $version -ZipPaths $zips
    if ($PublishToGallery) {
        Publish-ToGallery -Version $version
    }
}
else {
    Invoke-Build
    Write-Host "Import with: Import-Module ./out/win-x64/CopilotCmdlets.psd1  (or another runtime under ./out/)"
}
