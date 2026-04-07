#!/usr/bin/env pwsh
[CmdletBinding(DefaultParameterSetName = 'Build', SupportsShouldProcess, ConfirmImpact = 'High')]
param(
    [Parameter(ParameterSetName = 'Release')]
    [switch]$Release,

    [Parameter(ParameterSetName = 'Release')]
    [switch]$MinorBuild,

    [Parameter(ParameterSetName = 'Release')]
    [switch]$MajorBuild,

    [Parameter(ParameterSetName = 'Release')]
    [switch]$PublishToGallery
)

$ErrorActionPreference = 'Stop'

$repoRoot   = $PSScriptRoot
$manifest   = Join-Path $repoRoot 'CopilotCmdlets.psd1'
$outDir       = Join-Path $repoRoot 'out'
$runtimes     = @('win-x64', 'linux-x64', 'linux-arm64', 'osx-arm64')
$galleryDir   = Join-Path $outDir 'gallery'
$galleryStage = Join-Path $galleryDir 'CopilotCmdlets'

function Get-RuntimeOutDir { param([string]$Rid) Join-Path $outDir $Rid }
function Get-RuntimeStageDir { param([string]$Rid) Join-Path (Get-RuntimeOutDir $Rid) 'CopilotCmdlets' }

function Invoke-Native {
    param([scriptblock]$ScriptBlock, [string]$ErrorMessage)
    & $ScriptBlock
    if ($LASTEXITCODE -ne 0) { throw $ErrorMessage }
}

function Invoke-Build {
    $project = Join-Path $repoRoot 'src/CopilotCmdlets.csproj'
    # RID-specific publishes produce architecture-tagged release zips so users on each
    # platform get a payload matching any native dependencies GitHub.Copilot.SDK pulls
    # in. Even when the current SDK is pure-managed, shipping per-RID artifacts keeps
    # the release contract stable as the SDK evolves.
    foreach ($rid in $runtimes) {
        $ridOut = Get-RuntimeOutDir $rid
        Write-Host "Publishing $rid -> $ridOut"
        Invoke-Native { dotnet publish $project -c Release -r $rid --self-contained false -o $ridOut } "dotnet publish failed for $rid"
    }
    # Separate RID-neutral publish for the PowerShell Gallery payload.
    Write-Host "Publishing RID-neutral gallery build -> $galleryDir"
    Invoke-Native { dotnet publish $project -c Release -o $galleryDir } "dotnet publish failed for gallery build"
}

function Get-LatestReleaseVersion {
    $tagJson = gh release view --json tagName 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $tagJson) {
        Write-Host "No existing GitHub release found, starting from 0.0.0"
        return @{ Major = 0; Minor = 0; Patch = 0 }
    }
    $tag = ($tagJson | ConvertFrom-Json).tagName
    if ($tag -notmatch '^v?(\d+)\.(\d+)\.(\d+)$') {
        throw "Could not parse version from latest release tag '$tag'"
    }
    return @{ Major = [int]$Matches[1]; Minor = [int]$Matches[2]; Patch = [int]$Matches[3] }
}

function Update-ManifestVersion {
    param([switch]$MajorBuild, [switch]$MinorBuild)
    $current = Get-LatestReleaseVersion
    if ($MajorBuild) {
        $newVersion = "$($current.Major + 1).0.0"
    } elseif ($MinorBuild) {
        $newVersion = "$($current.Major).$($current.Minor + 1).0"
    } else {
        $newVersion = "$($current.Major).$($current.Minor).$($current.Patch + 1)"
    }
    $content = Get-Content $manifest -Raw
    $updated = $content -replace "ModuleVersion\s*=\s*'\d+\.\d+\.\d+'", "ModuleVersion     = '$newVersion'"
    Set-Content -Path $manifest -Value $updated -NoNewline
    Write-Host "Bumped ModuleVersion to $newVersion (from $($current.Major).$($current.Minor).$($current.Patch))"
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
    if (Test-Path $galleryStage) { Remove-Item $galleryStage -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $galleryStage | Out-Null
    Get-ChildItem -Path $galleryDir -Force | Where-Object { $_.FullName -ne $galleryStage } |
        Copy-Item -Destination $galleryStage -Recurse -Force
    Copy-Item $manifest -Destination $galleryStage -Force
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
    # Comma prefix prevents PowerShell from unrolling a single-element array to a scalar.
    return ,$zips
}

function Publish-GitHubRelease {
    param([string]$Version, [string[]]$ZipPaths)
    $tag = "v$Version"
    if (-not $PSCmdlet.ShouldProcess("GitHub release $tag", "Commit, tag, push and create release")) { return }
    Invoke-Native { git -C $repoRoot add $manifest } "git add failed"
    $staged = git -C $repoRoot diff --cached --quiet 2>&1; $hasStagedChanges = $LASTEXITCODE -ne 0
    if ($hasStagedChanges) {
        Invoke-Native { git -C $repoRoot commit -m "Release $tag" } "git commit failed"
    } else {
        Write-Host "Manifest already at $tag — skipping commit"
    }
    $existingTags = git -C $repoRoot tag --list $tag
    if ($existingTags) {
        Write-Host "Tag $tag already exists — skipping tag"
    } else {
        Invoke-Native { git -C $repoRoot tag $tag } "git tag failed"
    }
    Invoke-Native { git -C $repoRoot push origin HEAD } "git push HEAD failed"
    Invoke-Native { git -C $repoRoot push origin $tag } "git push tag failed"
    # Splat the per-RID zip paths as positional asset arguments to gh release create.
    $existingRelease = gh release view $tag --json tagName 2>$null
    if ($existingRelease) {
        Write-Host "Release $tag already exists — uploading assets"
        Invoke-Native { gh release upload $tag @ZipPaths --clobber } "gh release upload failed"
    } else {
        Invoke-Native { gh release create $tag @ZipPaths --title $tag --notes "Release $tag" } "gh release create failed"
    }
}

function Publish-ToGallery {
    param([string]$Version)
    if (-not $PSCmdlet.ShouldProcess("PowerShell Gallery", "Publish CopilotCmdlets $Version")) { return }
    $apiKey = Get-DotEnvValue -Key 'POWERSHELL_GALLERY_API_KEY'
    Publish-Module -Path $galleryStage -NuGetApiKey $apiKey
    Write-Host "Published CopilotCmdlets $Version to PowerShell Gallery"
}

if ($Release) {
    $version = Update-ManifestVersion -MajorBuild:$MajorBuild -MinorBuild:$MinorBuild
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
    $currentRid = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
    $matchRid = $runtimes | Where-Object { $currentRid -like "$_*" } | Select-Object -First 1
    if (-not $matchRid) { $matchRid = $runtimes[0] }
    Write-Host "Import with: Import-Module ./out/$matchRid/CopilotCmdlets.psd1"
}
