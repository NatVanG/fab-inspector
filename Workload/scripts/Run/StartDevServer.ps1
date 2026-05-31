<#
.SYNOPSIS
    Starts the React workload Vite dev server on https://localhost:60006.

.DESCRIPTION
    Loads environment variables from Workload/.env.<Environment> (default: dev) and
    runs `npm run dev`. The dev server proxies /api/* to VITE_BACKEND_URL, so the
    FabInspector.Web backend must be running separately (see StartBackend.ps1).
#>
[CmdletBinding()]
param(
    [ValidateSet('dev','test','prod')]
    [string]$Environment = 'dev'
)

$ErrorActionPreference = 'Stop'
$WorkloadRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')

Push-Location $WorkloadRoot
try {
    $envFile = ".env.$Environment"
    if (-not (Test-Path $envFile)) {
        throw "Missing $envFile in $WorkloadRoot."
    }

    Write-Host "==> Loading $envFile" -ForegroundColor Cyan
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^\s*([^#=]+?)\s*=\s*(.*?)\s*$') {
            [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
        }
    }

    Write-Host "==> npm run dev" -ForegroundColor Cyan
    npm run dev
    if ($LASTEXITCODE -ne 0) { throw "npm run dev failed (exit $LASTEXITCODE)" }
}
finally {
    Pop-Location
}
