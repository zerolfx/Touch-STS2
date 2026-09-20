param([Parameter(Mandatory)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$checker = Join-Path $repoRoot 'tests/TouchSts2.PackageChecks/bin/Release/net9.0/TouchSts2.PackageChecks.dll'
$manifest = Join-Path $repoRoot 'TouchSts2.json'
$strings = Join-Path $repoRoot 'src/TouchSts2/Localization/strings.json'
$scratch = Join-Path ([IO.Path]::GetTempPath()) ("TouchSts2-package-checks-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$fixtures = @{
    'extra-dll' = 'exactly the mod DLL'
    'duplicate-entry' = 'exactly the mod DLL'
    'path-traversal' = 'exactly the mod DLL'
    'missing-dll' = 'exactly the mod DLL'
    'wrong-manifest' = 'Packaged manifest differs'
    'empty-guide' = 'Player guide is empty'
}
foreach ($case in $fixtures.Keys) {
    $folder = Join-Path $scratch $case
    New-Item -ItemType Directory -Path $folder | Out-Null
    $archivePath = Join-Path $folder (Split-Path $PackagePath -Leaf)
    Copy-Item -LiteralPath $PackagePath -Destination $archivePath
    $zip = [IO.Compression.ZipFile]::Open($archivePath, [IO.Compression.ZipArchiveMode]::Update)
    try {
        switch ($case) {
            'extra-dll' { $null = $zip.CreateEntry('TouchSts2/sts2.dll') }
            'duplicate-entry' { $null = $zip.CreateEntry('TouchSts2/TouchSts2.json') }
            'path-traversal' { $null = $zip.CreateEntry('TouchSts2/../unexpected.txt') }
            'missing-dll' { $zip.GetEntry('TouchSts2/TouchSts2.dll').Delete() }
            'wrong-manifest' {
                $zip.GetEntry('TouchSts2/TouchSts2.json').Delete()
                $entry = $zip.CreateEntry('TouchSts2/TouchSts2.json')
                $writer = [IO.StreamWriter]::new($entry.Open())
                try { $writer.Write('{}') } finally { $writer.Dispose() }
            }
            'empty-guide' {
                $zip.GetEntry('TouchSts2/PLAYTEST.md').Delete()
                $null = $zip.CreateEntry('TouchSts2/PLAYTEST.md')
            }
        }
    } finally { $zip.Dispose() }
    $diagnostic = & dotnet $checker $archivePath $manifest $strings 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0 -or $diagnostic -notmatch [regex]::Escape($fixtures[$case])) {
        throw "Verifier did not reject $case for the expected reason: $diagnostic"
    }
    if (Test-Path -LiteralPath ($archivePath + '.verification.json')) {
        throw "Rejected package unexpectedly has a success report: $case"
    }
    Write-Host "PASS: package verifier rejects $case."
    # Remove only known fixture files; no recursive cleanup of computed paths.
    Remove-Item -LiteralPath $archivePath
    Remove-Item -LiteralPath $folder
}
Remove-Item -LiteralPath $scratch
# Every verifier process above fails intentionally. Do not leak its exit code to
# the workflow shell after all expected rejection checks have passed.
$global:LASTEXITCODE = 0
