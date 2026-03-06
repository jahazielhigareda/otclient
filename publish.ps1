# publish.ps1 — Build and publish OTClient on Windows.
# Usage: .\publish.ps1 [-RID win-x64]
#
# Requires: .NET 10 SDK  (https://dotnet.microsoft.com/download)
# Task 10.9 – Platform publish scripts.

param(
    [string]$RID = "win-x64"
)

$ErrorActionPreference = "Stop"

$ScriptDir  = Split-Path -Parent $MyInvocation.MyCommand.Definition
$CSharpDir  = Join-Path $ScriptDir "csharp"
$Launcher   = Join-Path $CSharpDir "src\OTClient.Launcher\OTClient.Launcher.csproj"
$OutBase    = Join-Path $ScriptDir "publish"
$Out        = Join-Path $OutBase $RID

Write-Host "=== OTClient Publish ===" -ForegroundColor Cyan
Write-Host "  RID        : $RID"
Write-Host "  Output dir : $Out"
Write-Host ""

dotnet publish $Launcher `
    --runtime $RID `
    --configuration Release `
    --self-contained true `
    --output $Out `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

Write-Host ""
Write-Host "=== Published to $Out ===" -ForegroundColor Green
