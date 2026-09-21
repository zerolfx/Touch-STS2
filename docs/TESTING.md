# Testing and remaining checks

## Evidence levels

Offline state-machine tests, API compatibility checks, mouse-driven game tests, and physical touch tests establish different things. A method still existing in a game assembly does not prove that its event timing or behavior remains compatible.

The recorded game baseline is Windows STS2 v0.111.0, MegaDot 4.5.1-m.14, and .NET 9. Historical runtime tests used isolated test saves and mouse/keyboard input. They did not inject `InputEventScreenTouch` or `InputEventScreenDrag`, and they were not hardware touch tests.

## Current offline checks

Version 0.2.10 builds in Release with zero warnings and zero errors. The checks cover:

| Group | Coverage |
|---|---|
| 417 gesture checks | Four viewport sizes; release policy, cancellation, card switching, positioning, drag/hold arbitration, and legal-target inputs outside the ordinary play area |
| 102 localization checks | All 16 game languages, nonempty labels, exact locale fixtures, case handling, and fallback |
| 28 cursor checks | Initial opacity, rotation, pointer following, repeated presses, held fade, release, cleanup, and snap timing at 30/60/144 Hz |
| 73 installed RitsuLib checks | Actual page registration, 16-language labels, preserved preferences, live reads/writes, default resets, no duplicate save, and page replacement |
| 23 pending confirmation checks | Preview has no gameplay side effects; replacement, cancellation, invalidation, cleanup reentrancy, and repeated/failed submission |
| Optional settings integration | Installed RitsuLib public APIs execute without starting Godot; absence of the plugin leaves native settings available |
| Cursor resource | Embedded texture matches the STS1 PC SHA-256 |
| 25 patch targets | Actual game method signatures and associated mouse, position, scroll, target, reward-preview, treasure, simple-grid, event, and Crystal Sphere contracts resolve |
| Embedded localization | Release DLL contains all 16 language tables |

The 0.2.6 treasure fix reuses the existing confirmation lifecycle. Source inspection verifies that staging does not call the native vote method, and the added patch and private fields resolve against the installed game. This is not a live multiplayer result.

The 0.2.7 event changes have source/API checks and engine-free confirmation lifecycle tests only. The latter simulate validity changes; they do not execute Godot controls, card-grid callbacks, or Crystal Sphere animations. In-game acceptance must cover fixed and variable card counts, revising choices, ancient gifts, event holds with the setting on/off, and Crystal Sphere area preview, tool changes, cancellation, duplicate release callbacks, reveal timing, and final-use completion. Verify ordinary shared-event short taps still vote directly.

The co-op policy tests receive target validity as an input. They do not prove real friend/enemy classification or synchronization in a multiplayer room. Native validation calls and source inspection support the implementation, but a real session remains necessary.

The cursor tests establish resource identity and fade-state behavior. They do not establish cross-engine pixel equivalence or identical visibility in every STS1 state; see [the STS1 reference](STS1_REFERENCE.md).

## Optional settings integration

The 0.2.9 integration replaces both older configuration adapters with RitsuLib's public settings API. The 73 local binding checks execute the installed settings assembly, not a mocked API. They do not render its menu. Check that Touch-STS2 appears under Mod Settings after restarting, that it no longer registers with ModConfig or BaseLib, and that native and plugin settings agree. These local checks require the Workshop content directory as the test executable's third argument; CI runs the remaining 570 pure checks without optional plugin assemblies.

## 0.2.10 platform simulation

The engine test project links the production `TouchInputOwnership`, `TouchHover`, and card gesture policy directly. It injects real Godot mouse, touch, drag, cancellation, and joypad events. This verifies the shared helpers and engine dispatch, not the full game's patched input order or a physical touchscreen.

Recorded on Godot .NET 4.5.1:

| Environment | Result |
|---|---|
| Windows, headless | 31 checks passed |
| Windows, native window / OpenGL | 37 checks passed |
| Ubuntu 24.04 under WSL 2, headless | 31 checks passed |
| WSLg X11, Mesa llvmpipe | 37 checks passed |
| WSLg Wayland, Mesa llvmpipe | 37 assertions passed, but the final run did not exit cleanly; not counted as a complete pass |
| Linux .NET 9 | All 570 pure checks passed |

