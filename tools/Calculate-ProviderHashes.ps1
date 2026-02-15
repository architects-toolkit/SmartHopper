<#
.SYNOPSIS
    Calculates SHA-256 hashes for SmartHopper provider DLLs and generates a JSON manifest.

.DESCRIPTION
    This script scans specified platform directories for SmartHopper provider DLLs,
    calculates their SHA-256 hashes, and generates a JSON manifest file with metadata.
    Used for provider assembly verification at runtime.

    Automatically locates build artifacts in bin/<SolutionVersion>/<Configuration> based on
    Solution.props and -Debug/-Release flags.

.PARAMETER ArtifactsPath
    Path to the artifacts directory containing provider DLLs. If not specified, defaults to
    bin/<SolutionVersion>/<Configuration> inferred from Solution.props and -Debug/-Release flags.

.PARAMETER Version
    Version string for the manifest (e.g., 1.2.3 or 1.2.3-alpha). If not specified, reads from
    Solution.props <SolutionVersion> element.

.PARAMETER Platforms
    Array of platform names to process (e.g., @("net7.0-windows", "net7.0")).
    Default: @("net7.0-windows", "net7.0")

.PARAMETER DebugBuild
    Use bin/<SolutionVersion>/Debug as the artifacts path. Requires Solution.props to exist.
    Cannot be used with -ReleaseBuild.

.PARAMETER ReleaseBuild
    Use bin/<SolutionVersion>/Release as the artifacts path. Requires Solution.props to exist.
    Default if neither -DebugBuild nor -ReleaseBuild is specified.
    Cannot be used with -DebugBuild.

.PARAMETER BuildNumber
    Optional GitHub Actions build/run number.

.PARAMETER CommitSha
    Optional Git commit SHA.

.PARAMETER Repository
    Optional GitHub repository (owner/repo format).

.PARAMETER OutputFile
    Optional output file path. If not specified, uses "provider-hashes-{version}.json".

.EXAMPLE
    .\Calculate-ProviderHashes.ps1 -ReleaseBuild
    Uses bin/<SolutionVersion>/Release from Solution.props, version from Solution.props

.EXAMPLE
    .\Calculate-ProviderHashes.ps1 -DebugBuild
    Uses bin/<SolutionVersion>/Debug from Solution.props, version from Solution.props

.EXAMPLE
    .\Calculate-ProviderHashes.ps1 -ArtifactsPath "artifacts" -Version "1.2.3"
    Uses explicit paths and version

.EXAMPLE
    .\Calculate-ProviderHashes.ps1 -Release -BuildNumber "123" -CommitSha "abc123" -Repository "owner/repo"
    Uses Release build with metadata
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ArtifactsPath = "",

    [Parameter(Mandatory = $false)]
    [string]$Version = "",

    [Parameter(Mandatory = $false)]
    [string[]]$Platforms = @("net7.0-windows", "net7.0"),

    [Parameter(Mandatory = $false)]
    [switch]$DebugBuild,

    [Parameter(Mandatory = $false)]
    [switch]$ReleaseBuild,

    [Parameter(Mandatory = $false)]
    [string]$BuildNumber = "",

    [Parameter(Mandatory = $false)]
    [string]$CommitSha = "",

    [Parameter(Mandatory = $false)]
    [string]$Repository = "",

    [Parameter(Mandatory = $false)]
    [string]$OutputFile = "",

    [Parameter(Mandatory = $false)]
    [string]$ProviderDllPattern = "SmartHopper.Providers.*.dll"
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Validate Debug/Release flags
if ($DebugBuild -and $ReleaseBuild) {
    Write-Host "##[error] Cannot use both -DebugBuild and -ReleaseBuild flags simultaneously."
    exit 1
}

# Validate input parameters
if ($Platforms.Count -eq 0) {
    Write-Host "##[error] Platforms array cannot be empty."
    exit 1
}

# Validate platform names (comprehensive validation)
foreach ($platform in $Platforms) {
    if ([string]::IsNullOrWhiteSpace($platform)) {
        Write-Host "##[error] Platform names cannot be empty or whitespace."
        exit 1
    }
    # Prevent path traversal: no slashes and no '..'
    if ($platform -match '[\\/]' -or $platform -match '\.\.') {
        Write-Host "##[error] Invalid platform name (contains path traversal or invalid characters): '$platform'"
        exit 1
    }
    # Validate format (alphanumeric, dots, hyphens only)
    if ($platform -notmatch '^[a-zA-Z0-9\.\-]+$') {
        Write-Host "##[error] Invalid platform name format: '$platform'. Only alphanumeric, dots, and hyphens allowed."
        exit 1
    }
}

