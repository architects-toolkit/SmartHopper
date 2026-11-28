# Verifies hook staging in a disposable linked worktree without signing keys.
#Requires -Version 7.0

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("smarthopper-hook-test-" + [Guid]::NewGuid().ToString('N'))
$owner = Join-Path $tempRoot 'owner'
$linked = Join-Path $tempRoot 'linked'
$encoding = [Text.UTF8Encoding]::new($false)
$originalLocation = Get-Location

function Write-FixtureFile {
    param([string]$Path, [string]$Content)
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $Content, $encoding)
}

function Invoke-FixtureGit {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$GitArguments)
    $output = & git @GitArguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Fixture Git command failed: $output"
    }
    return $output
}

try {
    [IO.Directory]::CreateDirectory($owner) | Out-Null
    Invoke-FixtureGit @('init', $owner) | Out-Null
    Invoke-FixtureGit @('-C', $owner, 'config', 'user.name', 'Hook Regression Test') | Out-Null
    Invoke-FixtureGit @('-C', $owner, 'config', 'user.email', 'hook-test@example.invalid') | Out-Null

    Write-FixtureFile (Join-Path $owner 'src/Existing.cs') "class Existing { }`n"
    $placeholder = 'This value is automatically replaced by the build tooling before official builds.'
    Write-FixtureFile (Join-Path $owner 'Directory.Build.props') "<Project><SmartHopperPublicKey>$placeholder</SmartHopperPublicKey></Project>`n"
    Write-FixtureFile (Join-Path $owner 'README.md') "# Fixture`n"
    Write-FixtureFile (Join-Path $owner 'CHANGELOG.md') "# Changelog`n"
    Write-FixtureFile (Join-Path $owner 'Solution.props') "<Project />`n"

    $helper = @'
param([switch]$Apply, [switch]$DateOnly, [string]$CsprojPath)
Set-Location -LiteralPath $PSScriptRoot
[IO.File]::WriteAllText((Join-Path $PSScriptRoot '../hook-ran'), 'yes')
$global:LASTEXITCODE = 0
'@
    foreach ($name in @('Update-GitHubLabels.ps1', 'Update-LicenseHeaders.ps1', 'Change-SolutionVersion.ps1', 'Anonymize-SmartHopperPublicKey.ps1')) {
        Write-FixtureFile (Join-Path $owner "tools/$name") $helper
    }
    foreach ($name in @('pre-commit.ps1', 'post-commit.ps1')) {
        Write-FixtureFile (Join-Path $owner ".githooks/$name") ([IO.File]::ReadAllText((Join-Path $repoRoot ".githooks/$name")))
    }
    Invoke-FixtureGit @('-C', $owner, 'add', '.') | Out-Null
    Invoke-FixtureGit @('-C', $owner, 'commit', '-m', 'test: initialize hook fixture') | Out-Null
    Invoke-FixtureGit @('-C', $owner, 'worktree', 'add', '-b', 'hook-regression', $linked) | Out-Null

    $wrapper = "#!/usr/bin/env bash`nexec pwsh -NoProfile -File `"`$(dirname `"`$0`")/pre-commit.ps1`"`n"
    Write-FixtureFile (Join-Path $linked '.githooks/pre-commit') $wrapper
    Invoke-FixtureGit @('-C', $linked, 'config', 'core.hooksPath', '.githooks') | Out-Null
    Write-FixtureFile (Join-Path $linked 'src/Existing.cs') "class Existing { public int Value = 1; }`n"
    Write-FixtureFile (Join-Path $linked 'src/Added.cs') "class Added { }`n"
    Write-FixtureFile (Join-Path $linked 'Directory.Build.props') "<Project><SmartHopperPublicKey>$placeholder</SmartHopperPublicKey><Fixture>true</Fixture></Project>`n"
    Invoke-FixtureGit @('-C', $linked, 'add', '.githooks/pre-commit', 'src/Existing.cs', 'src/Added.cs', 'Directory.Build.props') | Out-Null
    Invoke-FixtureGit @('-C', $linked, 'commit', '-m', 'test: verify hook staging') | Out-Null

    if (-not (Test-Path (Join-Path $linked 'hook-ran'))) {
        throw 'The regression fixture did not execute the commit hook.'
    }

    $existing = Invoke-FixtureGit @('-C', $linked, 'show', 'HEAD:src/Existing.cs')
    $added = Invoke-FixtureGit @('-C', $linked, 'show', 'HEAD:src/Added.cs')
    if ("$existing" -notmatch 'Value = 1' -or "$added" -notmatch 'class Added') {
        throw 'The hook failed to preserve the staged source contents.'
    }
    $deleted = Invoke-FixtureGit @('-C', $linked, 'diff-tree', '--no-commit-id', '--name-only', '--diff-filter=D', '-r', 'HEAD')
    if ($deleted) {
        throw "The hook unexpectedly staged deletions: $deleted"
    }

    & pwsh -NoProfile -File (Join-Path $linked '.githooks/post-commit.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw 'The post-commit hook must tolerate a clean worktree without signing keys.'
    }
    Write-Host 'PASS: linked-worktree hooks preserve staged files after helper directory changes and tolerate missing local signing keys.'
}
finally {
    Set-Location -LiteralPath $originalLocation
    if (Test-Path $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
