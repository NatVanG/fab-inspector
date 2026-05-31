<#
.SYNOPSIS
    Builds the React frontend into ../build/Frontend.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$WorkloadRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')

Push-Location $WorkloadRoot
try {
    Write-Host "==> npm run build" -ForegroundColor Cyan
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed (exit $LASTEXITCODE)" }
}
finally {
    Pop-Location
}

Write-Host "Frontend bundle written to $(Resolve-Path (Join-Path $WorkloadRoot '..' 'build' 'Frontend'))" -ForegroundColor Green
