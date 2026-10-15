param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,
    [string]$DotnetPath = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'VampireCrawlersRumble.csproj'
$gameExe = Join-Path $GameDir 'Vampire Crawlers.exe'
$interop = Join-Path $GameDir 'BepInEx\interop\Pancake.dll'
if (-not (Test-Path -LiteralPath $gameExe)) { throw "Game executable not found: $gameExe" }
if (-not (Test-Path -LiteralPath $interop)) { throw "BepInEx interop not found. Start the game once with BepInEx installed." }

& $DotnetPath build $project -c Release "-p:GameDir=$GameDir" --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\Release\net6.0\VampireCrawlersRumble.dll') -Destination $dist -Force
Write-Host "Built: $(Join-Path $dist 'VampireCrawlersRumble.dll')"

