$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "widgets\ProcGenWidget\ProcGenWidget.csproj"
$outputPath = Join-Path $repoRoot "public\widgets\proc-gen"
$stagingPath = Join-Path $repoRoot "public\widgets\proc-gen.__staging"
$localDotnetHome = Join-Path $repoRoot ".dotnet-cli"
$nugetConfigPath = Join-Path $localDotnetHome "NuGet.Config"

New-Item -ItemType Directory -Path $localDotnetHome -Force | Out-Null

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = $localDotnetHome

if (-not (Test-Path $nugetConfigPath))
{
    @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
'@ | Set-Content -Path $nugetConfigPath
}

New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
Get-ChildItem -Path $stagingPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force

dotnet publish $projectPath -c Release -o $stagingPath --configfile $nugetConfigPath

if ($LASTEXITCODE -ne 0)
{
    throw "dotnet publish failed for $projectPath."
}

$wwwrootPath = Join-Path $stagingPath "wwwroot"
if (-not (Test-Path $wwwrootPath))
{
    throw "Could not find published wwwroot output at $wwwrootPath."
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
Get-ChildItem -Path $outputPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
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

$dotnetModuleContent = Get-Content -Raw $dotnetModule.FullName
$jsonMatch = [regex]::Match(
    $dotnetModuleContent,
    '/\*json-start\*/(?<json>\{.*?\})/\*json-end\*/',
    [System.Text.RegularExpressions.RegexOptions]::Singleline)

if (-not $jsonMatch.Success)
{
    throw "Could not find embedded dotnet runtime config in $($dotnetModule.FullName)."
}

$dotnetConfig = $jsonMatch.Groups["json"].Value | ConvertFrom-Json
$hotReloadPattern = "Microsoft.DotNet.HotReload.WebAssembly.Browser"

if ($dotnetConfig.resources -and $dotnetModuleContent -like "*$hotReloadPattern*")
{
    if ($dotnetConfig.resources.assembly)
    {
        $dotnetConfig.resources.assembly = @(
            $dotnetConfig.resources.assembly |
                Where-Object { $_.virtualPath -notlike "$hotReloadPattern*" }
        )
    }

    if ($dotnetConfig.resources.libraryInitializers)
    {
        $dotnetConfig.resources.libraryInitializers = @(
            $dotnetConfig.resources.libraryInitializers |
                Where-Object { $_.name -notlike "*$hotReloadPattern*" }
        )
    }

    if ($dotnetConfig.resources.modulesAfterConfigLoaded)
    {
        $dotnetConfig.resources.modulesAfterConfigLoaded = @(
            $dotnetConfig.resources.modulesAfterConfigLoaded |
                Where-Object { $_.name -notlike "*$hotReloadPattern*" }
        )
    }

    $patchedJson = $dotnetConfig | ConvertTo-Json -Depth 100 -Compress:$false
    $patchedDotnetModuleContent = $dotnetModuleContent.Substring(0, $jsonMatch.Index) +
        "/*json-start*/$patchedJson/*json-end*/" +
        $dotnetModuleContent.Substring($jsonMatch.Index + $jsonMatch.Length)

    Set-Content -Path $dotnetModule.FullName -Value $patchedDotnetModuleContent -NoNewline
}

$stableDotnetModulePath = Join-Path $frameworkPath "dotnet.js"
if (-not (Test-Path $stableDotnetModulePath))
{
    Copy-Item -Path $dotnetModule.FullName -Destination $stableDotnetModulePath -Force
}

$hotReloadContentPath = Join-Path $outputPath "_content\Microsoft.DotNet.HotReload.WebAssembly.Browser"
if (Test-Path $hotReloadContentPath)
{
    Remove-Item -Path $hotReloadContentPath -Recurse -Force
}

Get-ChildItem -Path $frameworkPath -Filter "Microsoft.DotNet.HotReload.WebAssembly.Browser*" -ErrorAction SilentlyContinue |
    Remove-Item -Force

Get-ChildItem -Path $outputPath -Filter "*.staticwebassets.*" -ErrorAction SilentlyContinue |
    Remove-Item -Force

Get-ChildItem -Path $outputPath -Filter "*.runtimeconfig.json" -ErrorAction SilentlyContinue |
    Remove-Item -Force

Get-ChildItem -Path $outputPath -Filter "web.config" -ErrorAction SilentlyContinue |
    Remove-Item -Force

Get-ChildItem -Path $stagingPath -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
