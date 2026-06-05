param(
    # The GUID of the workspace to use for the developer environment
    [string]$DevWorkspaceId,
    # Force flag to overwrite existing configurations and don't prompt the user
    [boolean]$Force = $false

)

# Script to set up individual developer environment
# This script should be run by each developer after cloning the repository
# It configures DevGateway and other developer-specific settings

Write-Host "🛠️ Setting up Developer Environment" -ForegroundColor Green
Write-Host ""

# Paths
$ScriptRoot = $PSScriptRoot
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptRoot)
$DevGatewayDir = Join-Path $ProjectRoot "build\DevGateway"
$WorkloadDir = Join-Path $ProjectRoot "Workload"
$ManifestDir = Join-Path $ProjectRoot "build/Manifest"


# Check if .env files exist (should be created by SetupProject.ps1)
$EnvDevFile = Join-Path $WorkloadDir ".env.dev"
if (-not (Test-Path $EnvDevFile)) {
    Write-Error "Development environment file not found: $EnvDevFile"
    Write-Error "Please run SetupProject.ps1 first to configure the project."
    exit 1
}

# Load configuration from .env.dev file
Write-Host "📖 Loading project configuration from .env.dev..." -ForegroundColor Blue
$EnvConfig = @{}
Get-Content $EnvDevFile | Where-Object { $_ -notmatch '^#' -and $_ -notmatch '^$' } | ForEach-Object {
    $key, $value = $_ -split '=', 2
    if ($key -and $value) {
        $EnvConfig[$key.Trim()] = $value.Trim()
    }
}

# Validate required configuration
$RequiredKeys = @('WORKLOAD_NAME', 'FRONTEND_URL')
foreach ($Key in $RequiredKeys) {
    if (-not $EnvConfig.ContainsKey($Key)) {
        Write-Error "Required configuration '$Key' not found in $EnvDevFile"
        exit 1
    }
}

# Extract the frontend url 
$FrontendBaseUrl = $EnvConfig['FRONTEND_URL']

# Get workspace ID from user or environment variable
if (-not $DevWorkspaceId) {
    $DevWorkspaceId = $env:FABRIC_DEV_WORKSPACE_GUID
}

if (-not $DevWorkspaceId) {
    Write-Host "🏢 Workspace Configuration" -ForegroundColor Yellow
    Write-Host "You need to provide a Fabric workspace GUID for development."
    Write-Host "This should be a real workspace ID within your Fabric tenant."
    Write-Host ""
    $DevWorkspaceId = Read-Host "Enter your development workspace GUID"

    if (-not $DevWorkspaceId) {
        Write-Error "Workspace GUID is required for development setup"
        exit 1
    }
    
    # Ask if user wants to save it as environment variable
    $saveEnvVar = Read-Host "Do you want to save this workspace GUID as an environment variable? (y/n)"
    if ($saveEnvVar -eq 'y' -or $saveEnvVar -eq 'Y') {
        [Environment]::SetEnvironmentVariable("FABRIC_DEV_WORKSPACE_GUID", $DevWorkspaceId, "User")
        Write-Host "✅ Workspace GUID saved as environment variable FABRIC_DEV_WORKSPACE_GUID" -ForegroundColor Green
    }
}

# Validate GUID format
try {
    [System.Guid]::Parse($DevWorkspaceId) | Out-Null
} catch {
    Write-Error "Invalid GUID format for workspace: $DevWorkspaceId"
    exit 1
}


###############################################################################
# Download the DevGateway
###############################################################################
$downloadDevGatewayScript = Join-Path $PSScriptRoot "..\Setup\DownloadDevGateway.ps1"
if (Test-Path $downloadDevGatewayScript) {   
    Write-Host "Running DownloadDevGateway.ps1..."
    & $downloadDevGatewayScript -Force $Force 
} else {
    Write-Error "DownloadDevGateway.ps1 not found at $downloadDevGatewayScript"
    exit 1
}

