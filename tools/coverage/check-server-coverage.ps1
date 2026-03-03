param(
    [string]$CoverageRoot = "artifacts/test-results",
    [string]$AssemblyName = "MiniPainterHub.Server",
    [double]$Threshold = 65,
    [string[]]$ExcludeFilePatterns = @("MiniPainterHub.Server\Migrations\*")
)

$coverageFiles = Get-ChildItem -Path $CoverageRoot -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTimeUtc -Descending

if (-not $coverageFiles -or $coverageFiles.Count -eq 0) {
    throw "No cobertura coverage file found under '$CoverageRoot'."
}

$coverageFile = $null
$xml = $null
$package = @()

foreach ($candidate in $coverageFiles) {
    [xml]$candidateXml = Get-Content -Path $candidate.FullName
    $candidatePackage = @($candidateXml.coverage.packages.package | Where-Object { $_.name -eq $AssemblyName })
    if ($candidatePackage -and $candidatePackage.Count -gt 0) {
        $coverageFile = $candidate.FullName
        $xml = $candidateXml
        $package = $candidatePackage
        break
    }
}

if (-not $coverageFile) {
    throw "Assembly package '$AssemblyName' was not found in any cobertura file under '$CoverageRoot'."
}

$totalCovered = 0
$totalValid = 0

foreach ($pkg in $package) {
    foreach ($class in @($pkg.classes.class)) {
        $filename = ([string]$class.filename).Replace("/", "\")
        $shouldExclude = $false

        foreach ($pattern in $ExcludeFilePatterns) {
            $normalizedPattern = $pattern.Replace("/", "\")
            if ($filename -like $normalizedPattern) {
                $shouldExclude = $true
                break
            }
        }

        if ($shouldExclude) {
            continue
        }

        $lines = @($class.lines.line)
        $valid = $lines.Count
        $covered = @($lines | Where-Object { [int]$_.hits -gt 0 }).Count

        $totalValid += $valid
        $totalCovered += $covered
    }
}

if ($totalValid -eq 0) {
    throw "No eligible executable lines found for '$AssemblyName' after exclusions."
}

$coveragePercent = [math]::Round(($totalCovered / $totalValid) * 100, 2)
Write-Host "Coverage source: $coverageFile"
Write-Host "Assembly: $AssemblyName"
Write-Host "Line coverage (excluding configured patterns): $coveragePercent% ($totalCovered/$totalValid)"
Write-Host "Threshold: $Threshold%"

if ($coveragePercent -lt $Threshold) {
    throw "Coverage gate failed: $coveragePercent% is below threshold $Threshold%."
}

Write-Host "Coverage gate passed."
