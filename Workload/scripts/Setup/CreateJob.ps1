<#
.SYNOPSIS
    Enables job scheduling support for the HelloWorld item

.DESCRIPTION
    This script adds job-related configuration to the HelloWorld item:
    - Copies jobUtils.ts and JobSchedulerController.ts to Workload
    - Uncomments job-related code in index.worker.ts
    - Updates HelloWorldItem.json with itemJobActionConfig and itemSettings
    - Adds a "Run Job" context menu item to HelloWorldItem.json
    - Updates HelloWorldItem.xml with JobScheduler configuration
    - Adds translation key for context menu to manifest translations.json
    - Adds job UI translation keys to app translation.json
    - Copies execute.png icon to assets/images/

.PARAMETER ItemName
    The name of the item to enable jobs for (default: HelloWorld)

.PARAMETER JobName
    The name of the job type (default: RunJob)
    This will be used in the job type name: {{WORKLOAD_NAME}}.{ItemName}.{JobName}

.EXAMPLE
    .\CreateJob.ps1
    
.EXAMPLE
    .\CreateJob.ps1 -ItemName "HelloWorld" -JobName "ProcessData"

.NOTES
    Run this script from the scripts/Setup directory
#>

param (
    [String]$ItemName = "HelloWorld",
    [String]$JobName = "RunJob"
)

$ScriptRoot = $PSScriptRoot
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptRoot)

###############################################################################
# Check if job support is already enabled
###############################################################################
$jsonFile = Join-Path $ProjectRoot "Workload\Manifest\items\${ItemName}Item\${ItemName}Item.json"
$xmlFile = Join-Path $ProjectRoot "Workload\Manifest\items\${ItemName}Item\${ItemName}Item.xml"

$alreadyEnabled = $true
$specificJobType = "{{WORKLOAD_NAME}}.${ItemName}.${JobName}"

# Check JSON for the specific job type
if (Test-Path $jsonFile) {
    $jsonContent = Get-Content $jsonFile -Raw | ConvertFrom-Json
    
    # Check if itemSettings.schedule has this specific job type
    $hasSpecificJobInSettings = $false
    if ($jsonContent.itemSettings -and $jsonContent.itemSettings.schedule) {
        if ($jsonContent.itemSettings.schedule.itemJobType -eq $specificJobType) {
            $hasSpecificJobInSettings = $true
        }
    }
    if (-not $hasSpecificJobInSettings) {
        $alreadyEnabled = $false
    }
    
    # Check if context menu has an action for this specific job (using the job name in the action)
    $hasSpecificJobContextMenu = $false
    if ($jsonContent.contextMenuItems) {
        foreach ($menuItem in $jsonContent.contextMenuItems) {
            # Check if the menu item name matches our job name
            if ($menuItem.name -eq $JobName) {
                $hasSpecificJobContextMenu = $true
                break
            }
        }
    }
    if (-not $hasSpecificJobContextMenu) {
        $alreadyEnabled = $false
    }
}

# Check XML for the specific job type
if (Test-Path $xmlFile) {
    $xmlContent = Get-Content $xmlFile -Raw
    # Look for the specific job type name in the XML
    if ($xmlContent -notmatch [regex]::Escape($specificJobType)) {
        $alreadyEnabled = $false
    }
}

if ($alreadyEnabled) {
    Write-Host ""
    Write-Host "✅ Job '${JobName}' is already enabled for ${ItemName} item!" -ForegroundColor Green
    Write-Host ""
    Write-Host "The following job features are configured:" -ForegroundColor Cyan
    Write-Host "  - Job type: $specificJobType"
    Write-Host "  - itemSettings.schedule in ${ItemName}Item.json"
    Write-Host "  - '${JobName}' context menu item in ${ItemName}Item.json"
    Write-Host "  - JobScheduler with ${JobName} in ${ItemName}Item.xml"
    Write-Host "  - Translation key in translations.json"
    Write-Host "  - execute.png icon in assets/images/"
    Write-Host ""
    Write-Host "No changes were made." -ForegroundColor Yellow
    exit 0
}

Write-Host "🔧 Enabling job support for ${ItemName} item with job type '${JobName}'..." -ForegroundColor Green
Write-Host ""

###############################################################################
# Copy job-related source files to Workload
###############################################################################
$jobFilesDir = Join-Path $ScriptRoot "job"

