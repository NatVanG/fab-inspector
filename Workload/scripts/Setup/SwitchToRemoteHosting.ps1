<#
.SYNOPSIS
    Switches a FERemote workload to Remote hosting type with backend support

.DESCRIPTION
    This script migrates an existing FERemote workload configuration to Remote hosting type.
    It modifies the package.json, WorkloadManifest.xml, HelloWorldItem.xml, .env.template, and all .env.* files to include:
    - Schema version updates to 2.100.0 for Remote hosting support
    - Backend dependencies (express, jsonwebtoken, jwks-rsa)
    - Backend AAD App configuration
    - Lifecycle operation notifications
    - Backend service endpoint
    - OneLake integration settings
    
    Note: Build scripts (build:test, build:prod) are already present in the base template.
    
    This script is intended for ISV customers implementing extensions for 3rd-party workloads.

.PARAMETER WorkloadRoot
    Path to the Workload folder (defaults to the repository's Workload folder)

.PARAMETER BackendAppId
    Azure AD Application ID for the backend service

.PARAMETER BackendAudience
    Backend audience/resource ID for JWT validation (e.g., api://localdevinstance/{TenantId}/{WorkloadName}/123)

.PARAMETER BackendUrl
    URL of the backend service endpoint

.PARAMETER TenantId
    Azure AD Tenant ID

.PARAMETER EnableOneLakeLogging
    Enable OneLake logging for item operations (true/false)

.PARAMETER BackendClientSecret
    Backend client secret (required if EnableOneLakeLogging is true)

.PARAMETER Force
    Skip confirmation prompts

.EXAMPLE
    .\SwitchToRemoteHosting.ps1
    
.EXAMPLE  
    .\SwitchToRemoteHosting.ps1 -BackendAppId "12345678-1234-1234-1234-123456789012" -BackendAudience "api://myworkload/123" -Force

.NOTES
    This script modifies:
    - Workload/package.json
    - Workload/Manifest/WorkloadManifest.xml (schema version + Remote elements)
    - Workload/Manifest/items/HelloWorldItem/HelloWorldItem.xml (schema version)
    - Workload/.env.template
    - Workload/.env.dev
    - Workload/.env.test
    
    After running, execute 'npm install' to update dependencies.
    To revert changes, use: git checkout main <files> && npm install
    
    Also creates:
    - Workload/devServer/web.config (IIS/Azure Web App configuration)
#>

param (
    [string]$WorkloadRoot = "",
    [string]$BackendAppId = "",
    [string]$BackendAudience = "",
    [string]$BackendUrl = "",
    [string]$TenantId = "",
    [string]$EnableOneLakeLogging = "",
    [string]$BackendClientSecret = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Determine the workload root path
if ([string]::IsNullOrWhiteSpace($WorkloadRoot)) {
    $scriptRoot = Split-Path -Parent $PSCommandPath
    $WorkloadRoot = Join-Path (Split-Path (Split-Path $scriptRoot -Parent) -Parent) "Workload"
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Switch to Remote Hosting Configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if already configured for Remote hosting
$envDevPath = Join-Path $WorkloadRoot ".env.dev"
$envTestPath = Join-Path $WorkloadRoot ".env.test"
$envTemplatePath = Join-Path $WorkloadRoot ".env.template"

$alreadyConfigured = $false
if (Test-Path $envDevPath) {
    $envContent = Get-Content $envDevPath -Raw
    if ($envContent -match "WORKLOAD_HOSTING_TYPE=Remote") {
        $alreadyConfigured = $true
    }
}

if ($alreadyConfigured) {
    Write-Host "⚠️  WARNING: Remote hosting appears to be already configured!" -ForegroundColor Yellow
    Write-Host "   Current WORKLOAD_HOSTING_TYPE is set to 'Remote'" -ForegroundColor Yellow
    Write-Host ""
    
    if (-not $Force) {
        $confirmation = Read-Host "Do you want to proceed and reconfigure? (y/N)"
        if ($confirmation -ne "y" -and $confirmation -ne "Y") {
            Write-Host "Operation cancelled." -ForegroundColor Yellow
            exit 0
        }
    }
}

Write-Host "This script will modify the following files:" -ForegroundColor Yellow
Write-Host "  • Workload/package.json" -ForegroundColor White
Write-Host "  • Workload/Manifest/WorkloadManifest.xml" -ForegroundColor White
Write-Host "  • Workload/Manifest/items/HelloWorldItem/HelloWorldItem.xml" -ForegroundColor White
Write-Host "  • Workload/.env.template" -ForegroundColor White
Write-Host "  • Workload/.env.dev" -ForegroundColor White
Write-Host "  • Workload/.env.test" -ForegroundColor White
Write-Host ""
Write-Host "To revert these changes, use:" -ForegroundColor Cyan
Write-Host "  git checkout main Workload/package.json Workload/Manifest/WorkloadManifest.xml Workload/Manifest/items/HelloWorldItem/HelloWorldItem.xml Workload/.env.template Workload/.env.dev Workload/.env.test" -ForegroundColor Gray
Write-Host "  npm install  # Restore original dependencies" -ForegroundColor Gray
Write-Host ""

if (-not $Force) {
    $confirmation = Read-Host "Do you want to continue? (y/N)"
    if ($confirmation -ne "y" -and $confirmation -ne "Y") {
        Write-Host "Operation cancelled." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Backend Configuration" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Read Frontend AppId and WorkloadName from .env.dev if it exists
$FrontendAppIdFromEnv = ""
$WorkloadNameFromEnv = ""
if (Test-Path $envDevPath) {
    $envDevContent = Get-Content $envDevPath -Raw
    if ($envDevContent -match "FRONTEND_APPID=([^\r\n]+)") {
        $FrontendAppIdFromEnv = $Matches[1]
    }
    if ($envDevContent -match "WORKLOAD_NAME=([^\r\n]+)") {
        $WorkloadNameFromEnv = $Matches[1]
    }
}

# Collect Backend Configuration
if ([string]::IsNullOrWhiteSpace($BackendAppId)) {
    Write-Host "Enter the Azure AD Application ID for the backend service:" -ForegroundColor Green
    if (-not [string]::IsNullOrWhiteSpace($FrontendAppIdFromEnv)) {
        Write-Host "  Press Enter to use the same as frontend: $FrontendAppIdFromEnv" -ForegroundColor Gray
    } else {
        Write-Host "  (This can be the same as the frontend AppId or a separate app)" -ForegroundColor Gray
    }
    $BackendAppIdInput = Read-Host "Backend AppId"
    
    # Use frontend AppId if user pressed Enter
    if ([string]::IsNullOrWhiteSpace($BackendAppIdInput) -and -not [string]::IsNullOrWhiteSpace($FrontendAppIdFromEnv)) {
        $BackendAppId = $FrontendAppIdFromEnv
        Write-Host "  Using frontend AppId: $BackendAppId" -ForegroundColor Gray
    } else {
        $BackendAppId = $BackendAppIdInput
    }
}

if ([string]::IsNullOrWhiteSpace($TenantId)) {
    Write-Host ""
    Write-Host "Enter your Azure AD Tenant ID:" -ForegroundColor Green
    $TenantId = Read-Host "Tenant ID"
}

if ([string]::IsNullOrWhiteSpace($BackendAudience)) {
    Write-Host ""
    Write-Host "Enter the backend audience/resource ID for JWT validation:" -ForegroundColor Green
    
    # Build default audience if we have TenantId and WorkloadName
    $defaultAudience = ""
    if (-not [string]::IsNullOrWhiteSpace($TenantId) -and -not [string]::IsNullOrWhiteSpace($WorkloadNameFromEnv)) {
        $defaultAudience = "api://localdevinstance/$TenantId/$WorkloadNameFromEnv/123"
        Write-Host "  Press Enter to use default: $defaultAudience" -ForegroundColor Gray
    } else {
        Write-Host "  Example format: api://localdevinstance/{TenantId}/{WorkloadName}/123" -ForegroundColor Gray
        Write-Host "  Or: https://yourdomain.com/workload-name/123" -ForegroundColor Gray
    }
    
    $BackendAudienceInput = Read-Host "Backend Audience"
    
    # Use default if user pressed Enter and we have a default
    if ([string]::IsNullOrWhiteSpace($BackendAudienceInput) -and -not [string]::IsNullOrWhiteSpace($defaultAudience)) {
        $BackendAudience = $defaultAudience
        Write-Host "  Using default: $BackendAudience" -ForegroundColor Gray
    } else {
        $BackendAudience = $BackendAudienceInput
    }
}

if ([string]::IsNullOrWhiteSpace($BackendUrl)) {
    Write-Host ""
    Write-Host "Enter the backend service URL:" -ForegroundColor Green
    Write-Host "  Press Enter to use default: http://localhost:60006/workload/" -ForegroundColor Gray
    $BackendUrlInput = Read-Host "Backend URL"
    
    # Use default if user pressed Enter
    if ([string]::IsNullOrWhiteSpace($BackendUrlInput)) {
        $BackendUrl = "http://localhost:60006/workload/"
        Write-Host "  Using default: $BackendUrl" -ForegroundColor Gray
    } else {
        $BackendUrl = $BackendUrlInput
    }
}

# OneLake Configuration (Optional)
if ([string]::IsNullOrWhiteSpace($EnableOneLakeLogging)) {
    Write-Host ""
    Write-Host "Enable OneLake logging for item operations? (y/N)" -ForegroundColor Green
    Write-Host "  This requires a client secret for token exchange (OBO flow)" -ForegroundColor Gray
    $oneLakeChoice = Read-Host "Enable OneLake"
    $EnableOneLakeLogging = if ($oneLakeChoice -eq "y" -or $oneLakeChoice -eq "Y") { "true" } else { "false" }
}

if ($EnableOneLakeLogging -eq "true" -and [string]::IsNullOrWhiteSpace($BackendClientSecret)) {
    Write-Host ""
    Write-Host "Enter the backend client secret for OneLake token exchange:" -ForegroundColor Green
    Write-Host "  (This will be stored in .env files - ensure proper security)" -ForegroundColor Yellow
    $BackendClientSecretSecure = Read-Host "Backend Client Secret" -AsSecureString
    $BackendClientSecretPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($BackendClientSecretSecure))
} else {
    $BackendClientSecretPlain = $BackendClientSecret
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Applying Configuration Changes" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Update WorkloadManifest.xml
Write-Host "Updating WorkloadManifest.xml..." -ForegroundColor Green
$manifestPath = Join-Path $WorkloadRoot "Manifest\WorkloadManifest.xml"

if (-not (Test-Path $manifestPath)) {
    Write-Host "  ❌ Error: WorkloadManifest.xml not found at: $manifestPath" -ForegroundColor Red
    exit 1
}

$manifestContent = Get-Content $manifestPath -Raw

# Update schema version to 2.100.0 for Remote hosting
if ($manifestContent -match 'SchemaVersion="2\.0\.0"') {
    $manifestContent = $manifestContent -replace 'SchemaVersion="2\.0\.0"', 'SchemaVersion="2.100.0"'
    Write-Host "  ✓ Updated schema version to 2.100.0" -ForegroundColor Gray
} elseif ($manifestContent -match 'SchemaVersion="2\.100\.0"') {
    Write-Host "  ⚠️  Schema version already set to 2.100.0" -ForegroundColor Yellow
}

# Update HostingType from FERemote to Remote
if ($manifestContent -match 'HostingType="FERemote"') {
    $manifestContent = $manifestContent -replace 'HostingType="FERemote"', 'HostingType="Remote"'
    Write-Host "  ✓ Updated HostingType to Remote" -ForegroundColor Gray
} elseif ($manifestContent -match 'HostingType="Remote"') {
    Write-Host "  ⚠️  HostingType already set to Remote" -ForegroundColor Yellow
}

# Insert AADBEApp element after AADFEApp
$aadBeAppXml = @"
        <AADBEApp>
          <AppId>{{BACKEND_APPID}}</AppId>
          <RedirectUri>http://localhost:60006/close</RedirectUri>
          <ResourceId>{{BACKEND_AUDIENCE}}</ResourceId>
        </AADBEApp>
"@

if ($manifestContent -notmatch "AADBEApp") {
    $manifestContent = $manifestContent -replace "(</AADFEApp>)", "`$1`r`n$aadBeAppXml"
    Write-Host "  ✓ Added AADBEApp configuration" -ForegroundColor Gray
} else {
    Write-Host "  ⚠️  AADBEApp already exists in manifest" -ForegroundColor Yellow
}

# Insert LifecycleOperationsNotifications after AAD configuration
$lifecycleXml = @"
        <LifecycleOperationsNotifications>
          <OnCreate>true</OnCreate>
          <OnUpdate>true</OnUpdate>
          <OnDelete>true</OnDelete>
        </LifecycleOperationsNotifications>
"@

if ($manifestContent -notmatch "LifecycleOperationsNotifications") {
    $manifestContent = $manifestContent -replace "(<EnableSandboxRelaxation>)", "$lifecycleXml`r`n        `$1"
    Write-Host "  ✓ Added LifecycleOperationsNotifications" -ForegroundColor Gray
} else {
    Write-Host "  ⚠️  LifecycleOperationsNotifications already exists in manifest" -ForegroundColor Yellow
}

# Insert Backend ServiceEndpoint
$backendEndpointXml = @"
          <ServiceEndpoint>
            <Name>Workload</Name>
            <Url>{{BACKEND_URL}}</Url>
            <IsEndpointResolutionService>false</IsEndpointResolutionService>
          </ServiceEndpoint>
"@

if ($manifestContent -notmatch "<Name>Workload</Name>") {
    $manifestContent = $manifestContent -replace "(</Endpoints>)", "$backendEndpointXml`r`n        `$1"
    Write-Host "  ✓ Added backend ServiceEndpoint" -ForegroundColor Gray
} else {
    Write-Host "  ⚠️  Backend ServiceEndpoint already exists in manifest" -ForegroundColor Yellow
}

Set-Content -Path $manifestPath -Value $manifestContent -NoNewline
Write-Host "  ✓ WorkloadManifest.xml updated successfully" -ForegroundColor Green
Write-Host ""

# Update HelloWorldItem.xml schema version
Write-Host "Updating HelloWorldItem.xml..." -ForegroundColor Green
$helloWorldItemPath = Join-Path $WorkloadRoot "Manifest\items\HelloWorldItem\HelloWorldItem.xml"

if (Test-Path $helloWorldItemPath) {
    $helloWorldContent = Get-Content $helloWorldItemPath -Raw
    
    if ($helloWorldContent -match 'SchemaVersion="2\.0\.0"') {
        $helloWorldContent = $helloWorldContent -replace 'SchemaVersion="2\.0\.0"', 'SchemaVersion="2.100.0"'
        Set-Content -Path $helloWorldItemPath -Value $helloWorldContent -NoNewline
        Write-Host "  ✓ Updated HelloWorldItem.xml schema version to 2.100.0" -ForegroundColor Gray
    } elseif ($helloWorldContent -match 'SchemaVersion="2\.100\.0"') {
        Write-Host "  ⚠️  HelloWorldItem.xml schema version already set to 2.100.0" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ⚠️  HelloWorldItem.xml not found at: $helloWorldItemPath" -ForegroundColor Yellow
}
Write-Host ""

# 2. Update package.json
Write-Host "Updating package.json..." -ForegroundColor Green
$packageJsonPath = Join-Path $WorkloadRoot "package.json"

if (-not (Test-Path $packageJsonPath)) {
    Write-Host "  ❌ Error: package.json not found at: $packageJsonPath" -ForegroundColor Red
    exit 1
}

$packageJson = Get-Content $packageJsonPath -Raw | ConvertFrom-Json

# Add build scripts for Remote hosting
if (-not $packageJson.scripts.'build:test') {
    $packageJson.scripts | Add-Member -NotePropertyName 'build:test' -NotePropertyValue 'env-cmd -f .env.test webpack --config ./webpack.config.js --output-path ../build/Frontend --progress' -Force
    Write-Host "  ✓ Added build:test script" -ForegroundColor Gray
} else {
    Write-Host "  ⚠️  build:test script already exists" -ForegroundColor Yellow
}

if (-not $packageJson.scripts.'build:prod') {
    $packageJson.scripts | Add-Member -NotePropertyName 'build:prod' -NotePropertyValue 'env-cmd -f .env.prod webpack --config ./webpack.config.js --output-path ../build/Frontend --mode production --progress' -Force
    Write-Host "  ✓ Added build:prod script" -ForegroundColor Gray
} else {
    Write-Host "  ⚠️  build:prod script already exists" -ForegroundColor Yellow
}

# Move express from devDependencies to dependencies (needed for backend)
if ($packageJson.devDependencies.express) {
    $expressVersion = $packageJson.devDependencies.express
    $packageJson.PSObject.Properties.Remove('express')
    $packageJson.dependencies | Add-Member -NotePropertyName 'express' -NotePropertyValue $expressVersion -Force
    Write-Host "  ✓ Moved express to dependencies" -ForegroundColor Gray
} elseif (-not $packageJson.dependencies.express) {
    $packageJson.dependencies | Add-Member -NotePropertyName 'express' -NotePropertyValue '^4.18.2' -Force
    Write-Host "  ✓ Added express to dependencies" -ForegroundColor Gray
}

# Add JWT dependencies to devDependencies (for backend development)
$jwtDeps = @{
    'jsonwebtoken' = '^9.0.3'
    'jwks-rsa' = '^3.2.0'
}

foreach ($dep in $jwtDeps.GetEnumerator()) {
    if (-not $packageJson.devDependencies.($dep.Key)) {
        $packageJson.devDependencies | Add-Member -NotePropertyName $dep.Key -NotePropertyValue $dep.Value -Force
        Write-Host "  ✓ Added $($dep.Key) to devDependencies" -ForegroundColor Gray
    }
}

# Update @types/node to v20 for better compatibility
if ($packageJson.devDependencies.'@types/node' -match '^\^16') {
    $packageJson.devDependencies.'@types/node' = '^20.0.0'
    Write-Host "  ✓ Updated @types/node to ^20.0.0" -ForegroundColor Gray
}

# Save package.json with proper formatting
$packageJsonContent = ConvertTo-Json $packageJson -Depth 10
Set-Content -Path $packageJsonPath -Value $packageJsonContent
Write-Host "  ✓ package.json updated successfully" -ForegroundColor Green
Write-Host ""

# 3. Copy Remote hosting files to devServer
Write-Host "Setting up Remote hosting backend files..." -ForegroundColor Green
$remoteSourcePath = Join-Path $PSScriptRoot "remote"
$remoteDestPath = Join-Path $WorkloadRoot "devServer\remote"

if (Test-Path $remoteSourcePath) {
    $remoteHasContent = (Test-Path $remoteDestPath) -and @(Get-ChildItem $remoteDestPath -File).Count -gt 0
    if ($remoteHasContent) {
        Write-Host "  ⚠️  Remote folder already exists in devServer with files, skipping copy" -ForegroundColor Yellow
    } else {
        New-Item -ItemType Directory -Path $remoteDestPath -Force | Out-Null
        Copy-Item -Path "$remoteSourcePath\*" -Destination $remoteDestPath -Recurse -Force
        Write-Host "  ✓ Copied remote hosting files to Workload\devServer\remote\" -ForegroundColor Gray
    }
} else {
    Write-Host "  ⚠️  Remote hosting files not found at: $remoteSourcePath" -ForegroundColor Yellow
    Write-Host "      Backend API implementations will need to be added manually" -ForegroundColor Yellow
}
# 3b. Create web.config for IIS/Azure Web App (Remote hosting only)
$webConfigPath = Join-Path $WorkloadRoot "devServer\web.config"
if (-not (Test-Path $webConfigPath)) {
    $webConfigContent = @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <system.webServer>
    <!-- IIS Node.js handler -->
    <handlers>
      <add name="iisnode" path="index.js" verb="*" modules="iisnode" />
    </handlers>
    
    <!-- Rewrite all requests to Node.js -->
    <rewrite>
      <rules>
        <rule name="DynamicContent">
          <match url="/*" />
          <action type="Rewrite" url="index.js" />
        </rule>
      </rules>
    </rewrite>
    
    <!-- Security: Don't expose Node.js files -->
    <security>
      <requestFiltering>
        <hiddenSegments>
          <add segment="node_modules" />
        </hiddenSegments>
      </requestFiltering>
    </security>
    
    <!-- IIS Node.js configuration -->
    <iisnode 
      nodeProcessCommandLine="node"
      watchedFiles="*.js;iisnode.yml"
      loggingEnabled="true"
      devErrorsEnabled="true"
    />
    
    <!-- Enable CORS for development -->
    <httpProtocol>
      <customHeaders>
        <add name="Access-Control-Allow-Origin" value="*" />
        <add name="Access-Control-Allow-Methods" value="GET, POST, PUT, DELETE, PATCH, OPTIONS" />
        <add name="Access-Control-Allow-Headers" value="Content-Type, Authorization" />
      </customHeaders>
    </httpProtocol>
  </system.webServer>
</configuration>
'@
    Set-Content -Path $webConfigPath -Value $webConfigContent -Encoding UTF8
    Write-Host "  ✓ Created web.config for IIS/Azure Web App" -ForegroundColor Gray
} else {
    Write-Host "  ⚠️  web.config already exists, skipping" -ForegroundColor Yellow
}
Write-Host ""

# 4. Update webpack.dev.js for Remote hosting
Write-Host "Updating webpack.dev.js..." -ForegroundColor Green
$webpackDevPath = Join-Path $WorkloadRoot "devServer\webpack.dev.js"

if (Test-Path $webpackDevPath) {
    $webpackContent = Get-Content $webpackDevPath -Raw
    
    # Check if already updated (look for registerCrudApis import)
    if ($webpackContent -notmatch "registerCrudApis") {
        # Create the complete Remote hosting webpack.dev.js content
        $newWebpackContent = @"
const { merge } = require('webpack-merge');
const baseConfig = require('../webpack.config.js');
const express = require("express");
const Webpack = require("webpack");
const { registerDevServerApis, registerDevServerComponents } = require('.');
const { registerCrudApis, registerJobsApis, registerEndpointResolutionApi, registerPermissionsApi } = require('./remote');

// making sure the dev configuration is set correctly!
process.env.DEV_AAD_CONFIG_FE_APPID = process.env.FRONTEND_APPID;
process.env.DEV_AAD_CONFIG_BE_APPID = process.env.BACKEND_APPID;
process.env.DEV_AAD_CONFIG_BE_AUDIENCE = process.env.BACKEND_AUDIENCE;

console.log('********************   Development Configuration   *******************');
console.log('process.env.DEV_AAD_CONFIG_FE_APPID: ' + process.env.DEV_AAD_CONFIG_FE_APPID);
console.log('process.env.DEV_AAD_CONFIG_BE_APPID: ' + process.env.DEV_AAD_CONFIG_BE_APPID);
console.log('process.env.DEV_AAD_CONFIG_BE_AUDIENCE: ' + process.env.DEV_AAD_CONFIG_BE_AUDIENCE);
console.log('*********************************************************************');

const sharedPlugins = [
    new Webpack.DefinePlugin({
        "process.env.DEV_AAD_CONFIG_FE_APPID": JSON.stringify(process.env.DEV_AAD_CONFIG_FE_APPID),
        "process.env.DEV_AAD_CONFIG_BE_APPID": JSON.stringify(process.env.DEV_AAD_CONFIG_BE_APPID),
        "process.env.DEV_AAD_CONFIG_BE_AUDIENCE": JSON.stringify(process.env.DEV_AAD_CONFIG_BE_AUDIENCE)
    }),
];

const sharedHeaders = {
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "GET, PUT, POST, DELETE, PATCH, OPTIONS",
    "Access-Control-Allow-Headers": "*",
    "Connection": "close"
};

// Single unified server on port 60006 (Frontend + All APIs)
const config = merge(baseConfig, {
    mode: "development",
    devtool: "eval",
    cache: {
        type: 'filesystem',
        maxMemoryGenerations: 3,
        allowCollectingMemory: true,
    },
    plugins: sharedPlugins,
    devServer: {
        port: 60006,
        host: '127.0.0.1',
        open: false,
        historyApiFallback: true,
        headers: sharedHeaders,
        setupMiddlewares: function (middlewares, devServer) {
            console.log('*********************************************************************');
            console.log('****           DevServer is listening on port 60006               ****');
            console.log('****     Serving Frontend + All Workload APIs (Unified)          ****');
            console.log('*********************************************************************');

            // Memory monitoring setup
            let memoryCheckInterval;
            const startMemoryMonitoring = () => {
                const logMemoryUsage = () => {
                    const usage = process.memoryUsage();
                    const heapStats = require('v8').getHeapStatistics();
                    console.log('📊 Memory Usage - Heap: ' + Math.round(usage.heapUsed/1024/1024) + 'MB/' + Math.round(heapStats.heap_size_limit/1024/1024) + 'MB | RSS: ' + Math.round(usage.rss/1024/1024) + 'MB');
                };
                
                // Log memory every 30 seconds
                memoryCheckInterval = setInterval(logMemoryUsage, 30000);
                logMemoryUsage(); // Initial log
                
                // Cleanup on process exit
                process.on('SIGINT', () => {
                    if (memoryCheckInterval) clearInterval(memoryCheckInterval);
                });
            };
            
            // Enable memory monitoring based on environment variable
            if (process.env.ENABLE_MEMORY_MONITORING === 'true') {
                console.log('📊 Memory monitoring enabled via ENABLE_MEMORY_MONITORING=true');
                startMemoryMonitoring();
            } else {
                console.log('📊 Memory monitoring disabled. Set ENABLE_MEMORY_MONITORING=true to enable');
            }

            // Add JSON body parsing middleware for our APIs
            devServer.app.use(express.json());

            devServer.app.use((req, res, next) => {
                res.header('Access-Control-Allow-Origin', '*');
                res.header('Access-Control-Allow-Methods', 'GET, PUT, POST, DELETE, PATCH, OPTIONS');
                res.header('Access-Control-Allow-Headers', 'Content-Type, Authorization, Content-Length, X-Requested-With, ActivityId, RequestId, x-ms-client-tenant-id');
                res.header('Connection', 'close');

                if (req.method === 'OPTIONS') {
                    res.sendStatus(204);
                } else {
                    next();
                }
            });

            // Register Manifest APIs
            registerDevServerApis(devServer.app);

            // Register Workload Item Lifecycle APIs
            registerCrudApis(devServer.app);

            // Register Workload Jobs APIs
            registerJobsApis(devServer.app);

            // Register Endpoint Resolution API
            registerEndpointResolutionApi(devServer.app);
 
            // Register Permissions API
            registerPermissionsApi(devServer.app);

            // Register Dev Server Components
            registerDevServerComponents();

            console.log('📋 Available API endpoints:');
            console.log('   Manifest APIs:');
            console.log('   - GET    /manifests_new');
            console.log('   - GET    /manifests_new/metadata');
            console.log('');
            console.log('   Item Lifecycle APIs:');
            console.log('   - POST   /workspaces/{workspaceId}/items/{itemType}/{itemId}');
            console.log('   - PATCH  /workspaces/{workspaceId}/items/{itemType}/{itemId}');
            console.log('   - DELETE /workspaces/{workspaceId}/items/{itemType}/{itemId}');
            console.log('   - GET    /workspaces/{workspaceId}/items/{itemType}/{itemId}/payload');
            console.log('');
            console.log('   Jobs APIs:');
            console.log('   - POST   /workspaces/{workspaceId}/items/{itemType}/{itemId}/jobs/onCreateJobInstance');
            console.log('   - POST   /workspaces/{workspaceId}/items/{itemType}/{itemId}/jobs/onGetJobInstanceStatus');
            console.log('   - POST   /workspaces/{workspaceId}/items/{itemType}/{itemId}/jobs/onCancelJobInstance');
            console.log('');
            console.log('   Permissions API:');
            console.log('   - GET    /workspaces/{workspaceId}/items/{itemId}/permissions');
            console.log('');
            console.log('   Endpoint Resolution API:');
            console.log('   - POST   /resolve-endpoint');
            console.log('*********************************************************************');

            return middlewares;
        },
    }
});

module.exports = config;
"@
        
        Set-Content -Path $webpackDevPath -Value $newWebpackContent -NoNewline
        Write-Host "  ✓ webpack.dev.js updated for Remote hosting" -ForegroundColor Gray
    } else {
        Write-Host "  ⚠️  webpack.dev.js already configured for Remote hosting" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ⚠️  webpack.dev.js not found at: $webpackDevPath" -ForegroundColor Yellow
}
Write-Host ""

# 5. Update .env.template
Write-Host "Updating .env.template..." -ForegroundColor Green
$envTemplatePath = Join-Path $WorkloadRoot ".env.template"

if (-not (Test-Path $envTemplatePath)) {
    Write-Host "  ❌ Error: .env.template not found at: $envTemplatePath" -ForegroundColor Red
    exit 1
}

$envTemplateContent = Get-Content $envTemplatePath -Raw

# Check if Remote variables already exist
if ($envTemplateContent -notmatch "BACKEND_APPID") {
    $remoteVariables = @"


##########################################################
# Remote Hosting Configuration
##########################################################
# Backend AAD configuration
BACKEND_APPID={{BACKEND_APPID}}
BACKEND_AUDIENCE={{BACKEND_AUDIENCE}}
BACKEND_URL={{BACKEND_URL}}
TENANT_ID={{TENANT_ID}}

# Fabric Authentication Configuration
FABRIC_CLIENT_FOR_WORKLOADS_APP_ID=d2450708-699c-41e3-8077-b0c8341509aa
FABRIC_BACKEND_RESOURCEID=https://analysis.windows.net/powerbi/api
FABRIC_WORKLOAD_CONTROL_URL=https://api.fabric.microsoft.com

# OneLake Integration
ENABLE_ONELAKE_LOGGING={{ENABLE_ONELAKE_LOGGING}}
BACKEND_CLIENT_SECRET={{BACKEND_CLIENT_SECRET}}
ONELAKE_DFS_BASE_URL=https://onelake.dfs.fabric.microsoft.com
"@
    
    $envTemplateContent += $remoteVariables
    Write-Host "  ✓ Added Remote hosting variables" -ForegroundColor Gray
} else {
    Write-Host "  ⚠️  Remote hosting variables already exist in .env.template" -ForegroundColor Yellow
}

# Update WORKLOAD_HOSTING_TYPE
$envTemplateContent = $envTemplateContent -replace "WORKLOAD_HOSTING_TYPE=FERemote", "WORKLOAD_HOSTING_TYPE=Remote"

Set-Content -Path $envTemplatePath -Value $envTemplateContent -NoNewline
Write-Host "  ✓ .env.template updated successfully" -ForegroundColor Green
Write-Host ""

# 6. Update .env.dev and .env.test
$envFiles = @($envDevPath, $envTestPath)

foreach ($envFile in $envFiles) {
    if (Test-Path $envFile) {
        $fileName = Split-Path $envFile -Leaf
        Write-Host "Updating $fileName..." -ForegroundColor Green
        
        $envContent = Get-Content $envFile -Raw
        
        # Update WORKLOAD_HOSTING_TYPE
        $envContent = $envContent -replace "WORKLOAD_HOSTING_TYPE=FERemote", "WORKLOAD_HOSTING_TYPE=Remote"
        
        # Add Remote variables if not present
        if ($envContent -notmatch "BACKEND_APPID=") {
            $remoteVariables = @"


##########################################################
# Remote Hosting Configuration
##########################################################
# Backend AAD configuration
BACKEND_APPID=$BackendAppId
BACKEND_AUDIENCE=$BackendAudience
BACKEND_URL=$BackendUrl
TENANT_ID=$TenantId

# Fabric Authentication Configuration
FABRIC_CLIENT_FOR_WORKLOADS_APP_ID=d2450708-699c-41e3-8077-b0c8341509aa
FABRIC_BACKEND_RESOURCEID=https://analysis.windows.net/powerbi/api
FABRIC_WORKLOAD_CONTROL_URL=https://api.fabric.microsoft.com

# OneLake Integration
ENABLE_ONELAKE_LOGGING=$EnableOneLakeLogging
BACKEND_CLIENT_SECRET=$BackendClientSecretPlain
ONELAKE_DFS_BASE_URL=https://onelake.dfs.fabric.microsoft.com
"@
            
            $envContent += $remoteVariables
            Write-Host "  ✓ Added Remote hosting variables" -ForegroundColor Gray
        } else {
            # Update existing values or add missing ones
            Write-Host "  ✓ Updating Remote hosting variables" -ForegroundColor Gray
            
            # BACKEND_APPID
            if ($envContent -match "BACKEND_APPID=") {
                $envContent = $envContent -replace "BACKEND_APPID=.*", "BACKEND_APPID=$BackendAppId"
            } else {
                $envContent += "`r`nBACKEND_APPID=$BackendAppId"
            }
            
            # BACKEND_AUDIENCE
            if ($envContent -match "BACKEND_AUDIENCE=") {
                $envContent = $envContent -replace "BACKEND_AUDIENCE=.*", "BACKEND_AUDIENCE=$BackendAudience"
            } else {
                $envContent += "`r`nBACKEND_AUDIENCE=$BackendAudience"
            }
            
            # BACKEND_URL
            if ($envContent -match "BACKEND_URL=") {
                $envContent = $envContent -replace "BACKEND_URL=.*", "BACKEND_URL=$BackendUrl"
            } else {
                $envContent += "`r`nBACKEND_URL=$BackendUrl"
            }
            
            # TENANT_ID
            if ($envContent -match "TENANT_ID=") {
                $envContent = $envContent -replace "TENANT_ID=.*", "TENANT_ID=$TenantId"
            } else {
                $envContent += "`r`nTENANT_ID=$TenantId"
            }
            
            # FABRIC constants (add if not present)
            if ($envContent -notmatch "FABRIC_CLIENT_FOR_WORKLOADS_APP_ID=") {
                $envContent += "`r`nFABRIC_CLIENT_FOR_WORKLOADS_APP_ID=d2450708-699c-41e3-8077-b0c8341509aa"
            }
            if ($envContent -notmatch "FABRIC_BACKEND_RESOURCEID=") {
                $envContent += "`r`nFABRIC_BACKEND_RESOURCEID=https://analysis.windows.net/powerbi/api"
            }
            if ($envContent -notmatch "FABRIC_WORKLOAD_CONTROL_URL=") {
                $envContent += "`r`nFABRIC_WORKLOAD_CONTROL_URL=https://api.fabric.microsoft.com"
            }
            
            # ENABLE_ONELAKE_LOGGING
            if ($envContent -match "ENABLE_ONELAKE_LOGGING=") {
                $envContent = $envContent -replace "ENABLE_ONELAKE_LOGGING=.*", "ENABLE_ONELAKE_LOGGING=$EnableOneLakeLogging"
            } else {
                $envContent += "`r`nENABLE_ONELAKE_LOGGING=$EnableOneLakeLogging"
            }
            
            # BACKEND_CLIENT_SECRET
            if (-not [string]::IsNullOrWhiteSpace($BackendClientSecretPlain)) {
                if ($envContent -match "BACKEND_CLIENT_SECRET=") {
                    $envContent = $envContent -replace "BACKEND_CLIENT_SECRET=.*", "BACKEND_CLIENT_SECRET=$BackendClientSecretPlain"
                } else {
                    $envContent += "`r`nBACKEND_CLIENT_SECRET=$BackendClientSecretPlain"
                }
            }
            
            # ONELAKE_DFS_BASE_URL
            if ($envContent -notmatch "ONELAKE_DFS_BASE_URL=") {
                $envContent += "`r`nONELAKE_DFS_BASE_URL=https://onelake.dfs.fabric.microsoft.com"
            }
        }
        
        Set-Content -Path $envFile -Value $envContent -NoNewline
        Write-Host "  ✓ $fileName updated successfully" -ForegroundColor Green
        Write-Host ""
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "✓ Configuration Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Your workload has been switched to Remote hosting type." -ForegroundColor White
Write-Host ""
Write-Host "📋 NEXT STEPS:" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Install updated npm dependencies:" -ForegroundColor Yellow
Write-Host "   cd Workload" -ForegroundColor Gray
Write-Host "   npm install" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Verify your environment files:" -ForegroundColor Yellow
Write-Host "   • Workload\.env.dev" -ForegroundColor Gray
Write-Host "   • Workload\.env.test" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Update your Azure AD app registration:" -ForegroundColor Yellow
Write-Host "   • Expose API scopes (FabricWorkloadControl, etc.)" -ForegroundColor Gray
Write-Host "   • Add pre-authorized applications for OBO flow" -ForegroundColor Gray
Write-Host "   • Configure redirect URIs for backend (http://localhost:60006/close)" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Implement backend endpoints (if not already done):" -ForegroundColor Yellow
Write-Host "   • Item lifecycle operations (CREATE/UPDATE/DELETE)" -ForegroundColor Gray
Write-Host "   • Job instance notifications" -ForegroundColor Gray
Write-Host "   • JWT token validation" -ForegroundColor Gray
Write-Host "   Reference: Workload\devServer\remote\*.js" -ForegroundColor Gray
Write-Host ""
Write-Host "5. Start the dev server (manifest will be rebuilt automatically):" -ForegroundColor Yellow
Write-Host "   cd scripts\Run" -ForegroundColor Gray
Write-Host "   .\StartDevGateway.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "📖 For more details, see the Fabric Extensibility Toolkit documentation." -ForegroundColor Cyan
Write-Host ""
Write-Host "⚠️  To revert these changes:" -ForegroundColor Yellow
Write-Host "   git checkout main Workload\Manifest\WorkloadManifest.xml Workload\Manifest\items\HelloWorldItem\HelloWorldItem.xml Workload\package.json Workload\.env.template Workload\.env.dev Workload\.env.test" -ForegroundColor Gray
Write-Host ""