An isolated Windows STS2 v0.111.0 startup with Steam disabled loaded 0.2.10 and installed all 25 patches on MegaDot 4.5.1-m.14. This was a headless initialization check, not a new full gameplay acceptance run. Native task/shutdown diagnostics were present; the mod did not report an initialization or runtime failure.

The native-window regression first keeps the OS pointer over a control and sends only a synthetic motion elsewhere. The old fallback leaves the original hover active. Calling the production cleanup clears both the control's hover state and its tooltip without moving the OS pointer; a subsequent input restores normal hover and clicking. This reproduces a conditional, cross-platform fallback defect. It does not establish that a Steam Deck running STS2 actually ignores the original warp request.

Input checks cover a held drag, delayed release, controller press/release and stick movement, fresh controller takeover after completion, return to touch, touch mode disabled, two fingers, canceled touch, and focus-loss state cleanup. Targeted and untargeted card policies still commit once after controller interference. Raw touch with mouse emulation disabled intentionally produces no mouse commands; direct touch-only gameplay remains unsupported rather than silently enabling another input stream.

The local WSL Vulkan device exposes only software rendering and lacks `VK_KHR_external_semaphore_fd`, which the inspected Gamescope 3.16.1 code requires. The distribution also has no Gamescope package candidate. Gamescope was not run. Linux STS2, the game's Linux engine fork, SteamOS, Steam Input device translation, and physical Steam Deck interaction were not tested. Do not describe this as Steam Deck certification or a hardware regression fix.

Run with an official Godot .NET 4.5.1 executable:

```powershell
./scripts/test-engine.ps1 -EnginePath $engine
./scripts/test-engine.ps1 -EnginePath $engine -Graphical
```

Linux graphical runs can select `-DisplayDriver x11` or `-DisplayDriver wayland`. Run graphical tests serially because their windows share focus and the OS pointer. CI uses only the headless suite in the existing Ubuntu job.

## Hover handoff regression

The 0.2.8 fix refreshes native hover immediately after the deferred tap's release, while input replay is still guarded. It sends a motion event with no pressed buttons at the release position. Drag and hold paths do not replay a tap and are unchanged.

An isolated headless test on the installed MegaDot 4.5.1-m.14 engine reproduces a covered card retaining its tooltip after a click opens a preview. Refreshing hover removes that tooltip, activates the new preview's hover, does not click through, and allows the original hover to return when the preview closes. All 8 checks pass. This tests engine dispatch with minimal controls, not the actual upgrade card scene or physical touch hardware.

The engine updates mouse enter/exit before calling the mod's input handler, so swallowing motion alone is not evidence of stale hover. The confirmed gap is the UI replacement after the final input event. This can also occur with an ordinary mouse release; the test does not establish that it is exclusive to the mod. The fix covers the mod's deferred replay without changing tooltip placement.

To repeat with a Godot executable, set `$engine` to its executable and run:

```powershell
Compress-Archive -Path tests/TouchSts2.HoverChecks/project.godot,tests/TouchSts2.HoverChecks/hover_replay.gd -DestinationPath dist/hover-checks.zip -Force
& $engine --headless --main-pack "$PWD/dist/hover-checks.zip" --script res://hover_replay.gd | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Hover checks failed.' }
```

In-game follow-up: select a card with generated-card tips for upgrading, inspect both comparison cards, cancel, and repeat. Also check grid scrolling and long-press inspection. Any overlap that persists with the correct preview owner requires separate layout investigation.

The 0.2.8 menu-cursor change checks the displayed submenu, preserving the native cursor only on the main-menu home page, pause menu, and settings, including dialogs above those pages. Lobbies, the compendium, run history, patch notes, and other subpages retain touch behavior. Combat pointer parking is skipped on the route to settings. Build and existing offline checks cover compatibility only; visually verify the route to disabling touch mode, transitions to other subpages, controller handoff, and returning to combat.

## Continuous integration