$filesToCopy = @(
    @{ Source = "jobUtils.ts"; Destination = Join-Path $ProjectRoot "Workload\app\utils\jobUtils.ts" }
    @{ Source = "JobSchedulerController.ts"; Destination = Join-Path $ProjectRoot "Workload\app\controller\JobSchedulerController.ts" }
)

foreach ($file in $filesToCopy) {
    $sourcePath = Join-Path $jobFilesDir $file.Source
    if (Test-Path $sourcePath) {
        if (-not (Test-Path $file.Destination)) {
            $destDir = Split-Path -Parent $file.Destination
            if (-not (Test-Path $destDir)) {
                New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            }
            Copy-Item -Path $sourcePath -Destination $file.Destination -Force
            Write-Host "  ✅ Copied $($file.Source) to Workload" -ForegroundColor Green
        } else {
            Write-Host "  ⚠️  $($file.Source) already exists, skipping" -ForegroundColor Yellow
        }
    } else {
        Write-Warning "Source file not found: $sourcePath"
    }
}

###############################################################################
# Uncomment job-related code in index.worker.ts
###############################################################################
$workerFile = Join-Path $ProjectRoot "Workload\app\index.worker.ts"

if (Test-Path $workerFile) {
    Write-Host "📝 Enabling job code in index.worker.ts..." -ForegroundColor Blue
    $workerContent = Get-Content $workerFile -Raw
    
    if ($workerContent -match '\[JOB_SUPPORT\]') {
        # Remove [JOB_SUPPORT] comment lines
        $workerContent = $workerContent -replace '(?m)^(\s*)// \[JOB_SUPPORT\].*\r?\n', ''
        # Uncomment commented-out code lines (// followed by actual code)
        $workerContent = $workerContent -replace '(?m)^(\s*)// (import \{ callRunItemJob)', '$1$2'
        $workerContent = $workerContent -replace '(?m)^(\s*)// (import \{ getJobDetailsPane)', '$1$2'
        $workerContent = $workerContent -replace '(?m)^(\s*)// (ItemActionContext,)', '$1$2'
        $workerContent = $workerContent -replace '(?m)^(\s*)// (ItemJobData,)', '$1$2'
        # Uncomment the case blocks
        $workerContent = $workerContent -replace '(?m)^(\s*)// (case ''run\.)', '$1$2'
        $workerContent = $workerContent -replace '(?m)^(\s*)// (case ''item\.job\.)', '$1$2'
        # Uncomment remaining lines within the case blocks (indented commented code)
        $workerContent = $workerContent -replace '(?m)^(\s{12})//     ', '$1    '
        $workerContent = $workerContent -replace '(?m)^(\s{12})// \}', '$1}'
        
        Set-Content -Path $workerFile -Value $workerContent -Encoding UTF8 -NoNewline
        Write-Host "  ✅ Uncommented job-related code in index.worker.ts" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  No [JOB_SUPPORT] markers found - job code may already be enabled" -ForegroundColor Yellow
    }
}

###############################################################################
# Update HelloWorldItem.json - Add job configuration
###############################################################################
if (-not (Test-Path $jsonFile)) {
    Write-Error "JSON manifest file not found: $jsonFile"
    exit 1
}

Write-Host "📝 Updating $jsonFile..." -ForegroundColor Blue

$jsonContent = Get-Content $jsonFile -Raw | ConvertFrom-Json

# Check if job config already exists
if ($jsonContent.itemJobActionConfig -and $jsonContent.itemSettings) {
    Write-Host "  ⚠️  Job configuration already exists in JSON file" -ForegroundColor Yellow
} else {
    # Add itemJobActionConfig with registeredActions.detail if not present
    if (-not $jsonContent.itemJobActionConfig) {
        $itemJobActionConfig = @{
            registeredActions = @{
                detail = @{
                    action = "item.job.detail"
                }
            }
        }
        $jsonContent | Add-Member -NotePropertyName "itemJobActionConfig" -NotePropertyValue $itemJobActionConfig -Force
    }
    
    # Add itemSettings with schedule if not present
    if (-not $jsonContent.itemSettings) {
        $itemSettings = @{
            schedule = @{
                itemJobType = "{{WORKLOAD_NAME}}.${ItemName}.${JobName}"
                refreshType = "Refresh"
            }
        }
        $jsonContent | Add-Member -NotePropertyName "itemSettings" -NotePropertyValue $itemSettings -Force
    }
    
    Write-Host "  ✅ Added job configuration to JSON file" -ForegroundColor Green
}

