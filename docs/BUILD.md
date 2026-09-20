# Build and repository conventions

## Requirements

- .NET SDK 9.0.308 or a newer 9.0.3xx patch, as selected by `global.json`.
- STS2 v0.111.0 game assemblies: `sts2.dll`, `GodotSharp.dll`, and `0Harmony.dll`.
- STS1 PC's `desktop-1.0.jar`, containing the original touch cursor texture.

The project has no NuGet package or additional mod dependencies. Game assemblies are referenced without being copied into the package.

## Build

Run the repository's build script:

```powershell
./scripts/build.ps1 -Package
```

For installations that differ from the script defaults, supply `-GameDir` or `-GameDataDir` for STS2 and `-Sts1Jar` for the STS1 archive. After the first restore, `-NoRestore` uses existing dependencies.

The script builds the DLL, runs the offline checks against the game assemblies, and optionally creates a versioned ZIP package. It does not start the game or install the mod.

The original cursor texture is extracted during the build and embedded in the DLL. Running the mod does not require the STS1 archive. Extracted textures, decompiled sources, and game assemblies are excluded from source control.

## Validation

The console test project checks gesture policy, localization, cursor timing, and the embedded cursor hash. With game assemblies supplied, it also resolves the exact patch targets and private fields used by the runtime. These checks do not establish that physical touch input or multiplayer works in-game.

See [testing](TESTING.md) for recorded results and remaining device checks, and [architecture](ARCHITECTURE.md) for the runtime boundaries.

## Repository conventions

Use English for source comments, documentation, test descriptions, and commit messages. Non-English strings belong in the localization resource or the localization test fixtures. Keep exact translated expectations in those fixtures instead of hiding them as Unicode escapes in general-purpose source files.

Keep the README focused on the project overview and entry points. Record release changes in [CHANGELOG.md](../CHANGELOG.md). Keep technical documentation limited to the [player guide](PLAYTEST.md), this build guide, [architecture](ARCHITECTURE.md), [STS1 reference](STS1_REFERENCE.md), and [testing](TESTING.md).
