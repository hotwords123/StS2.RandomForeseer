param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z][0-9A-Za-z.-]*)?$')]
    [string]$Version,

    [string]$Configuration = "Release",

    [string]$NotesPath = "CHANGELOG.md",

    [switch]$Draft,
    [switch]$Prerelease,
    [switch]$PackageOnly,
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
$packageRoot = Join-Path $artifactsDir "packages"
$releaseStagingRoot = Join-Path $artifactsDir "release-staging"
$releaseNotesPath = Join-Path $root $NotesPath

if ($PackageOnly) {
    $SkipTagPush = $true
    $SkipReleaseCreate = $true
}

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

function Get-ChangelogSection {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Tag
    )

    $content = Get-Content $Path -Raw
    $escapedTag = [regex]::Escape($Tag)
    $match = [regex]::Match($content, "(?ms)^##\s+$escapedTag\s*\r?\n(?<body>.*?)(?=^##\s+|\z)")
    if (!$match.Success) {
        throw "Release notes for $Tag were not found in $Path. Add a '## $Tag' section."
    }

    $body = $match.Groups["body"].Value.Trim()
    if ([string]::IsNullOrWhiteSpace($body)) {
        throw "Release notes for $Tag in $Path are empty."
    }

    # GitHub release notes render ordinary newlines as line breaks. If the
    # changelog uses explicit <br> before the translated line, keeping both
    # produces <br><br> on the release page.
    $body = $body -replace '(?i)<br\s*/?>\r?\n([ \t]+)', "`n`$1"

    return $body
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

if (!$SkipReleaseCreate -and !(Test-Path $releaseNotesPath)) {
    throw "Release notes file not found: $releaseNotesPath"
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
    $localTagExists = $false
    $remoteTagExists = $false
    if (!$PackageOnly) {
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
    }

    $packageVersionDir = Join-Path $packageRoot $Version
    $packageDir = Join-Path $packageVersionDir $modId

    if (!$SkipBuild) {
        if (Test-Path $packageDir) {
            throw "Versioned package already exists: $packageDir. Published SemVer packages are immutable; pass -SkipBuild to reuse it, or remove an unpublished package explicitly before rebuilding."
        }

        $stagingVersionDir = Join-Path $releaseStagingRoot $Version
        $stagingPackageDir = Join-Path $stagingVersionDir $modId
        if (Test-Path $stagingVersionDir) {
            Remove-Item -LiteralPath $stagingVersionDir -Recurse -Force
        }
        New-Item -ItemType Directory -Path $stagingPackageDir -Force | Out-Null

        $modOutputDir = "$stagingPackageDir$([System.IO.Path]::DirectorySeparatorChar)"
        Invoke-Checked dotnet @(
            "build",
            $projectPath,
            "-c",
            $Configuration,
            "/p:ModOutputDir=$modOutputDir",
            "/p:CopyModOnBuild=true",
            "/p:RunPckExport=true"
        )

        $sourceRef = Invoke-Checked git @("branch", "--show-current")
        if ([string]::IsNullOrWhiteSpace($sourceRef)) {
            $sourceRef = "detached"
        }
        $buildInfo = @(
            "mod-version: $Version"
            "min-game-version: $([string]$manifest.min_game_version)"
            "git-commit: $headCommit"
            "git-ref: $sourceRef"
            "built-at: $([DateTimeOffset]::Now.ToString('o'))"
        ) -join "`n"
        Set-Content -Path (Join-Path $stagingPackageDir "build-info.txt") -Value $buildInfo -Encoding utf8 -NoNewline

        $stagedRequiredFiles = @(
            Join-Path $stagingPackageDir "$modId.json"
            Join-Path $stagingPackageDir "build-info.txt"
        )
        if ($manifest.has_dll) {
            $stagedRequiredFiles += Join-Path $stagingPackageDir "$modId.dll"
        }
        if ($manifest.has_pck) {
            $stagedRequiredFiles += Join-Path $stagingPackageDir "$modId.pck"
        }
        foreach ($file in $stagedRequiredFiles) {
            if (!(Test-Path $file)) {
                throw "Expected staged package file was not produced: $file"
            }
        }

        New-Item -ItemType Directory -Path $packageVersionDir -Force | Out-Null
        Move-Item -LiteralPath $stagingPackageDir -Destination $packageDir
        if (Test-Path $stagingVersionDir) {
            Remove-Item -LiteralPath $stagingVersionDir -Recurse -Force
        }
    }
    elseif (!(Test-Path $packageDir)) {
        throw "Versioned package not found for -SkipBuild: $packageDir"
    }

    $requiredFiles = @(
        Join-Path $packageDir "$modId.json"
        Join-Path $packageDir "build-info.txt"
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

    $packagedManifestPath = Join-Path $packageDir "$modId.json"
    $packagedManifest = Get-Content $packagedManifestPath -Raw | ConvertFrom-Json
    if ([string]$packagedManifest.id -ne $modId -or [string]$packagedManifest.version -ne $Version) {
        throw "Cached package manifest identity/version does not match $modId ${Version}: $packagedManifestPath"
    }
    if ([string]$packagedManifest.min_game_version -ne [string]$manifest.min_game_version) {
        throw "Cached package min_game_version does not match the source manifest: $packagedManifestPath"
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

    if (!$PackageOnly -and !$localTagExists) {
        Invoke-Checked git @("tag", "-a", $tag, "-m", "$modId $tag")
    }

    if (!$SkipTagPush -and !$remoteTagExists) {
        Invoke-Checked git @("push", "origin", $tag)
    }

    if (!$SkipReleaseCreate) {
        $releaseNotesContent = Get-ChangelogSection $releaseNotesPath $tag
        $generatedReleaseNotesPath = Join-Path $artifactsDir "$modId-$tag-notes.md"
        Set-Content -Path $generatedReleaseNotesPath -Value $releaseNotesContent -Encoding utf8

        $releaseArgs = @(
            "release",
            "create",
            $tag,
            $zipPath,
            $hashPath,
            "--title",
            $tag,
            "--notes-file",
            $generatedReleaseNotesPath
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
    Write-Host "Versioned package retained at: $packageDir"
    Write-Host "SHA256 file created: $hashPath"
}
finally {
    Pop-Location
}
