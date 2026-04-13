$ErrorActionPreference = "Stop"

$portfolioRoot = Split-Path -Parent $PSScriptRoot
$tauriRoot = Join-Path $portfolioRoot "..\GenOSys - Tauri\GenOSys"
$outputPath = Join-Path $portfolioRoot "public\genosys-app"
$distPath = Join-Path $tauriRoot "dist"

if (-not (Test-Path $tauriRoot))
{
    throw "Could not find the GenOSys Tauri project at $tauriRoot."
}

Push-Location $tauriRoot
try
{
    npm run build
}
finally
{
    Pop-Location
}

if (-not (Test-Path $distPath))
{
    throw "Expected web build output was not found at $distPath."
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
Get-ChildItem -Path $outputPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
Get-ChildItem -Path $distPath -Force | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $outputPath -Recurse -Force
}
