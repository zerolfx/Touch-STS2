param(
    [string]$GameDir = 'C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2',
    [string]$GameDataDir = '',
    [string]$Sts1Jar = 'C:/Program Files (x86)/Steam/steamapps/common/SlayTheSpire/desktop-1.0.jar',
    [switch]$Package,
    [switch]$NoRestore
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
# Extract the exact cursor texture from the local STS1 installation. Research
# assets stay outside source control; the built DLL needs no runtime STS1 path.
$cursorDir = Join-Path $repoRoot 'research/sts1'
New-Item -ItemType Directory -Force -Path $cursorDir | Out-Null
$archive = [IO.Compression.ZipFile]::OpenRead($Sts1Jar)
try {
    $orb = $archive.GetEntry('images/vfx/orb.png')
    if (!$orb) { throw 'STS1 archive is missing images/vfx/orb.png.' }
    [IO.Compression.ZipFileExtensions]::ExtractToFile($orb, (Join-Path $cursorDir 'orb.png'), $true)
} finally { $archive.Dispose() }
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$restoreConfig = Join-Path $repoRoot 'NuGet.Config'
$project = Join-Path $repoRoot 'src/TouchSts2/TouchSts2.csproj'
$buildArgs = @('build', $project, '-c', 'Release', "-p:GameDir=$GameDir", "-p:RestoreConfigFile=$restoreConfig")
if ($GameDataDir) { $buildArgs += "-p:GameDataDir=$GameDataDir" }
if ($NoRestore) { $buildArgs += '--no-restore' }
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
$testBuildArgs = @('build', (Join-Path $repoRoot 'tests/TouchSts2.Tests/TouchSts2.Tests.csproj'), '-c', 'Release', "-p:RestoreConfigFile=$restoreConfig")
if ($NoRestore) { $testBuildArgs += '--no-restore' }
& dotnet @testBuildArgs
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
if (!$GameDataDir) { $GameDataDir = Join-Path $GameDir 'data_sts2_windows_x86_64' }
& dotnet (Join-Path $repoRoot 'tests/TouchSts2.Tests/bin/Release/net9.0/TouchSts2.Tests.dll') $GameDataDir (Join-Path $repoRoot 'src/TouchSts2/bin/Release/net9.0/TouchSts2.dll')
if ($LASTEXITCODE -ne 0) { throw 'Gesture tests failed.' }
if ($Package) {
    $output = Join-Path $repoRoot 'dist/TouchSts2'
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'src/TouchSts2/bin/Release/net9.0/TouchSts2.dll') -Destination $output -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot 'TouchSts2.json') -Destination $output -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/PLAYTEST.md') -Destination $output -Force
    $version = (Get-Content -LiteralPath (Join-Path $repoRoot 'TouchSts2.json') -Raw | ConvertFrom-Json).version
    $packagePath = Join-Path $repoRoot "dist/TouchSts2-$version.zip"
    Compress-Archive -Path $output -DestinationPath $packagePath -Force
    Write-Host "Package: $packagePath"
}
