<#
.SYNOPSIS
    Starts the FabInspector.Web ASP.NET Core backend on https://localhost:7095.

.DESCRIPTION
    Wraps `dotnet run --launch-profile https`. Run this in a separate terminal
    before StartDevServer.ps1 — the React dev server proxies /api/* to this URL.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..' '..')

Push-Location $RepoRoot
try {
    Write-Host "==> dotnet run --project FabInspector.Web --launch-profile https" -ForegroundColor Cyan
    dotnet run --project .\FabInspector.Web\FabInspector.Web.csproj --launch-profile https
    if ($LASTEXITCODE -ne 0) { throw "dotnet run failed (exit $LASTEXITCODE)" }
}
finally {
    Pop-Location
}