# Add context menu item for running jobs
$hasRunJobContextMenu = $false
if ($jsonContent.contextMenuItems) {
    foreach ($menuItem in $jsonContent.contextMenuItems) {
        if ($menuItem.handler -and $menuItem.handler.action -eq "run.helloworld.job") {
            $hasRunJobContextMenu = $true
            break
        }
    }
}

if (-not $hasRunJobContextMenu) {
    # Create the context menu item for running jobs
    $runJobMenuItem = @{
        name = $JobName
        displayName = "${ItemName}Item_ContextMenu_${JobName}"
        icon = @{
            name = "assets/images/execute.png"
        }
        handler = @{
            action = "run.helloworld.job"
        }
    }
    
    # Add to contextMenuItems array
    if (-not $jsonContent.contextMenuItems) {
        $jsonContent.contextMenuItems = @()
    }
    $jsonContent.contextMenuItems += $runJobMenuItem
    Write-Host "  ✅ Added 'Run Job' context menu item" -ForegroundColor Green
}

# Convert back to JSON and save
$jsonContent | ConvertTo-Json -Depth 10 | Set-Content $jsonFile -Encoding UTF8

###############################################################################
# Update HelloWorldItem.xml - Add JobScheduler section
###############################################################################
if (-not (Test-Path $xmlFile)) {
    Write-Error "XML manifest file not found: $xmlFile"
    exit 1
}

Write-Host "📝 Updating $xmlFile..." -ForegroundColor Blue

$xmlContent = Get-Content $xmlFile -Raw

# Check if JobScheduler already exists
if ($xmlContent -match "<JobScheduler>") {
    Write-Host "  ⚠️  JobScheduler configuration already exists in XML file" -ForegroundColor Yellow
} else {
    # Add JobScheduler section before </Item>
    $jobSchedulerXml = @"
    <JobScheduler>
      <OnDemandJobDeduplicateOptions>PerItem</OnDemandJobDeduplicateOptions>
      <ScheduledJobDeduplicateOptions>PerItem</ScheduledJobDeduplicateOptions>
      <ItemJobTypes>
        <ItemJobType Name="{{WORKLOAD_NAME}}.${ItemName}.${JobName}" />
      </ItemJobTypes>
    </JobScheduler>
  </Item>
"@
    
    $xmlContent = $xmlContent -replace "</Item>", $jobSchedulerXml
    $xmlContent | Set-Content $xmlFile -Encoding UTF8
    Write-Host "  ✅ Added JobScheduler configuration to XML file" -ForegroundColor Green
}

###############################################################################
# Update translations.json - Add context menu translation key
###############################################################################
$translationsFile = Join-Path $ProjectRoot "Workload\Manifest\assets\locales\en-US\translations.json"

if (-not (Test-Path $translationsFile)) {
    Write-Error "Translations file not found: $translationsFile"
    exit 1
}

Write-Host "📝 Updating $translationsFile..." -ForegroundColor Blue

$translationsContent = Get-Content $translationsFile -Raw | ConvertFrom-Json
$translationKey = "${ItemName}Item_ContextMenu_${JobName}"

# Convert JobName from PascalCase to display format (e.g., "RunJob" -> "Run Job")
$displayName = $JobName -creplace '([A-Z])', ' $1'
$displayName = $displayName.Trim()

# Check if translation already exists
if ($translationsContent.PSObject.Properties.Name -contains $translationKey) {
    Write-Host "  ⚠️  Translation key '$translationKey' already exists" -ForegroundColor Yellow
} else {
    # Add the translation key
    $translationsContent | Add-Member -NotePropertyName $translationKey -NotePropertyValue $displayName -Force
    $translationsContent | ConvertTo-Json -Depth 10 | Set-Content $translationsFile -Encoding UTF8
    Write-Host "  ✅ Added translation key '$translationKey'" -ForegroundColor Green
}

###############################################################################
# Update app translation.json - Add job-related UI translation keys
###############################################################################
$appTranslationsFile = Join-Path $ProjectRoot "Workload\app\assets\locales\en-US\translation.json"

