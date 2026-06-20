param(
    [string]$Version,

    [string]$WorkshopSourceDir = "workshop",

    [string]$WorkspaceDir = "artifacts/workshop",

    [string]$PackageDir,

    [string]$NotesPath = "CHANGELOG.md"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$manifestPath = Join-Path $root "RandomForeseer.json"

function Resolve-RepoPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $root $Path
}

$sourceDir = Resolve-RepoPath $WorkshopSourceDir
$workspacePath = Resolve-RepoPath $WorkspaceDir
$contentDir = Join-Path $workspacePath "content"
$notesFile = Resolve-RepoPath $NotesPath
$artifactsDir = Join-Path $root "artifacts"
$pathTrimChars = [char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$artifactsFullPath = [System.IO.Path]::GetFullPath($artifactsDir).TrimEnd($pathTrimChars)
$workspaceFullPath = [System.IO.Path]::GetFullPath($workspacePath).TrimEnd($pathTrimChars)
$artifactsPrefix = "$artifactsFullPath$([System.IO.Path]::DirectorySeparatorChar)"

if ($workspaceFullPath -eq $artifactsFullPath -or !$workspaceFullPath.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Workshop workspace must be an artifacts subdirectory: $workspacePath"
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

    $body = $body -replace '(?i)<br\s*/?>\r?\n([ \t]+)', "`n`$1"
    return $body
}

function Copy-RequiredFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$DestinationPath
    )

    if (!(Test-Path $SourcePath)) {
        throw "Required file not found: $SourcePath"
    }

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
}

function Set-JsonProperty {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [object]$Value
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        $Object | Add-Member -NotePropertyName $Name -NotePropertyValue $Value
        return
    }

    $property.Value = $Value
}

function Get-OrCreateJsonPropertyObject {
    param(
        [Parameter(Mandatory = $true)]
        [psobject]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        $value = [pscustomobject]@{}
        Set-JsonProperty $Object $Name $value
        return $value
    }

    return $property.Value
}

function Get-TextFileWithoutTrailingNewline {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (!(Test-Path $Path)) {
        throw "Required text file not found: $Path"
    }

    return (Get-Content $Path -Raw).TrimEnd("`r", "`n")
}

if (!(Test-Path $manifestPath)) {
    throw "Manifest not found: $manifestPath"
}

if (!(Test-Path $sourceDir)) {
    throw "Workshop source directory not found: $sourceDir"
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$modId = [string]$manifest.id
$manifestVersion = [string]$manifest.version

if ([string]::IsNullOrWhiteSpace($modId)) {
    throw "RandomForeseer.json must contain a non-empty id."
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $manifestVersion
}

if ($manifestVersion -ne $Version) {
    throw "Manifest version is $manifestVersion, but requested Workshop version is $Version."
}

if ([string]::IsNullOrWhiteSpace($PackageDir)) {
    $PackageDir = Join-Path $root "artifacts/package/$modId"
}
elseif (![System.IO.Path]::IsPathRooted($PackageDir)) {
    $PackageDir = Join-Path $root $PackageDir
}

if (!(Test-Path $PackageDir)) {
    throw "Release package directory not found: $PackageDir. Run scripts/release.ps1 first, or pass -PackageDir."
}

$packageManifestPath = Join-Path $PackageDir "$modId.json"
if (!(Test-Path $packageManifestPath)) {
    throw "Package manifest not found: $packageManifestPath"
}

$packageManifest = Get-Content $packageManifestPath -Raw | ConvertFrom-Json
$packageVersion = [string]$packageManifest.version
if ($packageVersion -ne $Version) {
    throw "Package manifest version is $packageVersion, but requested Workshop version is $Version."
}

$workshopJsonSource = Join-Path $sourceDir "workshop.json"
$imageSource = Join-Path $sourceDir "image.png"

if (!(Test-Path $workshopJsonSource)) {
    throw "Workshop metadata not found: $workshopJsonSource"
}

if (!(Test-Path $imageSource)) {
    throw "Workshop image not found: $imageSource"
}

$imageSizeLimit = 1MB
$imageInfo = Get-Item -LiteralPath $imageSource
if ($imageInfo.Length -ge $imageSizeLimit) {
    throw "Workshop image must be smaller than 1 MB for Steam upload: $imageSource ($($imageInfo.Length) bytes)."
}

if (Test-Path $workspacePath) {
    Remove-Item -LiteralPath $workspacePath -Recurse -Force
}

New-Item -ItemType Directory -Path $contentDir -Force | Out-Null

Copy-RequiredFile -SourcePath $packageManifestPath -DestinationPath (Join-Path $contentDir "$modId.json")

if ($packageManifest.has_dll) {
    Copy-RequiredFile -SourcePath (Join-Path $PackageDir "$modId.dll") -DestinationPath (Join-Path $contentDir "$modId.dll")
}

if ($packageManifest.has_pck) {
    Copy-RequiredFile -SourcePath (Join-Path $PackageDir "$modId.pck") -DestinationPath (Join-Path $contentDir "$modId.pck")
}

$workshop = Get-Content $workshopJsonSource -Raw | ConvertFrom-Json
$workshop.changeNote = Get-ChangelogSection $notesFile "v$Version"
$localized = Get-OrCreateJsonPropertyObject $workshop "localized"

$localizedDescriptions = @{
    english = Join-Path $sourceDir "description.en.txt"
    schinese = Join-Path $sourceDir "description.txt"
}

foreach ($entry in $localizedDescriptions.GetEnumerator()) {
    $languageConfig = Get-OrCreateJsonPropertyObject $localized $entry.Key
    Set-JsonProperty $languageConfig "description" (Get-TextFileWithoutTrailingNewline $entry.Value)
}

$workshop | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $workspacePath "workshop.json") -Encoding utf8

Copy-RequiredFile -SourcePath $imageSource -DestinationPath (Join-Path $workspacePath "image.png")

Write-Host "Workshop workspace prepared: $workspacePath"
Write-Host "Workshop content prepared from: $PackageDir"
