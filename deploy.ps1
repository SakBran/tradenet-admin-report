<#
.SYNOPSIS
Deploy backend and frontend from the repo to M:\ (T20-ADMIN-REPORT-*).

.DESCRIPTION
Pulls latest git changes, builds and publishes the Backend, runs Frontend build, and copies the outputs to the target deployment folders.

Taking the backend offline is a TWO-STAGE affair, because dropping app_offline.htm alone is not
reliably enough to release API.dll:

  1. app_offline.htm is written into the target root. ANCM detects it, shuts the app down and is
     expected to release the lock on API.dll.
  2. If API.dll is STILL locked after -UnlockTimeoutSeconds, the app pool named -AppPoolName is
     stopped on EVERY node listed in -IisServer. Both IIS servers publish this same content root, so
     EITHER one's worker process can be the one holding the file open. Stopping the pool forcibly
     ends that worker process. Every pool that was stopped is restarted by the finally block - also
     when the copy, or the whole deploy, fails.

The lock is then VERIFIED again before anything is copied. If it still cannot be released the deploy
aborts with a clear message instead of letting robocopy fail halfway and leaving a half-updated site.

Without stage 2 a healthy, busy app keeps API.dll mapped longer than the wait allows, robocopy
fails with ERROR 32 (exit code >= 8) and the whole run aborts - including the frontend build that
would otherwise have followed it.

Frontend-only release: point -BackendTarget at a throwaway folder (e.g. %TEMP%\backend-scratch) so
the backend is built and copied outside production while the frontend step runs normally.

.NOTES
Sync this script with the "Production Deployment" custom agent
(<profile>\prompts\production-deployment.agent.md), which documents the same runbook.
#>

[CmdletBinding()]
param(
    [switch]$NoGit,
    [switch]$NoFrontend,
    # [string]$BackendTarget = 'P:\WEBSITES\tradenet-admin-backend',
    [string]$BackendTarget = 'M:\T20-ADMIN-REPORT-BACKEND',
    
    [string]$FrontendTarget = 'M:\T20-ADMIN-REPORT-FRONTEND',
    # After the backend is back online, poll this URL until it returns 200 (non-fatal warning on failure).
    [string]$HealthUrl = 'https://reportapi.myanmartradenet.com/health',
    [switch]$SkipHealthCheck,

    # How long to wait for ANCM to release API.dll after app_offline.htm is written (seconds).
    [int]$UnlockTimeoutSeconds = 120,

    # Fallback used when API.dll is still locked after -UnlockTimeoutSeconds: this app pool is stopped
    # on every node listed here, so the worker process that still holds API.dll is forced to exit.
    # Both IIS servers publish this same content root, so either one's worker can hold the file.
    # Every pool that was stopped is restarted once the copy finishes (or the deploy fails).
    # Pass -IisServer to limit this to a single node, or -AppPoolName '' to disable the fallback.
    [string[]]$IisServer = @('adminvm.myanmartradenet.com', 'adminvm2.myanmartradenet.com'),
    [string]$AppPoolName = 'ReportBackend'
)

$ErrorActionPreference = 'Stop'

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    Write-Host $Description
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Robocopy {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [string]$Destination,

        [string[]]$ExcludeFiles = @()
    )

    $arguments = @(
        $Source,
        $Destination,
        '/E',
        '/MT:8',
        '/R:3',
        '/W:5',
        '/NFL',
        '/NDL',
        '/NJH',
        '/NJS',
        '/nc',
        '/ns',
        '/np'
    )

    if ($ExcludeFiles.Count -gt 0) {
        $arguments += '/XF'
        $arguments += $ExcludeFiles
    }

    & robocopy @arguments
    $exitCode = $LASTEXITCODE
    if ($exitCode -ge 8) {
        throw "Robocopy from '$Source' to '$Destination' failed with exit code $exitCode."
    }

    Write-Host "Robocopy completed with exit code $exitCode."
}

