[CmdletBinding()]
param([ValidateSet('Deploy', 'Check')][string]$Mode = 'Check')
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$temporaryRoot = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) 'HumanGhostDataTools\BuildRuns'))
$buildRoot = Join-Path $temporaryRoot ([Guid]::NewGuid().ToString('N'))
$previousBuildRoot = $env:DataSheetBuildRoot
$exitCode = 1
try {
    Set-Location -LiteralPath $projectRoot
    $sdkAvailable = @(dotnet --list-sdks | Where-Object { $_ -match '^10\.' }).Count -gt 0
    if (-not $sdkAvailable) { throw '.NET 10 SDK 설치가 필요합니다.' }
    $env:DataSheetBuildRoot = $buildRoot
    $exportArgument = if ($Mode -eq 'Deploy') { '--deploy' } else { '--check' }
    dotnet run --project (Join-Path $PSScriptRoot 'DataExporter\DataExporter.csproj') -- $exportArgument
    $exitCode = $LASTEXITCODE
}
catch { Write-Host $_.Exception.Message -ForegroundColor Red }
finally {
    $env:DataSheetBuildRoot = $previousBuildRoot
    $resolvedBuildRoot = [System.IO.Path]::GetFullPath($buildRoot)
    if ($resolvedBuildRoot.StartsWith($temporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedBuildRoot)) {
        Remove-Item -LiteralPath $resolvedBuildRoot -Recurse -Force
    }
}
exit $exitCode