GitHub Actions builds the managed DLL on one Ubuntu runner using a pinned, locked compile-only reference package. The job runs the pure checks and inspects the final ZIP's allowlist, manifest, assembly identity/version, embedded translations, and cursor hash. Six malformed-package fixtures verify rejection of unexpected payloads and missing or changed files. Each successful build uploads the installation ZIP, a checksum, and a per-file verification report.

The 25 runtime patch-contract checks require actual installed game assemblies and remain part of the installed-game build. Reference-only CI reports this distinction explicitly. The cloud job does not start the game. See [build and release](BUILD.md) for commands and artifact details.

The Workshop script's dry run verifies the package again and stages the bilingual metadata, flat install payload, cover, three ordered GIFs, and English/Chinese settings screenshots. All previews and the cover are checked against the uploader's 1 MB limit. The official uploader has successfully created and updated the Workshop item. Dry runs never contact Steam. Versioned bilingual change notes are required unless explicitly overridden.

## Historical runtime evidence

### User acceptance, September 20, 2026

The user reported completed in-game acceptance and supplied recordings of combat card play, card reward selection, and shop confirmation. Edited demonstrations are included in the project overview. The input hardware and multiplayer coverage were not specified. The separately reported small settings text and missing plugin entries prompted 0.2.5; those settings changes have not been visually retested in-game.

### 0.1.0

Mouse-driven testing covered loading all 11 initial patches, toggling the preference, and the following combat outcomes:

- First tapping Defend selected it without spending energy or granting block.
- A second tap in the play area played it exactly once.
- Dragging an attack to an enemy submitted once; dragging to empty space canceled without spending energy.
- Selection survived waiting; another card could replace it, and dragging back to the bottom canceled it.
- Tap-card then tap-enemy submitted once. An unaffordable card was rejected by the native game.
- Opening pause cleared selection without delayed play.
- Numeric shortcuts retained the native path while touch mode was enabled.
- Completion cleared card selection and requested the neutral pointer position.

The game was tested in maximized and 1280 by 800 windows, but its logged logical viewport remained 1920 by 1080. This establishes window-coordinate mapping, not runtime coverage of four different logical viewport sizes.

With touch mode disabled, an automated fast drag left a card selected rather than playing it. The same result occurred with mods completely disabled. The test therefore did not establish a mod regression, but native dragging after disabling the mod was not marked as passed.

### 0.1.1

A targeted-card positioning defect was found after the initial tests. The correction anchored aiming at `(960, 820)` for a 1920 by 1080 viewport, matching 260 units above the bottom at scale 1. A mouse-driven attack produced one successful native play. Logs established entry into the anchored state, but sustained visual dragging across the entire screen was not fully tested.

### 0.2.0

An isolated session used native debug controls to reach rewards, shops, and a rest site. The final package loaded all 16 then-current patches. Observed results included:

| Scenario | Observed result |
|---|---|
| Drag from a reward row | No claim or card-selection opening |
| First tap on a reward card | Pending confirmation without adding the card |
| Replace, clear, or cancel reward selection | Only the current choice remained pending |
| Repeated confirmation input | One reward claim or shop purchase |
| Shop item selection and cancellation | Correct native item information; no purchase on cancellation |
| Select relic, potion, then removal service | Previous choices were not purchased |
| Confirm removal service | Native deck selection opened before charging the service fee |
| Drag starting on a deck card | Grid scrolled without opening the preview |
| Remove-card preview, back, then confirm | Removal and payment occurred only after native confirmation |
| First tap on rest, then choose smith | No premature healing; smith entered native upgrade selection |
| Upgrade preview and confirmation | Native before/after comparison and final confirmation |
| Settings controls and scrolling | Preferences persisted; scrolling did not start key rebinding |
| Main menu | Enlarged targets fit the available space and reverted when disabled |
| Map node | Native vote/travel succeeded |
| Final combat regression | Inspect, untargeted play, anchored attack, and cleanup succeeded |

These tests used the old confirmation bar and custom ring. They do not validate the appearance of the native button introduced in 0.2.1 or the orb introduced in 0.2.4. No mod runtime fault, confirmation exception, or submission timeout was observed in that session. Some engine shutdown warnings also occurred in the unmodified baseline and were not attributed to the mod.

### 0.2.1 through 0.2.4

