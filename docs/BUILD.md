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
2. Runs 417 gesture, 102 localization, and 28 cursor checks.
3. Creates the versioned installation ZIP.
4. Inspects the ZIP and its managed DLL without executing the mod.
5. Checks that deliberately malformed packages are rejected.
6. Uploads the verified ZIP, its SHA-256 checksum, and a JSON verification report for 14 days.

Download artifacts from the workflow run's summary. The outer Actions download contains the versioned installation ZIP and its two verification files. The installation ZIP contains only the mod DLL, matching manifest, and player guide inside a single mod folder.

A tag build requires the tag to equal `v` followed by the manifest version. CI does not create a GitHub Release or publish to Steam Workshop automatically.

## Artifact validation

The verifier requires an exact ZIP file allowlist. It rejects extra game DLLs, reference assemblies, duplicate entries, unexpected paths, missing files, and an empty player guide. It compares the packaged manifest with the checkout, validates its ID and payload flags, and requires the ZIP filename, manifest version, and assembly version to agree.

PE metadata is inspected without loading the DLL. The verifier checks assembly identity and game references, exact embedded localization bytes, all 16 language tables, and the original cursor SHA-256. The JSON report records the commit when available, package hash, and each packaged file's hash. Checksums detect changed files; they are not a signature or proof of runtime correctness.

Negative fixtures cover an extra DLL, duplicate entry, path traversal, missing DLL, substituted manifest, and empty guide. Run those checks after packaging with the repository's `test-package-validation.ps1` script and its `-PackagePath` parameter.

Reference-only CI cannot execute the game's patch-contract reflection checks. Before release, build against the installed game assemblies to resolve the 18 patch targets and associated private fields. The cloud build and a successful contract check still do not replace in-game tests. See [testing](TESTING.md).

## Steam Workshop release

Use Mega Crit's [official mod uploader](https://github.com/megacrit/sts2-mod-uploader). Its [workspace template guide](https://github.com/megacrit/sts2-mod-uploader/blob/main/template/README.md) documents the metadata fields and image limits.

Before the first public release:

- Choose the release version and keep the project, mod manifest, release tag, and changelog consistent.
- Run the installed-game contract checks and the key manual scenarios, including the latest confirmation UI and cursor changes. Keep untested hardware and co-op limitations explicit.
- Prepare the Workshop title, description, tags, visibility, and change note. The current mod has no required mod dependencies. Keep the input-settings activation instructions and modded-save behavior in the description.
- Create the required `image.png` cover, smaller than 1 MB. Optional additional preview images must also meet the uploader's size limit.
- Retain attribution for the included STS1 cursor asset. Its provenance is documented with the asset; it is not original project artwork.
- Sign into Steam with the publishing account and accept Steam's Workshop agreement if requested.

Run the uploader once to create a workspace. Put the verified mod payload in its `content` folder, fill out `workshop.json`, and replace the template cover. Start with private visibility to test subscription installation before making the item public. Upload with the uploader's `upload -w` command. Preserve the generated `mod_id.txt`; subsequent uploads use it to update the same item rather than creating another one.

The Workshop metadata is separate from the game's `TouchSts2.json` manifest. The official template notes inconsistent behavior for its branch metadata fields and recommends editing those on the Workshop page. No Workshop item is created by this repository's build workflow.

## Repository conventions

Use English for source comments, documentation, test descriptions, and commit messages. Non-English strings belong in the localization resource or localization test fixtures. Keep exact translated expectations in those fixtures instead of hiding them as Unicode escapes in general-purpose source files.

Keep the README focused on the project overview. Record release changes in [CHANGELOG.md](../CHANGELOG.md). The technical documentation consists of the [player guide](PLAYTEST.md), this build guide, [architecture](ARCHITECTURE.md), [STS1 reference](STS1_REFERENCE.md), and [testing](TESTING.md).
