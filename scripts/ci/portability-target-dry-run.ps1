param(
    [string]$TargetRepoZipPath = "",
    [string]$TargetRepoZipUrl = "",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$message) {
    Write-Host ""
    Write-Host "Target dry-run failed: $message" -ForegroundColor Red
    exit 1
}

function Resolve-RepoRoot {
    $root = Join-Path $PSScriptRoot "..\.."
    return (Resolve-Path $root).Path
}

function Normalize-Relative([string]$absolute, [string]$root) {
    $relative = $absolute.Substring($root.Length).TrimStart("\", "/")
    return $relative.Replace("\", "/")
}

function Ensure-DotNet {
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnetCmd) {
        Fail "dotnet SDK is required for target dry run."
    }
}

function Find-SolutionRoot([string]$path) {
    $solution = Get-ChildItem -Path $path -Recurse -Filter "*.sln" -File | Select-Object -First 1
    if (-not $solution) {
        Fail "No .sln file found in extracted target repo."
    }

    return $solution.DirectoryName
}

$repoRoot = Resolve-RepoRoot
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($TargetRepoZipPath) -and [string]::IsNullOrWhiteSpace($TargetRepoZipUrl)) {
    Fail "Provide -TargetRepoZipPath or -TargetRepoZipUrl."
}

Ensure-DotNet

$workDir = Join-Path ([System.IO.Path]::GetTempPath()) ("port-dry-run-" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $workDir -Force | Out-Null

try {
    if (-not [string]::IsNullOrWhiteSpace($TargetRepoZipUrl)) {
        $downloadPath = Join-Path $workDir "target.zip"
        Write-Host "Downloading target zip from URL..."
        Invoke-WebRequest -Uri $TargetRepoZipUrl -OutFile $downloadPath
        $TargetRepoZipPath = $downloadPath
    }

    if (-not (Test-Path $TargetRepoZipPath)) {
        Fail "Target zip does not exist: $TargetRepoZipPath"
    }

    $extractRoot = Join-Path $workDir "target"
    New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null
    Expand-Archive -Path $TargetRepoZipPath -DestinationPath $extractRoot -Force

    $targetRoot = Find-SolutionRoot $extractRoot
    Write-Host "Target solution root: $targetRoot"

    $excludedPathPattern = @(
        "^\.git/",
        "^\.render/",
        "^render\.yaml$",
        "^Dockerfile$",
        "^RENDER\.md$",
        "^LeadManagementPortal/wwwroot/uploads/",
        "^LeadManagementPortal/bin/",
        "^LeadManagementPortal/obj/",
        "^LeadManagementPortal\.Tests/bin/",
        "^LeadManagementPortal\.Tests/obj/",
        "(^|/)(AGENTS|CLAUDE|GEMINI)\.md$"
    )

    $includePrefixPattern = "^(LeadManagementPortal/|LeadManagementPortal\.Tests/|LeadManagementPortal\.sln$|scripts/ci/)"
    $currentFiles = Get-ChildItem -Path $repoRoot -Recurse -File

$candidateFiles = $currentFiles | ForEach-Object {
        $relative = Normalize-Relative $_.FullName $repoRoot
        [PSCustomObject]@{
            Relative = $relative
            FullName = $_.FullName
        }
    } | Where-Object {
        $_.Relative -match $includePrefixPattern
    }

    # Filter exclusions in a second pass to keep pattern matching explicit.
    $portableFiles = New-Object System.Collections.Generic.List[object]
    foreach ($file in $candidateFiles) {
        $excluded = $false
        foreach ($pattern in $excludedPathPattern) {
            if ($file.Relative -match $pattern) {
                $excluded = $true
                break
            }
        }
        if (-not $excluded) {
            $portableFiles.Add($file)
        }
    }

    $copied = New-Object System.Collections.Generic.List[string]
    foreach ($file in $portableFiles) {
        $targetPath = Join-Path $targetRoot ($file.Relative.Replace("/", "\"))
        $targetDir = Split-Path -Parent $targetPath
        if (-not (Test-Path $targetDir)) {
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }

        $shouldCopy = $true
        if (Test-Path $targetPath) {
            $sourceHash = (Get-FileHash -Algorithm SHA256 -Path $file.FullName).Hash
            $targetHash = (Get-FileHash -Algorithm SHA256 -Path $targetPath).Hash
            $shouldCopy = $sourceHash -ne $targetHash
        }

        if ($shouldCopy) {
            Copy-Item -Path $file.FullName -Destination $targetPath -Force
            $copied.Add($file.Relative)
        }
    }

    if ($copied.Count -eq 0) {
        Write-Host "No portable file differences to apply; target already matches candidate paths."
    }
    else {
        Write-Host "Applied $($copied.Count) portable file(s) onto target snapshot."
    }

    Push-Location $targetRoot
    try {
        dotnet restore LeadManagementPortal.sln
        dotnet build LeadManagementPortal.sln -c Release --no-restore

        if (-not $SkipTests.IsPresent) {
            dotnet test LeadManagementPortal.Tests/LeadManagementPortal.Tests.csproj `
                -c Release `
                --no-build `
                --filter "FullyQualifiedName!~LeadManagementPortal.Tests.LocalSeeder"
        }
    }
    finally {
        Pop-Location
    }

    Write-Host "Target portability dry run succeeded."
}
finally {
    if (Test-Path $workDir) {
        Remove-Item -Path $workDir -Recurse -Force
    }
}
