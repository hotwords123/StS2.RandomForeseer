param(
    [string]$Version,

    [string]$WorkshopSourceDir = "workshop",

    [string]$WorkspaceDir = "artifacts/workshop",

    [string]$PackagesRoot = "artifacts/packages",

    [string]$ActiveGameVersionsPath = "workshop/active-game-versions.txt",

    [string]$LoaderProjectPath = "workshop/loader/RandomForeseer.WorkshopLoader.csproj",

    [string]$NotesPath = "CHANGELOG.md"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$sourceManifestPath = Join-Path $root "RandomForeseer.json"

function Resolve-RepoPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $root $Path
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$Arguments = @()
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed: $FilePath $($Arguments -join ' ')"
    }
}

function ConvertTo-SemanticVersion {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Description
    )

    try {
        return [System.Management.Automation.SemanticVersion]::Parse($Value)
    }
    catch {
        throw "Invalid semantic version for ${Description}: $Value"
    }
}

function Get-ChangelogSection {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Tag
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
    return "## $Tag`n`n$body"
}

function Copy-RequiredFile {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath
    )

    if (!(Test-Path $SourcePath)) {
        throw "Required file not found: $SourcePath"
    }

    Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
}

function Set-JsonProperty {
    param(
        [Parameter(Mandatory = $true)][psobject]$Object,
        [Parameter(Mandatory = $true)][string]$Name,
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
        [Parameter(Mandatory = $true)][psobject]$Object,
        [Parameter(Mandatory = $true)][string]$Name
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

function Get-ActiveGameVersions {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (!(Test-Path $Path)) {
        throw "Active game version list not found: $Path"
    }

    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $versions = @()
    foreach ($line in Get-Content $Path) {
        $value = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($value) -or $value.StartsWith("#")) {
            continue
        }
        $semanticVersion = ConvertTo-SemanticVersion $value "active game version"
        if (!$seen.Add($value)) {
            throw "Duplicate active game version: $value"
        }
        $versions += [pscustomobject]@{
            Value = $value
            SemanticVersion = $semanticVersion
        }
    }

    if ($versions.Count -eq 0) {
        throw "Active game version list is empty: $Path"
    }

    return @($versions)
}

function Get-PackageDependencies {
    param(
        [Parameter(Mandatory = $true)][psobject]$Manifest,
        [Parameter(Mandatory = $true)][string]$ManifestPath
    )

    $dependenciesProperty = $Manifest.PSObject.Properties["dependencies"]
    if ($null -eq $dependenciesProperty -or $null -eq $dependenciesProperty.Value) {
        return @()
    }

    $dependencies = @()
    foreach ($dependency in @($dependenciesProperty.Value)) {
        $id = [string]$dependency.id
        if ([string]::IsNullOrWhiteSpace($id)) {
            throw "Package dependency has no ID: $ManifestPath"
        }
        if ($null -ne $dependency.PSObject.Properties["version"]) {
            throw "Legacy dependency field 'version' is not supported; migrate it to 'min_version': $ManifestPath"
        }

        $minVersionProperty = $dependency.PSObject.Properties["min_version"]
        $minVersion = if ($null -eq $minVersionProperty) { "" } else { [string]$minVersionProperty.Value }
        $minSemanticVersion = $null
        if (![string]::IsNullOrWhiteSpace($minVersion)) {
            $minSemanticVersion = ConvertTo-SemanticVersion $minVersion "minimum version for dependency $id"
        }

        $dependencies += [pscustomobject]@{
            Id = $id
            MinVersion = $minVersion
            MinSemanticVersion = $minSemanticVersion
        }
    }

    return @($dependencies)
}

function Get-VersionedPackages {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ModId,
        [Parameter(Mandatory = $true)]$MaximumVersion
    )

    if (!(Test-Path $Path)) {
        throw "Versioned package root not found: $Path. Run scripts/release.ps1 for each required Mod version first."
    }

    $packages = @()
    foreach ($versionDirectory in Get-ChildItem -LiteralPath $Path -Directory) {
        $directoryVersion = ConvertTo-SemanticVersion $versionDirectory.Name "package directory"
        if ($directoryVersion.CompareTo($MaximumVersion) -gt 0) {
            continue
        }

        $packageDirectory = Join-Path $versionDirectory.FullName $ModId
        $manifestPath = Join-Path $packageDirectory "$ModId.json"
        if (!(Test-Path $manifestPath)) {
            throw "Package manifest not found: $manifestPath"
        }

        $manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
        if ([string]$manifest.id -ne $ModId) {
            throw "Package manifest ID is '$([string]$manifest.id)', expected '$ModId': $manifestPath"
        }

        $modVersion = [string]$manifest.version
        if ($modVersion -ne $versionDirectory.Name) {
            throw "Package directory version is $($versionDirectory.Name), but manifest version is $modVersion."
        }
        $modSemanticVersion = ConvertTo-SemanticVersion $modVersion "package Mod version"

        $minGameVersion = [string]$manifest.min_game_version
        if ([string]::IsNullOrWhiteSpace($minGameVersion)) {
            throw "Package manifest has no min_game_version: $manifestPath"
        }
        $minGameSemanticVersion = ConvertTo-SemanticVersion $minGameVersion "package minimum game version"
        $dependencies = @(Get-PackageDependencies $manifest $manifestPath)

        if (!$manifest.has_dll) {
            throw "Workshop variants require a DLL package: $manifestPath"
        }
        $dllPath = Join-Path $packageDirectory "$ModId.dll"
        if (!(Test-Path $dllPath)) {
            throw "Package DLL not found: $dllPath"
        }

        if (!$manifest.has_pck) {
            throw "Workshop variants require a PCK package: $manifestPath"
        }
        $pckPath = Join-Path $packageDirectory "$ModId.pck"
        if (!(Test-Path $pckPath)) {
            throw "Package PCK not found: $pckPath"
        }

        $packages += [pscustomobject]@{
            ModVersion = $modVersion
            ModSemanticVersion = $modSemanticVersion
            MinGameVersion = $minGameVersion
            MinGameSemanticVersion = $minGameSemanticVersion
            Dependencies = $dependencies
            Directory = $packageDirectory
            Manifest = $manifest
            DllPath = $dllPath
            PckPath = $pckPath
            BuildInfoPath = Join-Path $packageDirectory "build-info.txt"
        }
    }

    return @($packages)
}