These releases were built and checked offline without starting or operating the game. The following counts were recorded:

| Version | Gesture | Localization | Cursor | Patch targets |
|---|---:|---:|---:|---:|
| 0.2.1 | 363 | 102 | n/a | 16 |
| 0.2.2 | 363 | 102 | n/a | 18 |
| 0.2.3 | 417 | 102 | n/a | 18 |
| 0.2.4 | 417 | 102 | 28 | 18 |

Native confirmation scenes, settings font updates, shared removal flows, party-sidebar targeting, and reward-preview entries were inspected in the game code. Installation artifacts were compared with their corresponding package hashes. These are not substitutes for visual or device acceptance.

## Manual acceptance matrix

| Scenario | Expected result |
|---|---|
| Touch mode disabled | Native mouse, keyboard shortcuts, and controller behavior |
| Attack dragged onto a legal enemy | One play and one energy charge |
| Aiming while moving to the top or sides | Card remains at the lower center; only the arrow follows |
| Attack released over empty space or an invalid/dead target | Cancel without spending energy |
| First stationary tap on an untargeted card | Inspection without playing |
| Untargeted card crossing the drag threshold | Continuous one-to-one movement with no extra jump |
| Select, replace, cancel, or skip a reward | Confirmation follows only the pending choice; existing controls remain usable |
| Switch game language | All three labels and their fonts refresh |
| Wait after inspecting, then select a target | No automatic play or pointer-drift cancellation |
| Switch cards quickly | New card remains selected; no click-through |
| Pause, end the turn, or lose focus | No delayed submission |
| Controller, touch, then controller | Explicit input changes ownership; minor stick drift does not play a card |
| Resize or change aspect ratio | Pointer, target, and card positions stay aligned |
| Long press, multiple fingers, and repeated taps | No duplicate submission; capture platform input events |
| Legal ally through character or party sidebar | Native validation, highlighting, submission, and synchronization |
| Invalid party entry overlapping an enemy | No targeting through the entry |
| Hold a special card reward | Native preview; release does not claim; a later short tap can claim |
| Powers, intents, relics, and stolen-card tips | Only native information appears; target selection is not hijacked |
| Cursor tap, hold, release, and repeated tap | One following orb, fading while held and relighting on a new press |
| Empty stock, changed price, insufficient gold, full potion slots | Native purchase constraints remain authoritative |
| Forced, optional, single, and multiple deck selections | Native counts, preview, back, and skip rules remain intact |

## Platform questions

- **Windows event promotion:** measure whether one physical tap produces duplicate emulated/system mouse presses, and whether press-and-hold produces a right click. Log event type, device, coordinates, and finger index.
- **Steam Deck/gamescope:** record the actual display backend, event types, mouse-emulation setting, and pointer-warp result. A touch may arrive solely as a mouse event; device `-1` cannot be required universally.
- **Engine fork:** upstream Godot behavior is evidence, not proof of MegaDot's exact dispatch order. Observe the shipped engine rather than assuming every upstream detail is unchanged.
- **Early card pickup:** newly drawn holders temporarily disable their hitboxes during animation. Measure whether fast first taps are lost before considering input buffering.
- **Pointer cleanup:** the conditional warp-failure case is covered by the 0.2.10 simulation above. Verify the same cleanup in actual game scenes and after overlays appear on physical hardware.
- **Multiplayer:** test host and client submission, party-sidebar targets, disconnects, and action completion timing. For treasure chests, have every other player vote first: tapping or changing a relic must not send a vote or start distribution; pressing confirm must submit exactly once. Repeat with a single available relic, cancel by tapping blank space or Escape, leave and reopen the screen, and verify invalidated choices cannot submit. Check native single-player, controller, and touch-disabled behavior.
- **Continuous holds:** validate the 0.55-second inspector with physical input and slow frames, including release after the threshold and dragging away.
- **Native UI details:** check dropdowns, credits, timeline scrolling, transform selection, and context cleanup on the real device. Existing source coverage should not be reported as a runtime pass.

Release summaries belong in [CHANGELOG.md](../CHANGELOG.md). Keep new test reports tied to the exact version, input source, display backend, and observed outcomes.
