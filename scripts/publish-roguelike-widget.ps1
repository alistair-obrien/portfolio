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

$wwwrootPath = Join-Path $publishPath "wwwroot"

if (-not (Test-Path $wwwrootPath))
{
    throw "Could not find published wwwroot output at $wwwrootPath."
}

Get-ChildItem -Path $wwwrootPath -Force | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $outputPath -Recurse -Force
}

$frameworkPath = Join-Path $outputPath "_framework"
$dotnetModule = Get-ChildItem -Path $frameworkPath -Filter "dotnet.*.js" |
    Where-Object { $_.Name -notlike "dotnet.native.*" -and $_.Name -notlike "dotnet.runtime.*" } |
    Select-Object -First 1

if (-not $dotnetModule)
{
    throw "Could not find the published dotnet JS module in $frameworkPath."
}

Copy-Item -Path $dotnetModule.FullName -Destination (Join-Path $frameworkPath "dotnet.js") -Force
