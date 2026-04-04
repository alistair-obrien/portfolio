$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$outputPath = Join-Path $repoRoot "public\widgets\proc-gen"
$stagingPath = Join-Path $repoRoot "public\widgets\proc-gen.__staging"
$procGenRepoRoot = Join-Path $repoRoot "..\ProcGen"
$widgetArtifactPath = Join-Path $procGenRepoRoot ".artifacts\widgets\proc-gen"

New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
Get-ChildItem -Path $stagingPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

if (-not (Test-Path $widgetArtifactPath))
{
    throw "Could not find the ProcGen widget artifact at $widgetArtifactPath. Run ProcGen\\scripts\\publish-widget.ps1 first."
}

Get-ChildItem -Path $widgetArtifactPath -Force | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $stagingPath -Recurse -Force
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
Get-ChildItem -Path $outputPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
Get-ChildItem -Path $stagingPath -Force | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $outputPath -Recurse -Force
}

Get-ChildItem -Path $stagingPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
