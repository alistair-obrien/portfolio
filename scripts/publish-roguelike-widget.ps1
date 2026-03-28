$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "widgets\RoguelikeWidget\RoguelikeWidget.csproj"
$outputPath = Join-Path $repoRoot "public\widgets\roguelike"
$publishPath = Join-Path $repoRoot "artifacts\widgets\roguelike-publish"

$env:DOTNET_CLI_HOME = $repoRoot
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

New-Item -ItemType Directory -Path $publishPath -Force | Out-Null
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

Get-ChildItem -Path $publishPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
Get-ChildItem -Path $outputPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

dotnet publish $projectPath -c Release -o $publishPath --no-restore

Copy-Item -Path (Join-Path $publishPath "wwwroot\*") -Destination $outputPath -Recurse -Force