$sourceDir = Resolve-RepoPath $WorkshopSourceDir
$workspacePath = Resolve-RepoPath $WorkspaceDir
$packagesRootPath = Resolve-RepoPath $PackagesRoot
$activeGameVersionsFile = Resolve-RepoPath $ActiveGameVersionsPath
$loaderProject = Resolve-RepoPath $LoaderProjectPath
$loaderNoticesPath = Join-Path (Split-Path $loaderProject -Parent) "THIRD_PARTY_NOTICES.md"
$notesFile = Resolve-RepoPath $NotesPath
$contentDir = Join-Path $workspacePath "content"
$artifactsDir = Join-Path $root "artifacts"
$loaderBuildDir = Join-Path $artifactsDir "workshop-loader-build"

$pathTrimChars = [char[]]@([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$artifactsFullPath = [System.IO.Path]::GetFullPath($artifactsDir).TrimEnd($pathTrimChars)
$workspaceFullPath = [System.IO.Path]::GetFullPath($workspacePath).TrimEnd($pathTrimChars)
$artifactsPrefix = "$artifactsFullPath$([System.IO.Path]::DirectorySeparatorChar)"
if ($workspaceFullPath -eq $artifactsFullPath -or
    !$workspaceFullPath.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Workshop workspace must be an artifacts subdirectory: $workspacePath"
}

if (!(Test-Path $sourceManifestPath)) {
    throw "Manifest not found: $sourceManifestPath"
}
if (!(Test-Path $sourceDir)) {
    throw "Workshop source directory not found: $sourceDir"
}
if (!(Test-Path $loaderProject)) {
    throw "Workshop loader project not found: $loaderProject"
}
if (!(Test-Path $loaderNoticesPath)) {
    throw "Workshop loader third-party notices not found: $loaderNoticesPath"
}

$sourceManifest = Get-Content $sourceManifestPath -Raw | ConvertFrom-Json
$modId = [string]$sourceManifest.id
$manifestVersion = [string]$sourceManifest.version
if ([string]::IsNullOrWhiteSpace($modId)) {
    throw "RandomForeseer.json must contain a non-empty id."
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $manifestVersion
}
if ($manifestVersion -ne $Version) {
    throw "Manifest version is $manifestVersion, but requested Workshop version is $Version."
}
$workshopSemanticVersion = ConvertTo-SemanticVersion $Version "Workshop version"

$activeGameVersions = @(Get-ActiveGameVersions $activeGameVersionsFile)
$packages = @(Get-VersionedPackages $packagesRootPath $modId $workshopSemanticVersion)
if ($packages.Count -eq 0) {
    throw "No versioned packages at or below Mod version $Version were found under $packagesRootPath."
}

$currentPackage = $packages | Where-Object { $_.ModVersion -eq $Version } | Select-Object -First 1
if ($null -eq $currentPackage) {
    throw "Current Workshop package was not found: $(Join-Path $packagesRootPath "$Version/$modId")"
}

$selectedByVersion = @{}
foreach ($activeGameVersion in $activeGameVersions) {
    $selected = $packages |
        Where-Object { $_.MinGameSemanticVersion.CompareTo($activeGameVersion.SemanticVersion) -le 0 } |
        Sort-Object ModSemanticVersion -Descending |
        Select-Object -First 1
    if ($null -eq $selected) {
        throw "No package supports active game version $($activeGameVersion.Value)."
    }
    $selectedByVersion[$selected.ModVersion] = $selected
    Write-Host "Active game $($activeGameVersion.Value) selects Random Foreseer $($selected.ModVersion)."
}

$selectedPackages = @($selectedByVersion.Values | Sort-Object ModSemanticVersion)
if ($selectedByVersion.Count -eq 0) {
    throw "No Workshop variants were selected."
}
if (!$selectedByVersion.ContainsKey($Version)) {
    throw "Current Mod version $Version is not selected by any active game version. Update $ActiveGameVersionsPath before publishing."
}

$workshopJsonSource = Join-Path $sourceDir "workshop.json"
$imageSource = Join-Path $sourceDir "image.png"
if (!(Test-Path $workshopJsonSource)) {
    throw "Workshop metadata not found: $workshopJsonSource"
}
if (!(Test-Path $imageSource)) {
    throw "Workshop image not found: $imageSource"
}
$imageInfo = Get-Item -LiteralPath $imageSource
if ($imageInfo.Length -ge 1MB) {
    throw "Workshop image must be smaller than 1 MB for Steam upload: $imageSource ($($imageInfo.Length) bytes)."
}

if (Test-Path $workspacePath) {
    Remove-Item -LiteralPath $workspacePath -Recurse -Force
}
if (Test-Path $loaderBuildDir) {
    Remove-Item -LiteralPath $loaderBuildDir -Recurse -Force
}
New-Item -ItemType Directory -Path $contentDir -Force | Out-Null
New-Item -ItemType Directory -Path $loaderBuildDir -Force | Out-Null

Invoke-Checked dotnet @("build", $loaderProject, "-c", "Release", "-o", $loaderBuildDir)
$loaderDll = Join-Path $loaderBuildDir "RandomForeseer.Loader.dll"
Copy-RequiredFile $loaderDll (Join-Path $contentDir "$modId.dll")
Copy-RequiredFile $loaderNoticesPath (Join-Path $contentDir "THIRD_PARTY_NOTICES.md")

$compositeManifest = $currentPackage.Manifest.PSObject.Copy()
Set-JsonProperty $compositeManifest "has_dll" $true
Set-JsonProperty $compositeManifest "has_pck" $false
$minimumSelectedGameVersion = $selectedPackages |
    Sort-Object MinGameSemanticVersion |
    Select-Object -First 1 -ExpandProperty MinGameVersion
Set-JsonProperty $compositeManifest "min_game_version" $minimumSelectedGameVersion
$lowestModPackage = $selectedPackages | Sort-Object ModSemanticVersion | Select-Object -First 1
$rootDependencies = @($lowestModPackage.Dependencies | ForEach-Object {
    $entry = [ordered]@{ id = $_.Id }
    if (![string]::IsNullOrWhiteSpace($_.MinVersion)) {
        $entry.min_version = $_.MinVersion
    }
    $entry
})
Set-JsonProperty $compositeManifest "dependencies" $rootDependencies
$compositeManifest | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $contentDir "$modId.json") -Encoding utf8

$variantEntries = @()
foreach ($package in $selectedPackages) {
    $relativeDirectory = "lib/$($package.ModVersion)"
    $variantDirectory = Join-Path $contentDir $relativeDirectory
    New-Item -ItemType Directory -Path $variantDirectory -Force | Out-Null

    Copy-RequiredFile $package.DllPath (Join-Path $variantDirectory "$modId.dll")
    Copy-RequiredFile $package.PckPath (Join-Path $variantDirectory "$modId.pck")

    if (Test-Path $package.BuildInfoPath) {
        Copy-Item -LiteralPath $package.BuildInfoPath -Destination (Join-Path $variantDirectory "build-info.txt") -Force
    }

    $variantDependencies = @($package.Dependencies | ForEach-Object {
        $entry = [ordered]@{ id = $_.Id }
        if (![string]::IsNullOrWhiteSpace($_.MinVersion)) {
            $entry.min_version = $_.MinVersion
        }
        $entry
    })
    $variantEntries += [ordered]@{
        modVersion = $package.ModVersion
        minGameVersion = $package.MinGameVersion
        directory = $relativeDirectory
        dependencies = $variantDependencies
    }
}

$variantManifest = [ordered]@{
    schema = 1
    variants = $variantEntries
}
$variantManifest | ConvertTo-Json -Depth 10 |
    Set-Content -Path (Join-Path $contentDir "mod-variants.manifest") -Encoding utf8

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
Copy-RequiredFile $imageSource (Join-Path $workspacePath "image.png")

Write-Host "Workshop workspace prepared: $workspacePath"
Write-Host "Selected variants: $($selectedPackages.ModVersion -join ', ')"
