param(
    [Parameter(Mandatory)][string]$EnginePath,
    [switch]$Graphical,
    [string]$DisplayDriver = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot 'tests/TouchSts2.EngineChecks'
$engine = (Resolve-Path -LiteralPath $EnginePath).Path
& dotnet build (Join-Path $project 'TouchSts2.EngineChecks.csproj') -c Debug
if ($LASTEXITCODE -ne 0) { throw 'Engine test build failed.' }
$arguments = @('--path', $project, '--audio-driver', 'Dummy')
if (!$Graphical) { $arguments += '--headless' }
if ($DisplayDriver) { $arguments += '--display-driver', $DisplayDriver }
if ($IsWindows) {
    $output = New-Item -ItemType Directory -Force (Join-Path $repoRoot 'dist/engine-checks')
    $stdout = Join-Path $output 'stdout.log'
    $stderr = Join-Path $output 'stderr.log'
    # Wait for GUI executables as well as console builds of the engine.
    $arguments[1] = '"' + $project + '"'
    $process = Start-Process -FilePath $engine -ArgumentList $arguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    if (!$process.WaitForExit(60000)) {
        $process.Kill()
        throw 'Engine checks timed out.'
    }
    Get-Content -LiteralPath $stdout, $stderr
    if ($process.ExitCode -ne 0) { throw 'Engine checks failed.' }
} else {
    & timeout --kill-after=5s 60 $engine @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Engine checks failed or timed out.' }
}
