#requires -Version 7.0
<#
.SYNOPSIS
    构建 KeyTrace 并发布为单文件 exe。

.EXAMPLE
    ./scripts/publish.ps1

.EXAMPLE
    ./scripts/publish.ps1 -Runtime win-arm64 -Output D:\dist
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('win-x64', 'win-x86', 'win-arm64')]
    [string]$Runtime = 'win-x64',

    [string]$Output = '',

    [switch]$FrameworkDependent,

    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/KeyTrace.App/KeyTrace.App.csproj'

if (-not (Test-Path $project)) {
    throw "找不到项目文件：$project"
}

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $repoRoot "artifacts/publish/$Runtime"
}

Write-Host "==> 构建 $Configuration / $Runtime" -ForegroundColor Cyan

if (-not $SkipTests) {
    Write-Host "==> 运行单元测试" -ForegroundColor Cyan
    dotnet test (Join-Path $repoRoot 'tests/KeyTrace.Core.Tests/KeyTrace.Core.Tests.csproj') -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "单元测试失败（exit $LASTEXITCODE）"
    }
}

$publishArgs = @(
    'publish', $project,
    '-c', $Configuration,
    '-r', $Runtime,
    '-o', $Output,
    '--nologo'
)

if ($FrameworkDependent) {
    $publishArgs += '--no-self-contained'
}
else {
    $publishArgs += '--self-contained', 'true'
    $publishArgs += '-p:PublishSingleFile=true'
}

Write-Host "==> dotnet $($publishArgs -join ' ')" -ForegroundColor DarkGray
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "发布失败（exit $LASTEXITCODE）"
}

$exe = Join-Path $Output 'KeyTrace.exe'
if (Test-Path $exe) {
    $sizeMb = [Math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Host "==> 完成：$exe（$sizeMb MB）" -ForegroundColor Green
}
else {
    Write-Host "==> 发布完成，但未找到 KeyTrace.exe，请检查输出目录：$Output" -ForegroundColor Yellow
}
