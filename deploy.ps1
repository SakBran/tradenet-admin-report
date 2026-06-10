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
    [string]$FrontendTarget = 'P:\WEBSITES\tradenenet-admin-frontend'
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

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host "Root folder: $root"
Set-Location $root

if (-not $NoGit) {
    Invoke-NativeCommand 'Pulling latest changes from git...' { git pull --ff-only }
}

$backendDir = Join-Path $root 'Backend'
Set-Location $backendDir
Invoke-NativeCommand 'Building Backend...' { dotnet build -c Release }

$publishOutput = Join-Path $root 'Backend\publish'
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
Write-Host "Copying backend files to: $BackendTarget"
Invoke-Robocopy -Source $publishOutput -Destination $BackendTarget -ExcludeFiles @('appsettings.json', 'appsettings.*.json')

if (-not $NoFrontend) {
    $frontendDir = Join-Path $root 'Frontend'
    Set-Location $frontendDir
    Invoke-NativeCommand 'Installing Frontend dependencies...' { npm install --legacy-peer-deps }
    Invoke-NativeCommand 'Building Frontend...' { npm run build }

    $frontendOutput = Join-Path $frontendDir 'dist'
    New-Item -ItemType Directory -Force -Path $FrontendTarget | Out-Null
    Write-Host "Copying frontend files to: $FrontendTarget"
    Invoke-Robocopy -Source $frontendOutput -Destination $FrontendTarget -ExcludeFiles @('web.config')
}

Write-Host 'Deployment complete.'
