<#
.SYNOPSIS
  Manage PFX certificate and Authenticode-sign SmartHopper provider assemblies.
.DESCRIPTION
  Generates a self-signed PFX, decodes/imports Base64 PFX, exports PFX as Base64, or
  recursively Authenticode-signs all SmartHopper.Providers.*.dll under a given path.
.PARAMETER Generate
  Creates a self-signed PFX certificate (signing.pfx) for Authenticode signing. Requires -Password.
.PARAMETER Base64
  Base64-encoded PFX data; decodes into signing.pfx. Requires -Password for export and signing.
.PARAMETER Password
  Password for PFX certificate import/export and signing operations.
  WARNING: Passing passwords as plain strings exposes them in shell history and process listings.
  For interactive use, omit this parameter to be prompted securely.
  For CI/CD automation, use environment variables or secrets management.
.PARAMETER Export
  Exports the signing.pfx file as Base64 text to stdout. Requires -Password.
.PARAMETER Sign
  Authenticode-signs all SmartHopper assemblies under the given path, using the decoded PFX.
  If no target path is provided (empty string), defaults to bin/<SolutionVersion>/Debug under the solution root.
.PARAMETER SignDebug
  Authenticode-signs SmartHopper assemblies in bin/<SolutionVersion>/Debug under the solution root.
.PARAMETER SignRelease
  Authenticode-signs SmartHopper assemblies in bin/<SolutionVersion>/Release under the solution root.
.PARAMETER Help
  Displays this help message.
.PARAMETER PfxPath
  Override default signing PFX path (default '..\signing.pfx', falling back to 'signing.pfx' next to this script).
#>
param(
    [switch]$Generate,
    [string]$Base64,
    [string]$Password,
    [switch]$Export,
    [switch]$Help,
    [string]$Sign,
    [switch]$SignDebug,
    [switch]$SignRelease,
    [string]$PfxPath
)

# Password handling: Accept string for CI/CD compatibility, but warn about security
$securePassword = $null
if (-not [string]::IsNullOrEmpty($Password)) {
    # CI/CD mode: Convert plain-text password to SecureString
    # WARNING: This exposes the password in shell history and process listings
    $securePassword = ConvertTo-SecureString -String $Password -AsPlainText -Force
    
    # Only warn in interactive sessions (not in CI/CD)
    if ([Environment]::UserInteractive -and -not $env:CI -and -not $env:GITHUB_ACTIONS) {
        Write-Warning "Password provided as plain text. For better security, omit -Password to be prompted securely."
    }
}

# default PFX paths; override via -PfxPath
$solutionRoot = Split-Path -Parent $PSScriptRoot
$defaultRootPfx = Join-Path -Path $solutionRoot -ChildPath 'signing.pfx'
$defaultLocalPfx = Join-Path -Path $PSScriptRoot -ChildPath 'signing.pfx'

if ($PfxPath) {
    $pfxPath = $PfxPath
} else {
    # Default to solution root signing.pfx
    $pfxPath = $defaultRootPfx
}

function Show-Help {
    Write-Host "Usage: .\Sign-Authenticode.ps1 [options]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -Generate               Creates a self-signed PFX certificate (signing.pfx). Requires -Password."
    Write-Host "  -Base64 <data>          Decodes Base64-encoded PFX into signing.pfx. Requires -Password."
    Write-Host "  -Password <pwd>         Password for PFX certificate import/export and signing.
                          WARNING: Plain-text passwords are insecure. Omit for secure prompt.
                          For CI/CD, use secrets management (e.g., GitHub Secrets)."
    Write-Host "  -Export                 Exports signing.pfx as Base64 text to stdout. Requires -Password."
    Write-Host "  -Sign <path>            Authenticode-signs all SmartHopper.Providers.*.dll under <path>. Requires Base64 or Generate."
    Write-Host "  -SignDebug              Authenticode-signs assemblies in bin/<SolutionVersion>/Debug under the solution root."
    Write-Host "  -SignRelease            Authenticode-signs assemblies in bin/<SolutionVersion>/Release under the solution root."
    Write-Host "  -Help                   Displays this help message."
    Write-Host "  -PfxPath <path>         Override default signing PFX path (default 'signing.pfx')."
}

