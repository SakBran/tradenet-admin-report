<#
.SYNOPSIS
Poll the GitHub 'main' branch and deploy to UAT when it has new commits.

.DESCRIPTION
Runs UNATTENDED on the Build Server via a Windows Scheduled Task (e.g. repeat every 5 minutes).
Each tick it does a cheap `git fetch`; if the local checkout is behind origin/main it hard-resets
to origin/main and runs deploy.ps1 (build + graceful app_offline copy + health check). When nothing
changed it exits quietly so the log only records real deploys. A lock file stops a slow deploy from
overlapping the next tick.

It calls deploy.ps1 DIRECTLY (never deploy.bat) because deploy.bat ends with `pause`, which would
hang a non-interactive task forever.

.NOTES
Pass the real UNC share paths for -BackendTarget / -FrontendTarget. A task set to "run whether
logged on or not" has NO mapped P: drive (drive maps are per-interactive-session), so the P:
paths used for manual runs will not resolve here. Get the UNC with: (Get-PSDrive P).DisplayRoot

.EXAMPLE
Scheduled Task action:
  powershell -NoProfile -ExecutionPolicy Bypass -File "C:\repo\tools\auto-deploy-watch.ps1" `
    -BackendTarget "\\UATSRV\WEBSITES\tradenet-admin-backend" `
    -FrontendTarget "\\UATSRV\WEBSITES\tradenenet-admin-frontend"
#>

[CmdletBinding()]
param(
    [string]$Branch = 'main',

    # Repo checkout to deploy from. Defaults to the repo this script lives in (tools\..).
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),

    # UNC targets on the UAT file share. REPLACE the server name with the real one (see .NOTES),
    # or override these from the Scheduled Task action.
    [string]$BackendTarget = '\\UAT-SERVER\WEBSITES\tradenet-admin-backend',
    [string]$FrontendTarget = '\\UAT-SERVER\WEBSITES\tradenenet-admin-frontend',

    [string]$HealthUrl = 'https://reportuatapi.myanmartradenet.com/health',

    [string]$LogFile = (Join-Path $env:TEMP 'tradenet-auto-deploy.log')
)

# Native git writes progress to stderr; leave EAP at Continue so that is not mistaken for a
# terminating error. We gate on $LASTEXITCODE for git, and deploy.ps1 throws on its own failures
# (it sets its own 'Stop' internally), which the try/catch below catches regardless.
$ErrorActionPreference = 'Continue'

function Write-Log {
    param([string]$Message)
    $line = '[{0}] {1}' -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'), $Message
    Write-Host $line
    Add-Content -LiteralPath $LogFile -Value $line
}

# --- single-instance lock: a slow deploy must not overlap the next scheduled tick ---
$lockFile = Join-Path $env:TEMP 'tradenet-auto-deploy.lock'
$lockStream = $null
try {
    $lockStream = [System.IO.File]::Open($lockFile, 'OpenOrCreate', 'ReadWrite', 'None')
}
catch {
    # Another instance holds the lock - a previous deploy is still running. Skip quietly.
    return
}

try {
    if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot '.git'))) {
        Write-Log "RepoRoot '$RepoRoot' is not a git checkout. Aborting."
        return
    }
    Set-Location $RepoRoot

    git fetch origin $Branch 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Log "git fetch failed (exit $LASTEXITCODE). Skipping this tick."
        return
    }

    $local = (git rev-parse HEAD).Trim()
    $remote = (git rev-parse "origin/$Branch").Trim()

    if ($local -eq $remote) {
        # Up to date - nothing to do. Stay quiet so the log only contains real deploys.
        return
    }

    Write-Log "Change detected on '$Branch': $local -> $remote. Starting deploy."

    git reset --hard "origin/$Branch" 2>&1 | ForEach-Object { Write-Log "git: $_" }
    if ($LASTEXITCODE -ne 0) {
        Write-Log "git reset --hard failed (exit $LASTEXITCODE). Aborting deploy."
        return
    }

    $deployScript = Join-Path $RepoRoot 'deploy.ps1'
    Write-Log "Running deploy.ps1 -> backend '$BackendTarget'"
    try {
        # *>&1 captures Write-Host/Warning/etc. from deploy.ps1 (PS 5.0+ routes Write-Host through
        # the information stream). A failure inside deploy.ps1 throws and is caught below; deploy.ps1's
        # own finally still removes app_offline.htm so the site is never left offline.
        & $deployScript -NoGit -BackendTarget $BackendTarget -FrontendTarget $FrontendTarget -HealthUrl $HealthUrl *>&1 |
            ForEach-Object { Add-Content -LiteralPath $LogFile -Value ('    {0}' -f $_) }
        Write-Log "Deploy finished OK for $remote."
    }
    catch {
        Write-Log "Deploy FAILED for $remote : $($_.Exception.Message)"
    }
}
catch {
    Write-Log "Unhandled error: $($_.Exception.Message)"
}
finally {
    if ($lockStream) {
        $lockStream.Close()
        $lockStream.Dispose()
        Remove-Item -LiteralPath $lockFile -Force -ErrorAction SilentlyContinue
    }
}
