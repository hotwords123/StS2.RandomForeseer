param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z][0-9A-Za-z.-]*)?$')]
    [string]$Version,

    [string]$Configuration = "Release",

    [switch]$Draft,
    [switch]$Prerelease,
    [switch]$SkipBuild,
    [switch]$SkipTagPush,
    [switch]$SkipReleaseCreate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $root "RandomForeseer.csproj"
$manifestPath = Join-Path $root "RandomForeseer.json"
$artifactsDir = Join-Path $root "artifacts"
$packageRoot = Join-Path $artifactsDir "package"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [string[]]$Arguments = @()
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $FilePath $($Arguments -join ' ')"
    }
}

function Test-CommandAvailable {
    param([Parameter(Mandatory = $true)][string]$Name)
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

if (!(Test-CommandAvailable "git")) {
    throw "git is required."
}

if (!(Test-CommandAvailable "dotnet")) {
    throw "dotnet is required."
}

if (!$SkipReleaseCreate -and !(Test-CommandAvailable "gh")) {
    throw "GitHub CLI is required for release creation. Install gh or pass -SkipReleaseCreate."
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$modId = [string]$manifest.id
$manifestVersion = [string]$manifest.version
$tag = "v$Version"

if ([string]::IsNullOrWhiteSpace($modId)) {
    throw "RandomForeseer.json must contain a non-empty id."
}

if ($manifestVersion -ne $Version) {
    throw "Manifest version is $manifestVersion, but release version is $Version. Update RandomForeseer.json and commit it before releasing."
}

Push-Location $root
try {
    $status = Invoke-Checked git @("status", "--porcelain")
    if ($status) {
        throw "Working tree is not clean. Commit or stash changes before releasing."
    }

    $headCommit = Invoke-Checked git @("rev-parse", "HEAD")
    $existingLocalTag = Invoke-Checked git @("tag", "--list", $tag)
    $localTagExists = [bool]$existingLocalTag
    if ($localTagExists) {
        $localTagCommit = Invoke-Checked git @("rev-list", "-n", "1", $tag)
        if ($localTagCommit -ne $headCommit) {
            throw "Local tag $tag already exists but does not point at HEAD."
        }
    }

    $remoteTag = Invoke-Checked git @("ls-remote", "--tags", "origin", "refs/tags/$tag")
    $remoteTagExists = [bool]$remoteTag

    if (Test-Path $artifactsDir) {
        Remove-Item -LiteralPath $artifactsDir -Recurse -Force
    }

    $packageDir = Join-Path $packageRoot $modId
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

    if (!$SkipBuild) {
        $modOutputDir = "$packageDir$([System.IO.Path]::DirectorySeparatorChar)"
        Invoke-Checked dotnet @(
            "build",
            $projectPath,
            "-c",
            $Configuration,
            "/p:ModOutputDir=$modOutputDir",
            "/p:CopyModOnBuild=true",
            "/p:RunPckExport=true"
        )
    }

    $requiredFiles = @(
        Join-Path $packageDir "$modId.json"
    )

    if ($manifest.has_dll) {
        $requiredFiles += Join-Path $packageDir "$modId.dll"
    }

    if ($manifest.has_pck) {
        $requiredFiles += Join-Path $packageDir "$modId.pck"
    }

    foreach ($file in $requiredFiles) {
        if (!(Test-Path $file)) {
            throw "Expected package file was not produced: $file"
        }
    }

    $postBuildStatus = Invoke-Checked git @("status", "--porcelain")
    if ($postBuildStatus) {
        throw "Build changed tracked files. Review and commit those changes, then rerun the release script."
    }

    $zipPath = Join-Path $artifactsDir "$modId-$tag.zip"
    Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -Force

    $hash = Get-FileHash -Algorithm SHA256 $zipPath
    $hashPath = "$zipPath.sha256"
    "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), (Split-Path $zipPath -Leaf) | Set-Content -Path $hashPath -Encoding ascii

    if (!$localTagExists) {
        Invoke-Checked git @("tag", "-a", $tag, "-m", "$modId $tag")
    }

    if (!$SkipTagPush -and !$remoteTagExists) {
        Invoke-Checked git @("push", "origin", $tag)
    }

    if (!$SkipReleaseCreate) {
        $releaseArgs = @(
            "release",
            "create",
            $tag,
            $zipPath,
            $hashPath,
            "--title",
            $tag,
            "--generate-notes"
        )

        if ($Draft) {
            $releaseArgs += "--draft"
        }

        if ($Prerelease -or $Version.Contains("-")) {
            $releaseArgs += "--prerelease"
        }

        Invoke-Checked gh $releaseArgs
    }

    Write-Host "Release package created: $zipPath"
    Write-Host "SHA256 file created: $hashPath"
}
finally {
    Pop-Location
}
