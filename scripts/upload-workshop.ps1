param(
    [string]$Version,

    [string]$WorkshopItemId,

    [string]$UploaderPath,

    [string]$WorkspaceDir,

    [string]$ConfigPath = "workshop/config.json",

    [string]$PackageDir,

    [switch]$SkipPrepare
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

function Resolve-RepoPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $root $Path
}

function Get-OptionalString {
    param(
        [object]$Object,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $Object) {
        return ""
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return ""
    }

    return [string]$property.Value
}

$resolvedConfigPath = Resolve-RepoPath $ConfigPath
$localConfigPath = [System.IO.Path]::ChangeExtension($resolvedConfigPath, ".local.json")

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

$config = $null
if (Test-Path $resolvedConfigPath) {
    $config = Get-Content $resolvedConfigPath -Raw | ConvertFrom-Json
}

if (Test-Path $localConfigPath) {
    if ($null -eq $config) {
        $config = [pscustomobject]@{}
    }

    $localConfig = Get-Content $localConfigPath -Raw | ConvertFrom-Json
    foreach ($property in $localConfig.PSObject.Properties) {
        $config | Add-Member -NotePropertyName $property.Name -NotePropertyValue $property.Value -Force
    }
}

if ([string]::IsNullOrWhiteSpace($UploaderPath) -and $null -ne $config) {
    $UploaderPath = Get-OptionalString $config "uploaderPath"
}

if ([string]::IsNullOrWhiteSpace($WorkspaceDir) -and $null -ne $config) {
    $WorkspaceDir = Get-OptionalString $config "workspaceDir"
}

if ([string]::IsNullOrWhiteSpace($WorkshopItemId) -and $null -ne $config) {
    $WorkshopItemId = Get-OptionalString $config "itemId"
}

if ([string]::IsNullOrWhiteSpace($WorkspaceDir)) {
    $WorkspaceDir = "artifacts/workshop"
}

if ([string]::IsNullOrWhiteSpace($UploaderPath)) {
    throw "Uploader path is not configured. Set uploaderPath in workshop/config.local.json, or pass -UploaderPath."
}

if (![System.IO.Path]::IsPathRooted($UploaderPath)) {
    $UploaderPath = Join-Path $root $UploaderPath
}

if ((Test-Path $UploaderPath) -and (Get-Item -LiteralPath $UploaderPath).PSIsContainer) {
    $uploaderDirectory = $UploaderPath
    $uploaderCandidates = @(
        (Join-Path $uploaderDirectory "bin/Release/net8.0/ModUploader.exe"),
        (Join-Path $uploaderDirectory "bin/Debug/net8.0/ModUploader.exe"),
        (Join-Path $uploaderDirectory "ModUploader.exe")
    )

    $UploaderPath = $uploaderCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($UploaderPath)) {
        $UploaderPath = $uploaderCandidates[0]
    }
}

if (![System.IO.Path]::IsPathRooted($WorkspaceDir)) {
    $WorkspaceDir = Join-Path $root $WorkspaceDir
}

if (!(Test-Path $UploaderPath)) {
    throw "Mod uploader not found: $UploaderPath"
}

if (!$SkipPrepare) {
    $prepareScript = Join-Path $PSScriptRoot "prepare-workshop.ps1"
    $prepareParams = @{
        WorkspaceDir = $WorkspaceDir
    }

    if (![string]::IsNullOrWhiteSpace($Version)) {
        $prepareParams.Version = $Version
    }

    if (![string]::IsNullOrWhiteSpace($PackageDir)) {
        $prepareParams.PackageDir = $PackageDir
    }

    & $prepareScript @prepareParams
}

if (!(Test-Path $WorkspaceDir)) {
    throw "Workshop workspace not found: $WorkspaceDir"
}

$uploadArgs = @("upload", "-w", $WorkspaceDir)
if (![string]::IsNullOrWhiteSpace($WorkshopItemId)) {
    $uploadArgs += @("-i", $WorkshopItemId)
}

Invoke-Checked $UploaderPath $uploadArgs
