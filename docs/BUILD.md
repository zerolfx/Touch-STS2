# Build, CI, and release

## Build options

Use .NET SDK 9.0.308 or a newer 9.0.3xx patch, as selected by `global.json`. The 845-byte STS1 cursor texture is included in the repository and embedded unchanged. No STS1 installation or CI secret is needed.

To build without a game installation:

```powershell
./scripts/build.ps1 -ReferenceAssemblies -Package
```

This restores `Book.StS2.RefLib` at exactly `0.111.0-beta` using a checked-in NuGet lock file. Only its compile assets are used; package build targets, runtime assets, and access-check bypass attributes are not imported. Reference assemblies are not included in the installation package. The package publisher documents that the stripped references are published with Mega Crit's permission: [package information](https://www.nuget.org/packages/Book.StS2.RefLib/0.111.0-beta).

To also validate the runtime patch contracts against installed STS2 v0.111.0 assemblies:

```powershell
./scripts/build.ps1 -Package
```

Supply `-GameDir` or `-GameDataDir` when the game installation differs from the script defaults. After restoring the selected build mode, `-NoRestore` reuses its existing dependencies. Restore again when switching between installed assemblies and reference assemblies.

The script does not start the game or install the mod. There are no additional runtime mod dependencies. Game assemblies and decompiled sources remain excluded from source control and packages.

## GitHub Actions

The **Build and verify** workflow runs on pushes to `main`, `v*` tags, pull requests, and manual dispatch. One GitHub-hosted Ubuntu runner builds the platform-independent managed DLL using the reference-build command, with pinned action commits, a read-only repository token, and no secrets. Compiling on multiple runner operating systems would not establish additional in-game compatibility.

The job:

1. Compiles the mod using the locked reference dependency.
2. Runs 417 gesture, 102 localization, 28 cursor, and 23 pending confirmation checks. Installed RitsuLib binding checks are run locally with the optional plugin assemblies.
3. Creates the versioned installation ZIP.
4. Inspects the ZIP and its managed DLL without executing the mod.
5. Checks that deliberately malformed packages are rejected.
6. Runs simulated input and hover checks in a checksum-pinned Linux Godot .NET 4.5.1 engine, using the same hover and input-ownership source files as the mod.
7. Stages the bilingual Workshop description and versioned change notes, cover, three GIFs, and two settings screenshots in dry-run mode without contacting Steam.
8. Uploads the verified ZIP, its SHA-256 checksum, and a JSON verification report for 14 days.

Download artifacts from the workflow run's summary. The outer Actions download contains the versioned installation ZIP and its two verification files. The installation ZIP contains only the mod DLL, matching manifest, and player guide inside a single mod folder.

A tag build requires the tag to equal `v` followed by the manifest version. CI does not create a GitHub Release or publish to Steam Workshop automatically.

## Artifact validation

The verifier requires an exact ZIP file allowlist. It rejects extra game DLLs, reference assemblies, duplicate entries, unexpected paths, missing files, and an empty player guide. It compares the packaged manifest with the checkout, validates its ID and payload flags, and requires the ZIP filename, manifest version, and assembly version to agree.

PE metadata is inspected without loading the DLL. The verifier checks assembly identity and game references, exact embedded localization bytes, all 16 language tables, and the original cursor SHA-256. The JSON report records the commit when available, package hash, and each packaged file's hash. Checksums detect changed files; they are not a signature or proof of runtime correctness.

Negative fixtures cover an extra DLL, duplicate entry, path traversal, missing DLL, substituted manifest, and empty guide. Run those checks after packaging with the repository's `test-package-validation.ps1` script and its `-PackagePath` parameter.

Reference-only CI cannot execute the game's patch-contract reflection checks. Before release, build against the installed game assemblies to resolve the 25 patch targets and associated private fields. The cloud build and a successful contract check still do not replace in-game tests. See [testing](TESTING.md).

## Steam Workshop release

Use [upload-workshop.ps1](../scripts/upload-workshop.ps1) from PowerShell 7.2 or newer. Uploading requires Windows x64, the SDK above, and Steam running under the publishing account. The script downloads Mega Crit's [official uploader v0.2.0](https://github.com/megacrit/sts2-mod-uploader/releases/tag/v0.2.0), verifies its pinned SHA-256, and reuses the Steam client session. No runner or stored Steam password is needed.

Build, validate, and inspect a staged workspace without uploading:

```powershell
./scripts/upload-workshop.ps1 -DryRun
```

Create the Workshop item with private visibility:

```powershell
./scripts/upload-workshop.ps1 -Create
```

The successful upload saves the ID in `workshop/mod_id.txt`. Keep this file so the ordinary command updates the same item:

```powershell
./scripts/upload-workshop.ps1
```

Each version needs nonempty bilingual release notes in `workshop/changelog/<version>.bbcode`. The script publishes them to the Workshop Change Notes tab automatically and checks them during dry runs. Use `-ChangeNote` to override the text for a metadata-only update.

Use `-ItemId` to adopt an existing item, `-Visibility public` to publish it, or `-PackagePath` to upload an already verified ZIP instead of rebuilding. Updates preserve visibility unless explicitly changed. If a creation fails after Steam has allocated an item, check the uploader log for that ID and retry with `-ItemId`; do not create a duplicate. Steam may require acceptance of the Workshop agreement before an item becomes visible.

Repository assets:

| File | Purpose |
|---|---|
| [workshop.json](../workshop/workshop.json) | Title, tags, and required-item metadata |
| [description.bbcode](../workshop/description.bbcode) | Matching English and Chinese descriptions, with source and issue links |
| [changelog](../workshop/changelog) | Versioned bilingual notes for the Workshop Change Notes tab |
| [workshop-cover.png](../media/workshop-cover.png) | Generated tablet and handheld touchscreen illustration |
| [workshop-previews](../media/workshop-previews) | Combat, card rewards, shop confirmation, English settings, then Chinese settings |
| [media](../media) | Larger GIFs for the project overview |

The cover, compact GIFs, and settings screenshots are each smaller than 1 MB, matching the [official uploader requirements](https://github.com/megacrit/sts2-mod-uploader/blob/main/template/README.md). The script stages the exact verified DLL, manifest, and guide directly under `content`; presentation assets stay outside the installed payload. The uploader reconciles additional previews by filename, so the five repository previews replace any other additional previews on the item. The original STS1 cursor asset's attribution remains documented with that asset.

Upload workspaces and logs remain under the ignored build-output directory. Neither the local dry run nor GitHub Actions publishes anything. Workshop branch compatibility is managed on the item page as recommended by the official template.

## Repository conventions

Use English for source comments, documentation, test descriptions, and commit messages. Non-English strings belong in localization resources or localization test fixtures; the Workshop title, bilingual description, and change notes are also localized publication content. Keep exact translated expectations in those fixtures instead of hiding them as Unicode escapes in general-purpose source files.

Keep the README focused on the project overview. Record release changes in [CHANGELOG.md](../CHANGELOG.md). The technical documentation consists of the [player guide](PLAYTEST.md), this build guide, [architecture](ARCHITECTURE.md), [STS1 reference](STS1_REFERENCE.md), and [testing](TESTING.md).
