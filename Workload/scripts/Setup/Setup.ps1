<#
.SYNOPSIS
    Backwards compatibility wrapper for SetupWorkload.ps1

.NOTES
    This script is provided for backwards compatibility only.
    Please use SetupWorkload.ps1 directly for new implementations.
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$HostingType,
    
    [Parameter()]
    [string]$WorkloadName,
    
    [Parameter()]
    [string]$WorkloadDisplayName,
    
    [Parameter()]
    [string]$FrontendAppId,
    
    [Parameter()]
    [string]$BackendAppId,
    
    [Parameter()]
    [switch]$Force
)

# Forward all arguments to SetupWorkload.ps1
& (Join-Path $PSScriptRoot "SetupWorkload.ps1") @PSBoundParameters