param(
    [string]$BaseSha = "",
    [string]$HeadSha = "HEAD"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$message) {
    Write-Host ""
    Write-Host "Portability guardrail failed: $message" -ForegroundColor Red
    exit 1
}

function Get-RepoRoot {
    $root = Join-Path $PSScriptRoot "..\.."
    return (Resolve-Path $root).Path
}

function Ensure-FileExists([string]$path, [string]$name) {
    if (-not (Test-Path $path)) {
        Fail "$name is missing at $path"
    }
}

function Has-Text([string]$path, [string]$pattern) {
    if (-not (Test-Path $path)) {
        return $false
    }

    $content = Get-Content -Raw -Path $path
    return $content -match $pattern
}

$repoRoot = Get-RepoRoot
Set-Location $repoRoot

$playbookPath = Join-Path $repoRoot "MIGRATION_PLAYBOOK.md"
$portingLogPath = Join-Path $repoRoot "PORTING_LOG.md"
$dbContextPath = Join-Path $repoRoot "LeadManagementPortal\Data\ApplicationDbContext.cs"
$programPath = Join-Path $repoRoot "LeadManagementPortal\Program.cs"
$leadsControllerPath = Join-Path $repoRoot "LeadManagementPortal\Controllers\LeadsController.cs"
$migrationsDir = Join-Path $repoRoot "LeadManagementPortal\Migrations"

Ensure-FileExists $playbookPath "Migration playbook"
Ensure-FileExists $portingLogPath "Porting log"
Ensure-FileExists $dbContextPath "ApplicationDbContext"
Ensure-FileExists $programPath "Program.cs"
Ensure-FileExists $leadsControllerPath "LeadsController.cs"
Ensure-FileExists $migrationsDir "Migrations directory"

# 1) Migration metadata integrity.
$migrationFiles = Get-ChildItem -Path $migrationsDir -Filter "*.cs" -File |
    Where-Object { $_.Name -notmatch "Designer\.cs$|ModelSnapshot\.cs$" }

if ($migrationFiles.Count -eq 0) {
    Fail "No migration files found in LeadManagementPortal/Migrations."
}

$missingMetadata = New-Object System.Collections.Generic.List[string]
foreach ($file in $migrationFiles) {
    $fileText = Get-Content -Raw -Path $file.FullName
    $hasAttribute = $fileText -match "\[Migration\("
    $designerPath = Join-Path $migrationsDir ($file.BaseName + ".Designer.cs")
    $hasDesigner = Test-Path $designerPath

    if (-not $hasAttribute -and -not $hasDesigner) {
        $missingMetadata.Add($file.Name)
    }
}

if ($missingMetadata.Count -gt 0) {
    Fail "Migration metadata is incomplete (missing [Migration] attribute and no designer file): $($missingMetadata -join ', ')"
}

# 2) Follow-up task schema contract.
$dbContextText = Get-Content -Raw -Path $dbContextPath
$usesFollowUps = $dbContextText -match "DbSet<\s*LeadFollowUpTask\s*>"
if ($usesFollowUps) {
    $hasFollowUpMigration = Get-ChildItem -Path $migrationsDir -Filter "*.cs" -File |
        ForEach-Object { Get-Content -Raw -Path $_.FullName } |
        Where-Object { $_ -match 'CreateTable\(\s*name:\s*"LeadFollowUpTasks"' } |
        Select-Object -First 1

    if (-not $hasFollowUpMigration) {
        Fail "LeadFollowUpTask DbSet exists but no migration creates LeadFollowUpTasks."
    }
}

# 3) Notification dependency contract.
$leadsControllerText = Get-Content -Raw -Path $leadsControllerPath
$usesNotifications = $leadsControllerText -match "INotificationService"
if ($usesNotifications) {
    $requiredPaths = @(
        "LeadManagementPortal\Models\Notification.cs",
        "LeadManagementPortal\Services\INotificationService.cs",
        "LeadManagementPortal\Services\NotificationService.cs",
        "LeadManagementPortal\Controllers\NotificationsApiController.cs"
    )

    foreach ($relative in $requiredPaths) {
        $fullPath = Join-Path $repoRoot $relative
        Ensure-FileExists $fullPath $relative
    }

    if (-not (Has-Text $programPath "AddScoped<\s*INotificationService\s*,\s*NotificationService\s*>")) {
        Fail "LeadsController uses INotificationService but Program.cs is missing DI registration."
    }

    $hasNotificationMigration = Get-ChildItem -Path $migrationsDir -Filter "*.cs" -File |
        ForEach-Object { Get-Content -Raw -Path $_.FullName } |
        Where-Object { $_ -match 'CreateTable\(\s*name:\s*"Notifications"' } |
        Select-Object -First 1

    if (-not $hasNotificationMigration) {
        Fail "INotificationService is wired but no migration creates Notifications."
    }
}

# 4) PR hygiene guardrail: do not mix Render-only and product files in one PR.
if (-not [string]::IsNullOrWhiteSpace($BaseSha) -and -not [string]::IsNullOrWhiteSpace($HeadSha)) {
    $changedFiles = git diff --name-only "$BaseSha...$HeadSha" 2>$null
    if ($LASTEXITCODE -eq 0 -and $changedFiles) {
        $normalized = $changedFiles | ForEach-Object { $_.Trim().Replace("\", "/") } | Where-Object { $_ -ne "" }

        $renderOnly = @($normalized | Where-Object {
            $_ -match '^render\.yaml$' -or
            $_ -match '^Dockerfile$' -or
            $_ -match '^RENDER\.md$' -or
            $_ -match '^\.render/' -or
            $_ -match '^LeadManagementPortal/appsettings(\.[^/]+)?\.json$'
        })

        $productFiles = @($normalized | Where-Object {
            $_ -match '^LeadManagementPortal/' -or
            $_ -match '^LeadManagementPortal\.Tests/'
        } | Where-Object {
            $_ -notmatch '^LeadManagementPortal/appsettings(\.[^/]+)?\.json$'
        })

        if ($renderOnly.Count -gt 0 -and $productFiles.Count -gt 0) {
            Fail "This PR mixes Render-only/runtime config files with product code. Split into separate PRs.`nRender-only: $($renderOnly -join ', ')`nProduct: $($productFiles -join ', ')"
        }
    }
}

Write-Host "Portability guardrails passed."