# Validate provider DLL pattern
if ([string]::IsNullOrWhiteSpace($ProviderDllPattern)) {
    Write-Host "##[warning] ProviderDllPattern is empty, using default pattern."
    $ProviderDllPattern = "SmartHopper.Providers.*.dll"
}

# Determine solution root and read Solution.props if needed
$solutionRoot = Split-Path -Parent $PSScriptRoot
$solutionPropsPath = Join-Path $solutionRoot "Solution.props"

# If -DebugBuild or -ReleaseBuild is specified, or if Version/ArtifactsPath are not provided, read Solution.props
$needsSolutionProps = $DebugBuild -or $ReleaseBuild -or [string]::IsNullOrWhiteSpace($Version) -or [string]::IsNullOrWhiteSpace($ArtifactsPath)

if ($needsSolutionProps) {
    if (!(Test-Path $solutionPropsPath)) {
        Write-Host "##[error] Solution.props not found at '$solutionPropsPath'"
        Write-Host "##[error] Required for -Debug/-Release flags or when Version/ArtifactsPath are not explicitly provided."
        exit 1
    }

    try {
        [xml]$solutionProps = Get-Content $solutionPropsPath -Raw -ErrorAction Stop
        
        # Validate XML structure before accessing properties
        if ($null -eq $solutionProps) {
            Write-Host "##[error] Failed to parse Solution.props: XML is null"
            exit 1
        }
        
        if ($null -eq $solutionProps.Project) {
            Write-Host "##[error] Invalid Solution.props structure: missing Project element"
            exit 1
        }
        
        if ($null -eq $solutionProps.Project.PropertyGroup) {
            Write-Host "##[error] Invalid Solution.props structure: missing PropertyGroup element"
            exit 1
        }
        
        $solutionVersion = $solutionProps.Project.PropertyGroup.SolutionVersion
        
        if ([string]::IsNullOrWhiteSpace($solutionVersion)) {
            Write-Host "##[error] SolutionVersion not found in Solution.props"
            exit 1
        }
    } catch {
        Write-Host "##[error] Failed to read Solution.props: $_"
        exit 1
    }
}

# Resolve Version: use parameter if provided, otherwise use Solution.props
if ([string]::IsNullOrWhiteSpace($Version)) {
    if ($needsSolutionProps) {
        $Version = $solutionVersion
        Write-Host "Version not specified; using Solution.props: $Version"
    } else {
        Write-Host "##[error] Version parameter is required when Solution.props cannot be accessed."
        exit 1
    }
}

# Validate version format (semantic versioning)
if ($Version -notmatch '^[0-9]+\.[0-9]+(\.[0-9]+)?(-[a-zA-Z0-9\.\-]+)?$') {
    Write-Host "##[error] Invalid version format: '$Version'. Expected semantic versioning (e.g., 1.2.3 or 1.2.3-alpha)"
    exit 1
}

# Resolve ArtifactsPath: use parameter if provided, otherwise infer from -DebugBuild/-ReleaseBuild
if ([string]::IsNullOrWhiteSpace($ArtifactsPath)) {
    # Determine configuration (Debug or Release)
    $configuration = if ($DebugBuild) { "Debug" } else { "Release" }
    
    $ArtifactsPath = Join-Path $solutionRoot "bin" $Version $configuration
    Write-Host "ArtifactsPath not specified; using inferred path for -$($configuration): $ArtifactsPath"
}

# Validate and sanitize artifacts path to prevent path traversal
$ArtifactsPath = $ArtifactsPath -replace '\.\.[\\/]', ''
if ($ArtifactsPath -match '\.\.') {
    Write-Host "##[error] Artifacts path contains potential path traversal: '$ArtifactsPath'"
    exit 1
}

Write-Host "=========================================="
Write-Host "SmartHopper Provider Hash Calculator"
Write-Host "=========================================="
Write-Host "Version: $Version"
Write-Host "Artifacts Path: $ArtifactsPath"
Write-Host "Platforms: $($Platforms -join ', ')"
Write-Host ""

