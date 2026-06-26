<#
.SYNOPSIS
Watch the GitHub 'main' branch and auto-deploy to UAT when it changes.

.DESCRIPTION
Run this once in your RDP session on the Build Server (easiest: double-click auto-deploy.bat in
the repo root). Every few minutes it checks origin/main; when new commits appear it pulls them and
runs deploy.ps1 (build + graceful app_offline copy + health check).

Because it runs in YOUR interactive session, the P: share is already mapped and your git
credentials are already available - so there is no scheduled task, no stored password, and no admin
needed. It keeps running after you disconnect RDP; it stops only when you sign out (just
double-click again to restart).

.EXAMPLE
  .\tools\auto-deploy-watch.ps1 -Loop     # keep watching (what auto-deploy.bat runs)
  .\tools\auto-deploy-watch.ps1           # check once and exit
#>

[CmdletBinding()]
param(
    [string]$Branch = 'main',
    [string]$RepoRoot,
    [int]$IntervalSeconds = 5,
    [switch]$Loop,
    [string]$LogFile = (Join-Path $env:ProgramData 'TradeNetDeploy\auto-deploy.log')
)

# Resolve the repo root (this script lives in <repo>\tools). Done HERE, not as a param default,
# because $PSScriptRoot is not reliably populated while param defaults are evaluated.
if (-not $RepoRoot) {
    $scriptDir = $PSScriptRoot
    if (-not $scriptDir) { $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path }
    $RepoRoot = Split-Path -Parent $scriptDir
}

# git writes progress to stderr; leave EAP at Continue so that is not treated as a terminating
# error. deploy.ps1 throws on its own failures (it sets 'Stop' internally), caught below.
$ErrorActionPreference = 'Continue'

$stateDir = Split-Path -Parent $LogFile
if (-not (Test-Path -LiteralPath $stateDir)) { New-Item -ItemType Directory -Force -Path $stateDir | Out-Null }

function Write-Log {
    param([string]$Message)
    $line = '[{0}] {1}' -f (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'), $Message
    Write-Host $line
    Add-Content -LiteralPath $LogFile -Value $line
}

function Invoke-DeployTick {
    if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot '.git'))) {
        Write-Log "RepoRoot '$RepoRoot' is not a git checkout. Skipping."
        return
    }
    Set-Location $RepoRoot

    git fetch origin $Branch 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Log "git fetch failed (exit $LASTEXITCODE). Skipping this tick."; return }

    $local  = (git rev-parse HEAD).Trim()
    $remote = (git rev-parse "origin/$Branch").Trim()
    if ($local -eq $remote) { return }   # nothing new - stay quiet so the log only holds real deploys

    Write-Log "Change detected on '$Branch': $local -> $remote. Deploying."
    git reset --hard "origin/$Branch" 2>&1 | ForEach-Object { Write-Log "git: $_" }
    if ($LASTEXITCODE -ne 0) { Write-Log "git reset --hard failed (exit $LASTEXITCODE). Aborting deploy."; return }

    try {
        # -NoGit: we already synced. deploy.ps1 uses its default P: targets, which resolve because
        # this runs in your mapped interactive session. *>&1 captures its Write-Host output to the log.
        & (Join-Path $RepoRoot 'deploy.ps1') -NoGit *>&1 |
            ForEach-Object { Add-Content -LiteralPath $LogFile -Value ('    {0}' -f $_) }
        Write-Log "Deploy finished OK for $remote."
    }
    catch {
        Write-Log "Deploy FAILED for $remote : $($_.Exception.Message)"
    }
}

if ($Loop) {
    Write-Log "Watcher started (checking '$Branch' every $IntervalSeconds s). Leave this window open. Ctrl+C to stop."
    while ($true) {
        Invoke-DeployTick
        Start-Sleep -Seconds $IntervalSeconds
    }
}
else {
    Invoke-DeployTick
}
