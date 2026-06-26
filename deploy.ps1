<#
.SYNOPSIS
Deploy backend and frontend from the repo to P:\WEBSITES.

.DESCRIPTION
Pulls latest git changes, builds and publishes the Backend, runs Frontend build, and copies the outputs to the target deployment folders.
#>

[CmdletBinding()]
param(
    [switch]$NoGit,
    [switch]$NoFrontend,
    [string]$BackendTarget = 'P:\WEBSITES\tradenet-admin-backend',
    [string]$FrontendTarget = 'P:\WEBSITES\tradenenet-admin-frontend',
    # After the backend is back online, poll this URL until it returns 200 (non-fatal warning on failure).
    [string]$HealthUrl = 'https://reportuatapi.myanmartradenet.com/health',
    [switch]$SkipHealthCheck
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
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [int]$TimeoutSeconds = 30
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $stream = [System.IO.File]::Open($Path, 'Open', 'ReadWrite', 'None')
            $stream.Close()
            $stream.Dispose()
            return
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }

    Write-Warning "File still locked after $TimeoutSeconds s: $Path. Proceeding anyway; robocopy will retry."
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
Invoke-NativeCommand 'Building Backend...' { dotnet build -c Release }

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

# Take the backend offline (release the API.dll lock), copy, then bring it back online.
# The finally block guarantees the site is brought back up even if the copy fails.
$appOfflinePath = Join-Path $BackendTarget 'app_offline.htm'
try {
    Write-Host "Taking backend offline: $appOfflinePath"
    Set-Content -LiteralPath $appOfflinePath -Value $AppOfflineHtml -Encoding UTF8
    Wait-ForFileUnlock -Path (Join-Path $BackendTarget 'API.dll')

    Write-Host "Copying backend files to: $BackendTarget"
    Invoke-Robocopy -Source $publishOutput -Destination $BackendTarget -ExcludeFiles @('appsettings.json', 'appsettings.*.json', 'app_offline.htm')
}
finally {
    if (Test-Path -LiteralPath $appOfflinePath) {
        Write-Host 'Bringing backend online (removing app_offline.htm)...'
        Remove-Item -LiteralPath $appOfflinePath -Force -ErrorAction SilentlyContinue
    }
}

if (-not $NoFrontend) {
    $frontendDir = Join-Path $root 'Frontend'
    Set-Location $frontendDir

    # Vite bakes VITE_* from the environment into the bundle at build time (Frontend/src/config.ts
    # reads them, falling back to localhost only when unset). Default to the UAT API here so both
    # manual (deploy.bat) and automated (auto-deploy-watch.ps1) runs build the right URLs without
    # duplicating the values. An externally-set VITE_* (e.g. for a different environment) wins.
    if (-not $env:VITE_BASE_URL)  { $env:VITE_BASE_URL = 'https://reportuatapi.myanmartradenet.com/api/' }
    if (-not $env:VITE_IMAGE_URL) { $env:VITE_IMAGE_URL = 'https://reportuatapi.myanmartradenet.com/Image/' }
    if (-not $env:VITE_QR_URL)    { $env:VITE_QR_URL = 'https://uatapi.ecomreg.gov.mm/QR/' }

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
