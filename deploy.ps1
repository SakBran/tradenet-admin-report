<#
.SYNOPSIS
Deploy backend and frontend from the repo to P:\WEBSITES.

.DESCRIPTION
Pulls latest git changes, builds and publishes the Backend, runs Frontend build, and copies the outputs to the target deployment folders.
#>

[CmdletBinding()]
param(
    [switch]$NoGit,
    [switch]$NoFrontend
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Write-Host "Root folder: $root"
Set-Location $root

if (-not $NoGit) {
    Write-Host 'Pulling latest changes from git...'
    git pull --ff-only
}

$backendDir = Join-Path $root 'Backend'
Set-Location $backendDir
Write-Host 'Building Backend...'
dotnet build

$publishOutput = Join-Path $root 'Backend\publish'
Write-Host "Publishing Backend to: $publishOutput"
dotnet publish API.csproj -c Release -o $publishOutput

$backendTarget = 'P:\WEBSITES\tradenet-admin-backend'
New-Item -ItemType Directory -Force -Path $backendTarget | Out-Null
Write-Host "Copying backend files to: $backendTarget"
robocopy $publishOutput $backendTarget /MIR /MT:8 /NFL /NDL /NJH /NJS /nc /ns /np

if (-not $NoFrontend) {
    $frontendDir = Join-Path $root 'Frontend'
    Set-Location $frontendDir
    Write-Host 'Building Frontend...'
    npm run build

    $frontendOutput = Join-Path $frontendDir 'dist'
    $frontendTarget = 'P:\WEBSITES\tradenenet-admin-frontend'
    New-Item -ItemType Directory -Force -Path $frontendTarget | Out-Null
    Write-Host "Copying frontend files to: $frontendTarget"
    robocopy $frontendOutput $frontendTarget /MIR /MT:8 /NFL /NDL /NJH /NJS /nc /ns /np
}

Write-Host 'Deployment complete.'
