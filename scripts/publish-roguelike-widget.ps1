$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $repoRoot "public\widgets\roguelike"
$corePublishScript = Join-Path $repoRoot "..\GenOSys - Core\scripts\publish-browser-host.ps1"

if (-not (Test-Path $corePublishScript))
{
    throw "Could not find the shared browser publish script at $corePublishScript."
}

& $corePublishScript -OutputPath $outputPath