if (Test-Path $appTranslationsFile) {
    Write-Host "📝 Updating app translations ($appTranslationsFile)..." -ForegroundColor Blue
    
    $appTranslationsContent = Get-Content $appTranslationsFile -Raw | ConvertFrom-Json
    
    $appTranslationKeys = @{
        "ItemEditor_Ribbon_RunJob_Label" = "Run Job"
        "ItemEditor_Ribbon_RunJob_Tooltip" = "Execute an on-demand job for this item"
        "ItemEditor_RunJob_Notification_Title" = "Job Started"
        "ItemEditor_RunJob_Notification_Text" = "Job instance {{jobInstanceId}} has been started."
        "ItemEditor_RunJob_Error_Title" = "Job Failed"
        "ItemEditor_RunJob_Error_Text" = "Failed to start the job. Please try again."
        "Job_Type" = "Job Type"
        "Job_Status" = "Job Status"
        "Job_Start_Time_UTC" = "Job Start Time (UTC)"
        "Job_End_Time_UTC" = "Job End Time (UTC)"
        "Job_Instance_ID" = "Job Instance ID"
    }
    
    $addedCount = 0
    foreach ($key in $appTranslationKeys.Keys) {
        if ($appTranslationsContent.PSObject.Properties.Name -notcontains $key) {
            $appTranslationsContent | Add-Member -NotePropertyName $key -NotePropertyValue $appTranslationKeys[$key] -Force
            $addedCount++
        }
    }
    
    if ($addedCount -gt 0) {
        $appTranslationsContent | ConvertTo-Json -Depth 10 | Set-Content $appTranslationsFile -Encoding UTF8
        Write-Host "  ✅ Added $addedCount job translation keys to app translations" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  All job translation keys already exist" -ForegroundColor Yellow
    }
} else {
    Write-Warning "App translations file not found: $appTranslationsFile"
}

###############################################################################
# Copy execute.png icon for context menu
###############################################################################
$sourceIcon = Join-Path $ScriptRoot "execute.png"
$targetIconDir = Join-Path $ProjectRoot "Workload\Manifest\assets\images"
$targetIcon = Join-Path $targetIconDir "execute.png"

if (-not (Test-Path $sourceIcon)) {
    Write-Warning "Source icon not found: $sourceIcon"
    Write-Warning "Context menu may not display correctly without the icon."
} else {
    Write-Host "📝 Copying execute.png icon..." -ForegroundColor Blue
    
    # Ensure target directory exists
    if (-not (Test-Path $targetIconDir)) {
        New-Item -ItemType Directory -Path $targetIconDir -Force | Out-Null
    }
    
    # Check if icon already exists
    if (Test-Path $targetIcon) {
        Write-Host "  ⚠️  Icon execute.png already exists" -ForegroundColor Yellow
    } else {
        Copy-Item -Path $sourceIcon -Destination $targetIcon -Force
        Write-Host "  ✅ Copied execute.png to assets/images/" -ForegroundColor Green
    }
}

###############################################################################
# Summary
###############################################################################
Write-Host ""
Write-Host "🎉 Job support enabled successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Changes made:" -ForegroundColor Yellow
Write-Host "  - Copied jobUtils.ts and JobSchedulerController.ts to Workload"
Write-Host "  - Uncommented job-related code in index.worker.ts"
Write-Host "  - Added itemJobActionConfig and itemSettings to ${ItemName}Item.json"
Write-Host "  - Added '${JobName}' context menu item to ${ItemName}Item.json"
Write-Host "  - Added JobScheduler section with job type '${JobName}' to ${ItemName}Item.xml"
Write-Host "  - Added translation key '${translationKey}' to manifest translations.json"
Write-Host "  - Added job UI translation keys to app translation.json"
Write-Host "  - Copied execute.png icon to assets/images/"
Write-Host ""

###############################################################################
# Next Steps
###############################################################################
Write-Host "📌 Next Steps:" -ForegroundColor Cyan
Write-Host ""
Write-Host "  1. Start the DevGateway to register with Fabric:" -ForegroundColor White
$devGatewayScript = Join-Path $ProjectRoot "scripts\Run\StartDevGateway.ps1"
Write-Host "     $devGatewayScript" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Start the DevServer to compile your changes:" -ForegroundColor White
$devServerScript = Join-Path $ProjectRoot "scripts\Run\StartDevServer.ps1"
Write-Host "     $devServerScript" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Open your item in Fabric portal and use the context menu to run jobs"
Write-Host ""