# Validate artifacts path exists
if (!(Test-Path $ArtifactsPath)) {
    Write-Host "##[error] Artifacts path '$ArtifactsPath' not found!"
    exit 1
}

# Initialize hash manifest
$hashManifest = [ordered]@{
    version   = $Version
    generated = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    algorithm = "SHA-256"
    providers = [ordered]@{}
    metadata  = [ordered]@{}
}

# Add optional metadata with validation
if ($BuildNumber) {
    # Validate build number is numeric
    if ($BuildNumber -notmatch '^[0-9]+$') {
        Write-Host "##[warning] BuildNumber is not numeric: '$BuildNumber'. Including as-is."
    }
    $hashManifest.metadata.buildNumber = $BuildNumber
}
if ($CommitSha) {
    # Validate commit SHA format (40 hex characters for full SHA)
    if ($CommitSha -notmatch '^[a-fA-F0-9]{7,40}$') {
        Write-Host "##[warning] CommitSha format appears invalid: '$CommitSha'. Including as-is."
    }
    $hashManifest.metadata.commitSha = $CommitSha
}
if ($Repository) {
    # Validate repository format (owner/repo)
    if ($Repository -notmatch '^[a-zA-Z0-9_\-]+/[a-zA-Z0-9_\-\.]+$') {
        Write-Host "##[warning] Repository format appears invalid: '$Repository'. Expected 'owner/repo'."
    }
    $hashManifest.metadata.repository = $Repository
}

$totalHashes = 0

# Process each platform
foreach ($platform in $Platforms) {
    $platformPath = Join-Path $ArtifactsPath $platform
    
    if (Test-Path $platformPath) {
        Write-Host "Processing platform: $platform"
        
        # Find all provider DLLs using the configured pattern
        try {
            $providerDlls = Get-ChildItem -Path $platformPath -Filter $ProviderDllPattern -File -ErrorAction Stop
        } catch {
            Write-Host "##[error] Failed to enumerate DLLs in '$platformPath': $_"
            exit 1
        }
        
        if ($null -eq $providerDlls -or $providerDlls.Count -eq 0) {
            Write-Host "  No provider DLLs found matching pattern '$ProviderDllPattern'"
        }
        else {
            foreach ($dll in $providerDlls) {
                try {
                    # Calculate SHA-256 hash
                    $hash = Get-FileHash -Path $dll.FullName -Algorithm SHA256 -ErrorAction Stop
                    $hashValue = $hash.Hash.ToLower()
                    
                    # Store in manifest with platform suffix for cross-platform tracking
                    $key = "$($dll.Name)-$platform"
                    $hashManifest.providers[$key] = $hashValue
                    $totalHashes++
                    
                    Write-Host "  $($dll.Name): $hashValue"
                } catch {
                    Write-Host "##[error] Failed to hash file '$($dll.FullName)': $_"
                    exit 1
                }
            }
        }
    }
    else {
        Write-Host "##[warning] Platform folder '$platformPath' not found, skipping"
    }
}

Write-Host ""

# Validate that we found providers
if ($totalHashes -eq 0) {
    Write-Host "##[error] No provider DLLs found in any platform folder!"
    Write-Host "Searched in: $ArtifactsPath"
    Write-Host "Platforms: $($Platforms -join ', ')"
    exit 1
}

# Determine output file path
if ([string]::IsNullOrWhiteSpace($OutputFile)) {
    $OutputFile = "provider-hashes-$Version.json"
}

# Convert to JSON and save
$hashJson = $hashManifest | ConvertTo-Json -Depth 10
Set-Content -Path $OutputFile -Value $hashJson -Encoding UTF8

Write-Host "=========================================="
Write-Host "Hash Calculation Complete"
Write-Host "=========================================="
Write-Host "Output File: $OutputFile"
Write-Host "Total Provider DLLs Hashed: $totalHashes"
Write-Host ""
Write-Host "Hash Manifest Content:"
Write-Host "----------------------------------------"
Get-Content $OutputFile | Write-Host
Write-Host "----------------------------------------"
Write-Host ""

# Output file path for GitHub Actions
Write-Host "Hash manifest saved successfully."
