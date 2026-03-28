$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

& (Join-Path $PSScriptRoot "publish-roguelike-widget.ps1")

Push-Location $repoRoot
try {
    npm run dev
}
finally {
    Pop-Location
}