function Remove-LocalDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$AllowedRoot
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $resolvedRoot = [System.IO.Path]::GetFullPath($AllowedRoot)
    if (-not $resolvedRoot.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $resolvedRoot += [System.IO.Path]::DirectorySeparatorChar
    }

    if (-not $resolvedPath.StartsWith($resolvedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove '$resolvedPath' because it is outside '$resolvedRoot'."
    }

    Write-Host "Removing local generated folder: $resolvedPath"
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Remove-Item -LiteralPath $resolvedPath -Recurse -Force
            return
        }
        catch {
            if ($attempt -eq 5) {
                throw
            }

            Start-Sleep -Seconds 2
        }
    }
}

# Dropping app_offline.htm into an ASP.NET Core site root makes the ASP.NET Core Module (ANCM)
# gracefully stop the worker process, releasing the lock on API.dll so robocopy can overwrite it.
# Removing the file lets the app restart on the next request. This replaces the manual
# "stop IIS -> copy -> start IIS" dance with no IIS Manager access required.
$AppOfflineHtml = @'
<!DOCTYPE html>
<html lang="en">
<head><meta charset="utf-8" /><title>Deploying an update...</title></head>
<body style="font-family:-apple-system,Segoe UI,Roboto,sans-serif;text-align:center;padding-top:80px;color:#333">
  <h1>Deploying an update...</h1>
  <p>The service is briefly offline while a new version is published. Please retry in a moment.</p>
</body>
</html>
'@

function Wait-ForFileUnlock {
    # Block until $Path can be opened for write (i.e. ANCM has released the lock), or timeout.
    # A missing file is treated as already unlocked (first-ever deploy).
    # Returns $true when the file is free to overwrite, $false when the timeout expired.
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [int]$TimeoutSeconds = 120
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $true
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $stream = [System.IO.File]::Open($Path, 'Open', 'ReadWrite', 'None')
            $stream.Close()
            $stream.Dispose()
            return $true
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }

    Write-Warning "File still locked after $TimeoutSeconds s: $Path"
    return $false
}

function Stop-IisAppPool {
    # Force the worker process holding API.dll to exit. This briefly takes that node's site down; it is
    # the proven remedy when the app_offline.htm hand-off does not release the file in time.
    # Returns the resulting pool state so the caller can report what really happened.
    param(
        [Parameter(Mandatory = $true)]
        [string]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Pool
    )

    Invoke-Command -ComputerName $Server -ScriptBlock {
        param($PoolName)
        Import-Module WebAdministration -ErrorAction Stop
        Stop-WebAppPool -Name $PoolName -ErrorAction Stop
        (Get-Item "IIS:\AppPools\$PoolName").State
    } -ArgumentList $Pool
}

function Start-IisAppPool {
    # Counterpart of Stop-IisAppPool. Returns the resulting pool state.
    param(
        [Parameter(Mandatory = $true)]
        [string]$Server,

        [Parameter(Mandatory = $true)]
        [string]$Pool
    )

    Invoke-Command -ComputerName $Server -ScriptBlock {
        param($PoolName)
        Import-Module WebAdministration -ErrorAction Stop
        Start-WebAppPool -Name $PoolName -ErrorAction Stop
        (Get-Item "IIS:\AppPools\$PoolName").State
    } -ArgumentList $Pool
}

function Test-DeploymentHealth {
    # Poll $Url until it returns HTTP 200 (the app may cold-start on the first request).
    # Non-fatal: prints DEPLOY OK / DEPLOY HEALTH CHECK FAILED but never throws.
    param(
        [Parameter(Mandatory = $true)]
        [string]$Url,

        [int]$TimeoutSeconds = 90
    )

    Write-Host "Health check (waiting for 200): $Url"
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 10
            if ($response.StatusCode -eq 200) {
                Write-Host 'DEPLOY OK - health check returned 200.'
                return $true
            }
        }
        catch {
            Start-Sleep -Seconds 3
        }
    }

    Write-Warning "DEPLOY HEALTH CHECK FAILED - $Url did not return 200 within $TimeoutSeconds s. Verify the site manually."
    return $false
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host "Root folder: $root"
Set-Location $root

