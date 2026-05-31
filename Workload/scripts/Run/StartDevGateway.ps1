<#
.SYNOPSIS
    Registers the local workload bundle with the Fabric Extensibility Toolkit DevGateway.

.DESCRIPTION
    The DevGateway is distributed separately as part of the Microsoft Fabric
    Extensibility Toolkit (see https://learn.microsoft.com/fabric/extensibility-toolkit).
    Install it once via the toolkit installer, then run it pointed at this
    repository's Workload/Manifest folder so Fabric loads your local frontend
    bundle.

    This script is a placeholder that documents the expected invocation; replace
    the body with the actual DevGateway executable path for your machine.
#>
[CmdletBinding()]
param(
    [string]$DevGatewayExe = $env:FABRIC_DEVGATEWAY_EXE
)

$ErrorActionPreference = 'Stop'
$WorkloadRoot = Resolve-Path (Join-Path $PSScriptRoot '..' '..')
$ManifestRoot = Join-Path $WorkloadRoot 'Manifest'

if (-not $DevGatewayExe) {
    Write-Warning "Set `$env:FABRIC_DEVGATEWAY_EXE to the DevGateway executable installed by the Fabric Extensibility Toolkit, then re-run."
    Write-Host ""
    Write-Host "Expected manual invocation:" -ForegroundColor Cyan
    Write-Host "    & `$env:FABRIC_DEVGATEWAY_EXE --workload-manifest `"$ManifestRoot`""
    exit 1
}

if (-not (Test-Path $DevGatewayExe)) {
    throw "DevGateway executable not found at: $DevGatewayExe"
}

Write-Host "==> Launching DevGateway against $ManifestRoot" -ForegroundColor Cyan
& $DevGatewayExe --workload-manifest $ManifestRoot