if ($Help -or (-not $Generate -and -not $Base64 -and -not $Export -and -not $Sign -and -not $SignDebug -and -not $SignRelease)) {
    Show-Help
    exit 0
}

if ($Generate) {
    if (-not $Password) {
        $securePassword = Read-Host "Enter password for PFX certificate generation" -AsSecureString
        if (-not $securePassword) {
            Write-Error "-Password is required when generating a PFX certificate."
            exit 1
        }
    }
    Write-Host "Generating self-signed PFX certificate at $pfxPath" -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate -Subject "CN=SmartHopperDev" -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy Exportable -KeySpec Signature -Type CodeSigningCert
    Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $pfxPath -Password $securePassword
    Write-Host "PFX certificate created at $pfxPath" -ForegroundColor Green
} elseif ($Base64) {
    if (-not $Password) {
        $securePassword = Read-Host "Enter password for Base64 PFX import" -AsSecureString
        if (-not $securePassword) {
            Write-Error "-Password is required when importing a Base64 PFX."
            exit 1
        }
    }
    Write-Host "Decoding Base64 PFX into $pfxPath" -ForegroundColor Cyan
    [IO.File]::WriteAllBytes($pfxPath, [Convert]::FromBase64String($Base64))
} elseif ($Export) {
    if (-not (Test-Path $pfxPath)) {
        if (-not $PfxPath -and (Test-Path $defaultLocalPfx)) {
            $pfxPath = $defaultLocalPfx
        } else {
            Write-Error "$pfxPath not found."
            exit 1
        }
    }
    if (-not $Password) {
        $securePassword = Read-Host "Enter password for PFX export" -AsSecureString
        if (-not $securePassword) {
            Write-Error "-Password is required when exporting PFX."
            exit 1
        }
    }
    Write-Host "Exporting PFX as Base64:" -ForegroundColor Cyan
    $bytes = [IO.File]::ReadAllBytes($pfxPath)
    $b64 = [Convert]::ToBase64String($bytes)
    Write-Host $b64
} elseif ($Sign -or $SignDebug -or $SignRelease) {
    # Determine all target signing paths based on Sign / SignDebug / SignRelease
    $targetPaths = @()

    $explicitSignProvided = $PSBoundParameters.ContainsKey('Sign')

    # 1) Explicit -Sign <path>
    if ($explicitSignProvided -and -not [string]::IsNullOrWhiteSpace($Sign)) {
        $targetPaths += $Sign
    }

    # 2) Any mode that needs SolutionVersion-based paths
    $needsSolutionVersion = $SignDebug -or $SignRelease -or ($explicitSignProvided -and [string]::IsNullOrWhiteSpace($Sign))
    $solutionVersion = $null

    if ($needsSolutionVersion) {
        $solutionPropsPath = Join-Path $solutionRoot 'Solution.props'
        if (-not (Test-Path $solutionPropsPath)) {
            Write-Error "No explicit target path provided and Solution.props not found at '$solutionPropsPath' to infer bin/<SolutionVersion>/<Configuration>."
            exit 1
        }

        try {
            $xml = [xml](Get-Content $solutionPropsPath -Raw)
            $solutionVersion = $xml.Project.PropertyGroup.SolutionVersion
        } catch {
            Write-Error "Failed to read SolutionVersion from '$solutionPropsPath': $_"
            exit 1
        }

        if (-not $solutionVersion) {
            Write-Error "SolutionVersion not found in '$solutionPropsPath'; cannot infer default bin/<SolutionVersion>/<Configuration> path."
            exit 1
        }
    }

    # 3) -Sign "" (empty) => bin/<SolutionVersion>/Debug OR -SignDebug
    if ($explicitSignProvided -and [string]::IsNullOrWhiteSpace($Sign) -or $SignDebug) {
        $debugPath = Join-Path $solutionRoot ("bin/$solutionVersion/Debug")
        $targetPaths += $debugPath
        if ($explicitSignProvided -and [string]::IsNullOrWhiteSpace($Sign)) {
            Write-Host "No -Sign target path specified. Using default: $debugPath" -ForegroundColor Cyan
        } else {
            Write-Host "Using inferred target path for -SignDebug: $debugPath" -ForegroundColor Cyan
        }
    }

    # 4) -SignRelease => bin/<SolutionVersion>/Release
    if ($SignRelease) {
        $releasePath = Join-Path $solutionRoot ("bin/$solutionVersion/Release")
        $targetPaths += $releasePath
        Write-Host "Using inferred target path for -SignRelease: $releasePath" -ForegroundColor Cyan
    }

    # Deduplicate targets in case the same path was added multiple times
    $targetPaths = $targetPaths | Select-Object -Unique

    if ($targetPaths.Count -eq 0) {
        Write-Error "No valid signing targets computed from -Sign, -SignDebug, or -SignRelease."
        exit 1
    }

    # Decode Base64 into PFX if provided
    if ($Base64) {
        Write-Host "Decoding Base64 PFX into $pfxPath" -ForegroundColor Cyan
        [IO.File]::WriteAllBytes($pfxPath, [Convert]::FromBase64String($Base64))
    }
    # Ensure PFX file exists, with fallback to local signing.pfx when using defaults
    if (-not (Test-Path $pfxPath)) {
        if (-not $PfxPath -and (Test-Path $defaultLocalPfx)) {
            $pfxPath = $defaultLocalPfx
            Write-Host "Using local PFX certificate file: $pfxPath" -ForegroundColor Cyan
        } else {
            Write-Error "PFX file '$pfxPath' not found. Please use -Base64, or ensure a signing.pfx exists in the solution root or next to this script."
            exit 1
        }
    }

    # Ensure we have a password; if not, prompt interactively (useful for local/dev scenarios)
    if (-not $securePassword) {
        $securePassword = Read-Host "Enter password for Authenticode signing (PFX)" -AsSecureString
        if (-not $securePassword) {
            Write-Error "-Password is required for signing operations."
            exit 1
        }
    }

    $plainPassword = [System.Net.NetworkCredential]::new("", $securePassword).Password

    # Find signtool.exe once
    $signtoolPath = $null
    $signtoolCmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($signtoolCmd) {
        $signtoolPath = $signtoolCmd.Source
        Write-Host "Found signtool.exe on PATH: $signtoolPath" -ForegroundColor Cyan
    }

    # If not found on PATH, search in Windows SDK directories
    if (-not $signtoolPath) {
        Write-Host "Searching for signtool.exe in Windows SDK directories..." -ForegroundColor Cyan
        $programFiles = @(
            ${env:ProgramFiles(x86)},
            $env:ProgramFiles
        ) | Where-Object { $_ }

        foreach ($pf in $programFiles) {
            $sdkDir = Join-Path $pf "Windows Kits\10\bin"
            if (Test-Path $sdkDir) {
                # Get newest SDK version directory
                $versions = Get-ChildItem $sdkDir -Directory | Sort-Object -Property Name -Descending
                foreach ($ver in $versions) {
                    $candidatePath = Join-Path $ver.FullName "x64\signtool.exe"
                    if (Test-Path $candidatePath) {
                        $signtoolPath = $candidatePath
                        Write-Host "Found signtool.exe in Windows SDK: $signtoolPath" -ForegroundColor Cyan
                        break
                    }
                }
                if ($signtoolPath) { break }
            }
        }
    }

    if (-not $signtoolPath) {
        Write-Error "Could not find signtool.exe. Please ensure Windows SDK is installed."
        exit 1
    }

    # Security: Use explicit allowlist to prevent signing unintended/malicious assemblies
    $allowedAssemblies = @(
        "SmartHopper.Core.dll",
        "SmartHopper.Core.Grasshopper.dll",
        "SmartHopper.Components.dll",
        "SmartHopper.Infrastructure.dll",
        "SmartHopper.Menu.dll",
        "SmartHopper.Providers.OpenAI.dll",
        "SmartHopper.Providers.MistralAI.dll",
        "SmartHopper.Providers.DeepSeek.dll",
        "SmartHopper.Providers.OpenRouter.dll",
        "SmartHopper.Providers.Anthropic.dll",
        "SmartHopper.Providers.Gemini.dll"
    )

    # Test hook: allow non-SmartHopper assemblies when SMARTHOPPER_ALLOW_TEST_ASSEMBLY_SIGNING is set
    $allowAnyAssemblyForTests = $false
    if ($env:SMARTHOPPER_ALLOW_TEST_ASSEMBLY_SIGNING) {
        $flag = $env:SMARTHOPPER_ALLOW_TEST_ASSEMBLY_SIGNING.Trim().ToLowerInvariant()
        if ($flag -eq '1' -or $flag -eq 'true' -or $flag -eq 'yes') {
            $allowAnyAssemblyForTests = $true
        }
    }

    foreach ($targetPath in $targetPaths) {
        Write-Host "Signing provider assemblies under path '$targetPath' with Authenticode certificate" -ForegroundColor Cyan

        # Determine items to sign: explicit DLL or specific SmartHopper assemblies in directory
        if ((Test-Path $targetPath -PathType Leaf) -and ([IO.Path]::GetExtension($targetPath) -ieq ".dll")) {
            $fileName = [IO.Path]::GetFileName($targetPath)
            if ($fileName -notlike "SmartHopper*.dll") {
                if (-not $allowAnyAssemblyForTests) {
                    Write-Error "File '$fileName' is not a SmartHopper assembly"
                    exit 1
                } else {
                    Write-Host "Warning: signing non-SmartHopper assembly '$fileName' because SMARTHOPPER_ALLOW_TEST_ASSEMBLY_SIGNING is set." -ForegroundColor Yellow
                }
            }
            Write-Host "Signing explicit DLL: $targetPath" -ForegroundColor Cyan
            $items = @(Get-Item $targetPath)
        } elseif (Test-Path $targetPath -PathType Container) {
            Write-Host "Signing SmartHopper assemblies under directory: $targetPath" -ForegroundColor Cyan
            Write-Host "Allowed assemblies: $($allowedAssemblies -join ', ')" -ForegroundColor DarkGray
            $items = Get-ChildItem -Path $targetPath -Recurse -Filter "SmartHopper*.dll" |
                Where-Object { $allowedAssemblies -contains $_.Name }
            if ($items.Count -eq 0) {
                Write-Warning "No allowed SmartHopper assemblies found in '$targetPath'"
                continue
            }
        } else {
            Write-Error "Path '$targetPath' is not a .dll file or directory"
            exit 1
        }

        foreach ($dll in $items) {
            Write-Host "Signing $($dll.FullName) with PFX file (file-based signing)..." -ForegroundColor Cyan
            # Use file-based sign without /a to embed the certificate
            & $signtoolPath sign /fd SHA256 /f "$pfxPath" /p "$plainPassword" $dll.FullName
            if ($LASTEXITCODE -ne 0) {
                Write-Host "File-based signing failed (exit code $LASTEXITCODE), falling back to store-based signing..." -ForegroundColor Yellow
                $imported = Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation Cert:\CurrentUser\My -Password $securePassword
                if (-not $imported) {
                    Write-Error "Failed to import PFX certificate. Please verify the PFX password and that the runner has permission to import certificates."
                    exit 1
                }
                $thumb = $imported.Thumbprint
                Write-Host "Signing $($dll.FullName) with certificate thumbprint $thumb..." -ForegroundColor Cyan
                & $signtoolPath sign /fd SHA256 /sha1 $thumb $dll.FullName
                if ($LASTEXITCODE -ne 0) {
                    Write-Error "Fallback signing failed for $($dll.FullName)."
                    exit 1
                }
            }
        }
    }
}