if (-not $NoGit) {
    Invoke-NativeCommand 'Pulling latest changes from git...' { git pull --ff-only }
}

$backendDir = Join-Path $root 'Backend'
$legacyPublishOutput = Join-Path $backendDir 'publish'
Remove-LocalDirectory -Path $legacyPublishOutput -AllowedRoot $backendDir

$publishOutput = Join-Path $root '.deploy\backend-publish'
Remove-LocalDirectory -Path $publishOutput -AllowedRoot $root
New-Item -ItemType Directory -Force -Path $publishOutput | Out-Null

Set-Location $backendDir

# Stamp the commit into the assembly's informational version. ExcelExportWorker reads it back and
# appends it to its worker id, which the jobs API reports as `processedBy` -- that is how a stale
# API instance sharing the export queue is identified, without having to download and fingerprint
# a generated file.
#
# Set as an environment variable rather than a -p: argument on purpose: MSBuild picks environment
# variables up as global properties, so the build/publish command lines below stay exactly as they
# were. Non-fatal -- an unstamped build still runs and reports "@unstamped".
try {
    $sha = (& git -C $root rev-parse --short HEAD 2>$null)
    if ($LASTEXITCODE -eq 0 -and $sha) {
        $env:SourceRevisionId = $sha.Trim()
        Write-Host "Stamping backend build with commit: $env:SourceRevisionId"
    }
}
catch {
    Write-Warning "Could not read the current commit; the backend build will be unstamped. $_"
}

Invoke-NativeCommand 'Building Backend...' { dotnet build API.csproj -c Release }

Invoke-NativeCommand "Publishing Backend to: $publishOutput" { dotnet publish API.csproj -c Release -o $publishOutput }

$backendConfigFiles = @(
    Join-Path $publishOutput 'appsettings.json'
) + @(
    Get-ChildItem -LiteralPath $publishOutput -Filter 'appsettings.*.json' -File -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty FullName
)

foreach ($configFile in $backendConfigFiles) {
    if (Test-Path -LiteralPath $configFile) {
        Write-Host "Removing backend publish config before copy: $configFile"
        Remove-Item -LiteralPath $configFile -Force
    }
}

New-Item -ItemType Directory -Force -Path $BackendTarget | Out-Null

# A 'publish' folder inside the site root is the signature of someone publishing manually into the
# live folder: the new code lands in <root>\publish\publish\... and the site silently keeps serving
# the old build. This script never writes there, so the folder is safe to delete.
if (Test-Path -LiteralPath (Join-Path $BackendTarget 'publish')) {
    Write-Warning "A stray 'publish' folder exists inside '$BackendTarget'. That is left over from a manual publish into the site root; it is unused and can be deleted."
}

