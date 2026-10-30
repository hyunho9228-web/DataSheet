[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'HumanGhostDataTools\PublishRuns'))
$workRoot = Join-Path $temporaryRoot ([Guid]::NewGuid().ToString('N'))
$previousBuildRoot = $env:DataSheetBuildRoot
$exitCode = 1
try {
    $env:DataSheetBuildRoot = Join-Path $workRoot 'Build'
    $publishRoot = Join-Path $workRoot 'Publish'
    dotnet publish (Join-Path $PSScriptRoot 'DataExporter\DataExporter.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None --source https://api.nuget.org/v3/index.json -o $publishRoot
    if ($LASTEXITCODE -ne 0) { throw '실행파일 빌드에 실패했습니다.' }
    $executable = Join-Path $publishRoot 'DeployData.exe'
    Push-Location -LiteralPath $projectRoot
    try {
        & $executable --check
        if ($LASTEXITCODE -ne 0) { throw '새 실행파일의 데이터 검사에 실패했습니다. 기존 EXE를 유지합니다.' }
    }
    finally { Pop-Location }
    Copy-Item -LiteralPath $executable -Destination (Join-Path $projectRoot 'DeployData.exe') -Force
    Write-Host '배포 실행파일 생성·검사가 완료되었습니다. 소스를 변경하면 실행파일도 함께 버전 관리에 반영하세요.'
    $exitCode = 0
}
catch { Write-Host $_.Exception.Message -ForegroundColor Red }
finally {
    $env:DataSheetBuildRoot = $previousBuildRoot
    $resolvedWorkRoot = [IO.Path]::GetFullPath($workRoot)
    if ($resolvedWorkRoot.StartsWith($temporaryRoot + '\', [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $resolvedWorkRoot)) {
        Remove-Item -LiteralPath $resolvedWorkRoot -Recurse -Force
    }
}
exit $exitCode
