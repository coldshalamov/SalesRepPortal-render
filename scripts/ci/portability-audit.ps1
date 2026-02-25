param(
    [string]$TargetRepoZipPath = "",
    [string]$TargetRepoZipUrl = "",
    [string]$OutputJsonPath = "",
    [switch]$ShowLists
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Fail([string]$message) {
    Write-Host ""
    Write-Host "Portability audit failed: $message" -ForegroundColor Red
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

function Find-SolutionRoot([string]$path) {
    $solution = Get-ChildItem -Path $path -Recurse -Filter "*.sln" -File | Select-Object -First 1
    if (-not $solution) {
        Fail "No .sln file found in extracted target repo."
    }

    return $solution.DirectoryName
}

function Is-TextFile([string]$relativePath) {
    $ext = [System.IO.Path]::GetExtension($relativePath).ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($ext)) {
        return $false
    }

    return $ext -in @(
        ".cs", ".csproj", ".sln", ".cshtml", ".razor",
        ".json", ".yml", ".yaml", ".xml", ".config", ".editorconfig", ".props", ".targets",
        ".md", ".txt",
        ".js", ".mjs", ".cjs", ".ts", ".tsx",
        ".css", ".scss",
        ".ps1", ".psm1",
        ".sql"
    )
}

function Get-NormalizedTextHash([string]$path) {
    $reader = $null
    try {
        $reader = New-Object System.IO.StreamReader($path, [System.Text.Encoding]::UTF8, $true)
        $text = $reader.ReadToEnd()
    }
    finally {
        if ($reader) { $reader.Dispose() }
    }

    $normalized = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($normalized)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $sha.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($hashBytes)).Replace("-", "")
    }
    finally {
        $sha.Dispose()
    }
}

function Get-PortableHash([string]$path, [string]$relativePath) {
    if (Is-TextFile $relativePath) {
        try {
            return Get-NormalizedTextHash $path
        }
        catch {
            # Fall back to raw file hashing if a file is not valid UTF-8 text.
        }
    }

    return (Get-FileHash -Algorithm SHA256 -Path $path).Hash
}

$repoRoot = Resolve-RepoRoot
Set-Location $repoRoot

if ([string]::IsNullOrWhiteSpace($TargetRepoZipPath) -and [string]::IsNullOrWhiteSpace($TargetRepoZipUrl)) {
    Fail "Provide -TargetRepoZipPath or -TargetRepoZipUrl."
}

$workDir = Join-Path ([System.IO.Path]::GetTempPath()) ("port-audit-" + [Guid]::NewGuid().ToString("N"))
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

    $onlyInSandbox = New-Object System.Collections.Generic.List[string]
    $same = New-Object System.Collections.Generic.List[string]
    $different = New-Object System.Collections.Generic.List[string]

    foreach ($file in $portableFiles) {
        $targetPath = Join-Path $targetRoot ($file.Relative.Replace("/", "\"))
        if (-not (Test-Path $targetPath)) {
            $onlyInSandbox.Add($file.Relative)
            continue
        }

        $sourceHash = Get-PortableHash $file.FullName $file.Relative
        $targetHash = Get-PortableHash $targetPath $file.Relative
        if ($sourceHash -eq $targetHash) {
            $same.Add($file.Relative)
        }
        else {
            $different.Add($file.Relative)
        }
    }

    $result = [PSCustomObject]@{
        TargetRepoZipPath = (Resolve-Path $TargetRepoZipPath).Path
        TimestampUtc = [DateTime]::UtcNow.ToString("o")
        TotalPortableFiles = $portableFiles.Count
        OnlyInSandboxCount = $onlyInSandbox.Count
        SameCount = $same.Count
        DifferentCount = $different.Count
        OnlyInSandbox = $onlyInSandbox
        Same = $same
        Different = $different
    }

    Write-Host ""
    Write-Host "Portable-file diff (normalized line endings for text files):"
    Write-Host "  Total portable files considered: $($result.TotalPortableFiles)"
    Write-Host "  Present only in sandbox:          $($result.OnlyInSandboxCount)"
    Write-Host "  Same content:                     $($result.SameCount)"
    Write-Host "  Different content:                $($result.DifferentCount)"

    if ($ShowLists.IsPresent) {
        Write-Host ""
        Write-Host "Only in sandbox ($($onlyInSandbox.Count)):"
        $onlyInSandbox | Sort-Object | ForEach-Object { Write-Host "  $_" }

        Write-Host ""
        Write-Host "Different content ($($different.Count)):"
        $different | Sort-Object | ForEach-Object { Write-Host "  $_" }
    }

    if (-not [string]::IsNullOrWhiteSpace($OutputJsonPath)) {
        $outDir = Split-Path -Parent $OutputJsonPath
        if (-not [string]::IsNullOrWhiteSpace($outDir) -and -not (Test-Path $outDir)) {
            New-Item -ItemType Directory -Path $outDir -Force | Out-Null
        }

        $result | ConvertTo-Json -Depth 5 | Set-Content -Path $OutputJsonPath -Encoding UTF8
        Write-Host ""
        Write-Host "Wrote audit JSON: $OutputJsonPath"
    }
}
finally {
    if (Test-Path $workDir) {
        Remove-Item -Path $workDir -Recurse -Force
    }
}