###############################################################################
# Setup DevGateway configuration files
###############################################################################
# Ensure directories exist
Write-Host "📁 Creating output directories..." -ForegroundColor Blue
if (-not (Test-Path $DevGatewayDir)) {
    New-Item -ItemType Directory -Path $DevGatewayDir -Force | Out-Null
    Write-Host "  ✅ Created: $DevGatewayDir" -ForegroundColor Green
}

if (-not (Test-Path $ManifestDir)) {
    New-Item -ItemType Directory -Path $ManifestDir -Force | Out-Null
    Write-Host "  ✅ Created: $ManifestDir" -ForegroundColor Green
}

# Generate DevGateway configuration
Write-Host "🔧 Generating DevGateway configuration..." -ForegroundColor Blue

$WorkloadName = $EnvConfig['WORKLOAD_NAME']
$WorkloadVersion = $EnvConfig['WORKLOAD_VERSION']
$FrontendUrl = $EnvConfig['FRONTEND_URL']
$ManifestPath = Join-Path $ManifestDir "$WorkloadName.$WorkloadVersion.nupkg"

$DevGatewayConfig = @{
    WorkspaceGuid = $DevWorkspaceId
    ManifestPackageFilePath = $ManifestPath
    WorkloadEndpointURL = $FrontendUrl
}

$DevGatewayFile = Join-Path $DevGatewayDir "workload-dev-mode.json"
$DevGatewayConfig | ConvertTo-Json -Depth 10 | Out-File -FilePath $DevGatewayFile -Encoding UTF8
Write-Host "  ✅ Created: $DevGatewayFile" -ForegroundColor Green

Write-Host ""
Write-Host "🎉 Developer environment setup completed!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Configuration Summary:" -ForegroundColor Yellow
Write-Host "  Development Workspace: $DevWorkspaceId" -ForegroundColor Cyan
Write-Host "  Workload Name: $WorkloadName" -ForegroundColor Cyan
Write-Host "  Frontend URL: $FrontendUrl" -ForegroundColor Cyan
Write-Host ""
Write-Host "📁 Generated files:" -ForegroundColor Yellow
Write-Host "  ✅ $DevGatewayFile" -ForegroundColor Green
if (Test-Path $ManifestPath) {
    Write-Host "  ✅ $ManifestPath" -ForegroundColor Green
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
# Promt user to start the DevServer
$startDevServerScript = Join-Path $PSScriptRoot "..\Run\StartDevServer.ps1"
if (Test-Path $startDevServerScript) {
    $startDevServerScriptFull = (Resolve-Path $startDevServerScript).Path
    Write-Host "  💻 Launch the DevServer locally:" -ForegroundColor Green
    Write-Host "  `"$startDevServerScriptFull`""
} else {
    Write-Host "StartDevServer.ps1 not found at $startDevServerScript" -ForegroundColor Red
}

# Prompt user to run StartDevGateway.ps1 with absolute path
$startDevGatewayScript = Join-Path $PSScriptRoot "..\Run\StartDevGateway.ps1"
if (Test-Path $startDevGatewayScript) {
    $startDevGatewayScriptFull = (Resolve-Path $startDevGatewayScript).Path
    Write-Host ""
    Write-Host "  💻 Start the DevGateway to register your dev instance with Fabric:" -ForegroundColor Green
    Write-Host "  `"$startDevGatewayScriptFull`""
} else {
    Write-Host "StartDevGateway.ps1 not found at $startDevGatewayScript" -ForegroundColor Red
}

Write-Host ""
Write-Host "  💻 Make sure you have enabled the Fabric Developer mode in the Fabric portal." -ForegroundColor Green
Write-Host "  Open https://app.fabric.microsoft.com/ and activate it under Settings > Developer settings > Fabric Developer mode."
Write-Host "  Be aware this setting will not stay on forever. Check back if you have problems if it is still active."
Write-Host ""
Write-Host "After following all the instructions above, you will see your workload being available in the Fabric portal."
Write-Host "It will appear in the Workload Hub and items can be created in the workspace you have configured."

Write-Host "Happy coding! 🚀"