#requires -Version 7.2
param(
    [switch]$DryRun,
    [switch]$Create,
    [ValidatePattern('^[1-9][0-9]{0,19}$')][string]$ItemId,
    [ValidateSet('keep', 'private', 'public', 'unlisted', 'friends_only')][string]$Visibility = 'keep',
    [string]$ChangeNote = '',
    [string]$PackagePath = ''
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repoRoot 'workshop'
$savedIdPath = Join-Path $source 'mod_id.txt'
if (!$ItemId -and (Test-Path -LiteralPath $savedIdPath)) { $ItemId = (Get-Content -LiteralPath $savedIdPath -Raw).Trim() }
if ($ItemId -and ($ItemId -notmatch '^[1-9][0-9]{0,19}$' -or ![ulong]::TryParse($ItemId, [ref]([ulong]0)))) { throw 'Invalid Workshop item ID.' }
if ($Create -and $ItemId) { throw 'An item ID already exists. Omit -Create to update it.' }
if (!$DryRun -and !$Create -and !$ItemId) { throw 'First upload: use -Create. To update an existing item, use -ItemId or workshop/mod_id.txt.' }
if (!$DryRun -and !$IsWindows) { throw 'Uploading uses the official Windows x64 uploader. DryRun works on any build platform.' }

Push-Location $repoRoot
try {
    if (!$PackagePath) {
        & (Join-Path $PSScriptRoot 'build.ps1') -ReferenceAssemblies -Package
        $version = (Get-Content TouchSts2.json -Raw | ConvertFrom-Json).version
        $PackagePath = Join-Path $repoRoot "dist/TouchSts2-$version.zip"
    }
    $PackagePath = (Resolve-Path -LiteralPath $PackagePath).Path
    $env:DOTNET_CLI_HOME = Join-Path $repoRoot '.dotnet'
    & dotnet run --project tests/TouchSts2.PackageChecks -c Release "-p:RestoreConfigFile=$repoRoot/NuGet.Config" -- $PackagePath TouchSts2.json src/TouchSts2/Localization/strings.json
    if ($LASTEXITCODE -ne 0) { throw 'Package verification failed; nothing was uploaded.' }

    $cover = Get-Item -LiteralPath (Join-Path $repoRoot 'media/workshop-cover.png')
    $previews = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'media/workshop-previews') -File | Sort-Object Name)
    if ($previews.Count -ne 5) { throw 'Expected three gameplay GIFs and two settings screenshots.' }
    foreach ($file in @($cover) + $previews) {
        if ($file.Length -eq 0 -or $file.Length -ge 1000000) { throw "Workshop image must be nonempty and smaller than 1 MB: $($file.Name)" }
    }
    # Fresh staging avoids stale content and preserves previous upload logs/IDs on failure.
    $workspace = Join-Path $repoRoot ('dist/workshop-' + [Guid]::NewGuid().ToString('N'))
    $content = New-Item -ItemType Directory -Path (Join-Path $workspace 'content')
    $previewDir = New-Item -ItemType Directory -Path (Join-Path $workspace 'previews')
    $zip = [IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        foreach ($entry in $zip.Entries) {
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $content.FullName $entry.Name))
        }
    } finally { $zip.Dispose() }
    Copy-Item -LiteralPath $cover.FullName -Destination (Join-Path $workspace 'image.png')
    foreach ($preview in $previews) { Copy-Item -LiteralPath $preview.FullName -Destination $previewDir.FullName }
    $metadata = Get-Content -LiteralPath (Join-Path $source 'workshop.json') -Raw | ConvertFrom-Json -AsHashtable
    $metadata.description = (Get-Content -LiteralPath (Join-Path $source 'description.bbcode') -Raw).Trim()
    if (!$metadata.title -or !$metadata.description) { throw 'Workshop title and description must be nonempty.' }
    if ($Visibility -ne 'keep') { $metadata.visibility = $Visibility }
    elseif (!$ItemId) { $metadata.visibility = 'private' }
    else { $null = $metadata.Remove('visibility') }
    $metadata.changeNote = if ($ChangeNote) { $ChangeNote } else { 'Touch-STS2 ' + (Get-Content TouchSts2.json -Raw | ConvertFrom-Json).version }
    $metadata | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $workspace 'workshop.json') -Encoding utf8NoBOM
    if ($ItemId) { Set-Content -LiteralPath (Join-Path $workspace 'mod_id.txt') -Value $ItemId -Encoding utf8NoBOM }
    Write-Host "Workshop workspace: $workspace"
    Write-Host "Preview order: $($previews.Name -join ', ')"
    if ($DryRun) { Write-Host 'Dry run complete. Steam was not contacted.'; return }

    if (!(Get-Process -Name steam -ErrorAction SilentlyContinue)) { throw 'Start Steam and sign in with the publishing account, then run this command again.' }
    $toolVersion = 'v0.2.0'
    $expectedHash = '2b55c19cc5932235ca9dbd663ca07d04e5d5a0018402303199f9b4ec8ca06578'
    $cache = New-Item -ItemType Directory -Force -Path (Join-Path $repoRoot 'dist/tools')
    $archive = Join-Path $cache.FullName "ModUploader-$toolVersion-win-x64.zip"
    if (!(Test-Path -LiteralPath $archive)) {
        Invoke-WebRequest -Uri "https://github.com/megacrit/sts2-mod-uploader/releases/download/$toolVersion/ModUploader-win-x64.zip" -OutFile $archive
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Official uploader checksum mismatch. Remove the cached archive and retry.' }
    $toolDir = Join-Path $workspace 'uploader'
    Expand-Archive -LiteralPath $archive -DestinationPath $toolDir
    $executables = @(Get-ChildItem -LiteralPath $toolDir -Recurse -Filter 'ModUploader.exe')
    if ($executables.Count -ne 1) { throw 'Expected exactly one official uploader executable.' }
    Push-Location $executables[0].DirectoryName
    try {
        & $executables[0].FullName upload -w $workspace
        if ($LASTEXITCODE -ne 0) { throw "Steam upload failed. Inspect the uploader log in $toolDir before retrying a creation." }
    } finally { Pop-Location }
    $publishedId = (Get-Content -LiteralPath (Join-Path $workspace 'mod_id.txt') -Raw).Trim()
    if ($publishedId -notmatch '^[1-9][0-9]{0,19}$') { throw 'Uploader returned an invalid item ID.' }
    Set-Content -LiteralPath $savedIdPath -Value $publishedId -Encoding utf8NoBOM
    Write-Host "Uploaded: https://steamcommunity.com/sharedfiles/filedetails/?id=$publishedId"
    Write-Host 'Item ID saved to workshop/mod_id.txt. Future runs update this item.'
} finally { Pop-Location }
