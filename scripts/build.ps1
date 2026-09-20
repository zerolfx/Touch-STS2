param(
    [string]$GameDir = 'C:/Program Files (x86)/Steam/steamapps/common/Slay the Spire 2',
    [string]$GameDataDir = '',
    [switch]$ReferenceAssemblies,
    [switch]$Package,
    [switch]$NoRestore
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$cursorPath = Join-Path $repoRoot 'src/TouchSts2/Assets/touch-cursor.png'
if ((Get-FileHash -LiteralPath $cursorPath).Hash -ne 'AB6A47EE0F8742873BC8F17765BF052C3F21EF3AA1A1698EC1188D412242536B') {
    throw 'Cursor texture does not match the STS1 PC reference.'
}
$env:DOTNET_CLI_HOME = Join-Path $repoRoot '.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$restoreConfig = Join-Path $repoRoot 'NuGet.Config'
$modRestoreConfig = if ($ReferenceAssemblies) { Join-Path $repoRoot 'NuGet.CI.Config' } else { $restoreConfig }
$project = Join-Path $repoRoot 'src/TouchSts2/TouchSts2.csproj'
$buildArgs = @('build', $project, '-c', 'Release', "-p:GameDir=$GameDir", "-p:RestoreConfigFile=$modRestoreConfig", "-p:UseReferenceAssemblies=$($ReferenceAssemblies.IsPresent.ToString().ToLowerInvariant())")
if ($ReferenceAssemblies) { $buildArgs += '-p:RestoreLockedMode=true' }
if ($GameDataDir) { $buildArgs += "-p:GameDataDir=$GameDataDir" }
if ($NoRestore) { $buildArgs += '--no-restore' }
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed.' }
$testBuildArgs = @('build', (Join-Path $repoRoot 'tests/TouchSts2.Tests/TouchSts2.Tests.csproj'), '-c', 'Release', "-p:RestoreConfigFile=$restoreConfig")
if ($NoRestore) { $testBuildArgs += '--no-restore' }
& dotnet @testBuildArgs
if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
$testArgs = @((Join-Path $repoRoot 'tests/TouchSts2.Tests/bin/Release/net9.0/TouchSts2.Tests.dll'))
if (!$ReferenceAssemblies) {
    if (!$GameDataDir) { $GameDataDir = Join-Path $GameDir 'data_sts2_windows_x86_64' }
    $testArgs += $GameDataDir, (Join-Path $repoRoot 'src/TouchSts2/bin/Release/net9.0/TouchSts2.dll')
} else {
    Write-Host 'Reference build: runtime game API checks require installed game assemblies and are not run here.'
}
& dotnet @testArgs
if ($LASTEXITCODE -ne 0) { throw 'Gesture tests failed.' }
if ($Package) {
    $output = Join-Path $repoRoot 'dist/TouchSts2'
    New-Item -ItemType Directory -Force -Path $output | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'src/TouchSts2/bin/Release/net9.0/TouchSts2.dll') -Destination $output -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot 'TouchSts2.json') -Destination $output -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot 'docs/PLAYTEST.md') -Destination $output -Force
    $version = (Get-Content -LiteralPath (Join-Path $repoRoot 'TouchSts2.json') -Raw | ConvertFrom-Json).version
    if ($env:GITHUB_REF_TYPE -eq 'tag' -and $env:GITHUB_REF_NAME -ne "v$version") {
        throw 'Release tag must match the manifest version.'
    }
    $packagePath = Join-Path $repoRoot "dist/TouchSts2-$version.zip"
    Compress-Archive -Path $output -DestinationPath $packagePath -Force
    & dotnet run --project (Join-Path $repoRoot 'tests/TouchSts2.PackageChecks/TouchSts2.PackageChecks.csproj') -c Release -- $packagePath (Join-Path $repoRoot 'TouchSts2.json') (Join-Path $repoRoot 'src/TouchSts2/Localization/strings.json')
    if ($LASTEXITCODE -ne 0) { throw 'Package verification failed.' }
    Write-Host "Package: $packagePath"
}
