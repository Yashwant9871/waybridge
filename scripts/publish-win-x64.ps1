$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\WaybridgeApp.csproj"
$publishDir = Join-Path $PSScriptRoot "..\bin\Release\net8.0-windows\win-x64\publish"
$zipPath = Join-Path $PSScriptRoot "..\WaybridgeApp-win-x64.zip"

dotnet publish $project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true

if (Test-Path $zipPath) {
  Remove-Item $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Publish complete:"
Write-Host "EXE: $publishDir\WaybridgeApp.exe"
Write-Host "ZIP: $zipPath"
