$ErrorActionPreference = "Stop"

$root = Join-Path $PSScriptRoot "..\.."
$viewsRoot = Join-Path $root "LeadManagementPortal\Views"
$atRulePattern = '(?<!@)@(media|keyframes|supports|font-face|page|import)\b'

if (-not (Test-Path $viewsRoot)) {
    Write-Error "Views directory not found: $viewsRoot"
    exit 1
}

$violations = New-Object System.Collections.Generic.List[string]
$files = Get-ChildItem -Path $viewsRoot -Recurse -File -Filter "*.cshtml"

foreach ($file in $files) {
    $inStyleBlock = $false
    $lineNumber = 0

    foreach ($line in Get-Content -Path $file.FullName) {
        $lineNumber++

        if ($line -imatch '<style(\s|>)') {
            $inStyleBlock = $true
        }

        if ($inStyleBlock -and $line -match $atRulePattern) {
            $relativePath = Resolve-Path -Relative -Path $file.FullName
            $snippet = $line.Trim()
            $violations.Add("${relativePath}:$lineNumber -> $snippet")
        }

        if ($line -imatch '</style>') {
            $inStyleBlock = $false
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Host "Found unescaped CSS at-rules inside Razor <style> blocks:" -ForegroundColor Red
    $violations | ForEach-Object { Write-Host " - $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "Use @@media/@@keyframes/etc. in .cshtml style blocks so Razor emits literal @." -ForegroundColor Yellow
    exit 1
}

Write-Host "Razor CSS at-rule check passed."
