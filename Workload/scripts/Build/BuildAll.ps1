<#
.SYNOPSIS
    End-to-end build: .NET solution + React frontend.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$WorkloadRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$RepoRoot     = Resolve-Path (Join-Path $WorkloadRoot '..')

Push-Location $RepoRoot
try {
    Write-Host "==> dotnet build FabInspector.sln ($Configuration)" -ForegroundColor Cyan
    dotnet build .\FabInspector.sln -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw ".NET build failed (exit $LASTEXITCODE)" }
}
finally {
    Pop-Location
}

& (Join-Path $PSScriptRoot 'BuildFrontend.ps1')

Write-Host "All builds succeeded." -ForegroundColor Green