# Take the backend offline (release the API.dll lock), copy, then bring it back online.
# The finally block guarantees that app_offline.htm is removed and that EVERY pool we stopped is
# started again, even when the copy (or the whole deploy) fails.
$apiDllPath = Join-Path $BackendTarget 'API.dll'
$appOfflinePath = Join-Path $BackendTarget 'app_offline.htm'
$stoppedPools = New-Object System.Collections.Generic.List[string]
try {
    Write-Host "Taking backend offline: $appOfflinePath"
    Set-Content -LiteralPath $appOfflinePath -Value $AppOfflineHtml -Encoding UTF8

    Write-Host "Waiting up to ${UnlockTimeoutSeconds}s for the app to release API.dll..."
    $unlocked = Wait-ForFileUnlock -Path $apiDllPath -TimeoutSeconds $UnlockTimeoutSeconds

    if (-not $unlocked -and $AppPoolName -and @($IisServer).Count -gt 0) {
        Write-Warning "API.dll is still locked. Stopping app pool '$AppPoolName' on each node that serves this share (brief downtime on each):"

        foreach ($server in @($IisServer)) {
            try {
                $state = Stop-IisAppPool -Server $server -Pool $AppPoolName
                $stoppedPools.Add($server)
                Write-Host ("    {0,-34} stopped (pool state: {1})" -f $server, $state)
            }
            catch {
                Write-Warning ("    {0,-34} could NOT be stopped: {1}" -f $server, $_.Exception.Message)
            }
        }

        if ($stoppedPools.Count -eq 0) {
            Write-Warning "No app pool could be stopped. A node that cannot be reached from here may be the one holding API.dll (see the failure runbook in the Production Deployment agent)."
        }

        Write-Host 'Waiting up to 60s for the lock to clear...'
        $unlocked = Wait-ForFileUnlock -Path $apiDllPath -TimeoutSeconds 60
    }

    # Verify before copying: a half-updated site is worse than a failed deploy, and the finally block
    # puts every stopped pool back online either way.
    if (-not $unlocked) {
        $stoppedText = if ($stoppedPools.Count -gt 0) { $stoppedPools -join ', ' } else { 'none' }
        throw ("API.dll is still locked, so nothing was copied - the site is unchanged. " +
            "Pools stopped: $stoppedText. Holder is a node that could not be stopped; candidates: " +
            "$(@($IisServer) -join ', '). Stop that node's '$AppPoolName' pool (IIS Manager over WMSVC " +
            "port 8172 works without WinRM) and re-run the deploy.")
    }

    Write-Host "Copying backend files to: $BackendTarget"
    Invoke-Robocopy -Source $publishOutput -Destination $BackendTarget -ExcludeFiles @('appsettings.json', 'appsettings.*.json', 'app_offline.htm')
}
finally {
    if (Test-Path -LiteralPath $appOfflinePath) {
        Write-Host 'Bringing backend online (removing app_offline.htm)...'
        Remove-Item -LiteralPath $appOfflinePath -Force -ErrorAction SilentlyContinue
    }

    foreach ($server in $stoppedPools) {
        Write-Host "Restarting app pool '$AppPoolName' on '$server'..."
        try {
            $state = Start-IisAppPool -Server $server -Pool $AppPoolName
            Write-Host ("    {0,-34} started (pool state: {1})" -f $server, $state)
        }
        catch {
            Write-Warning ("    {0,-34} could NOT be restarted: {1}" -f $server, $_.Exception.Message)
            Write-Warning "    START app pool '$AppPoolName' on '$server' MANUALLY before walking away."
        }
    }
}

if (-not $NoFrontend) {
    $frontendDir = Join-Path $root 'Frontend'
    Set-Location $frontendDir

    # Vite bakes VITE_* from the environment into the bundle at build time (Frontend/src/config.ts
    # reads them, falling back to localhost only when unset). Default to the production API here so
    # both manual (deploy.bat) and automated (auto-deploy-watch.ps1) runs build the right URLs without
    # duplicating the values. An externally-set VITE_* (e.g. for a different environment) wins.
    if (-not $env:VITE_BASE_URL) { $env:VITE_BASE_URL = 'https://reportapi.myanmartradenet.com/api/' }
    if (-not $env:VITE_IMAGE_URL) { $env:VITE_IMAGE_URL = 'https://reportapi.myanmartradenet.com/Image/' }
    if (-not $env:VITE_QR_URL) { $env:VITE_QR_URL = 'https://api.ecomreg.gov.mm/QR/' }

    Invoke-NativeCommand 'Installing Frontend dependencies...' { npm install --legacy-peer-deps }
    Invoke-NativeCommand 'Building Frontend...' { npm run build }

    $frontendOutput = Join-Path $frontendDir 'dist'
    New-Item -ItemType Directory -Force -Path $FrontendTarget | Out-Null
    Write-Host "Copying frontend files to: $FrontendTarget"
    Invoke-Robocopy -Source $frontendOutput -Destination $FrontendTarget -ExcludeFiles @('web.config')
}

if (-not $SkipHealthCheck -and $HealthUrl) {
    Test-DeploymentHealth -Url $HealthUrl | Out-Null
}

Write-Host 'Deployment complete.'
