<#
.SYNOPSIS
    Installs the React workload's npm dependencies and restores the .NET solution.

.EXAMPLE
    pwsh ./scripts/Setup/SetupDevEnvironment.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$WorkloadRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$RepoRoot     = Resolve-Path (Join-Path $WorkloadRoot '..')

Push-Location $WorkloadRoot
try {
    Write-Host "==> npm install (Workload/)" -ForegroundColor Cyan
    npm install
    if ($LASTEXITCODE -ne 0) { throw "npm install failed (exit $LASTEXITCODE)" }
}
finally {
    Pop-Location
}

Push-Location $RepoRoot
try {
    Write-Host "==> dotnet restore (FabInspector.sln)" -ForegroundColor Cyan
    dotnet restore .\FabInspector.sln
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed (exit $LASTEXITCODE)" }
}
finally {
    Pop-Location
}

Write-Host "Dev environment ready." -ForegroundColor Green
