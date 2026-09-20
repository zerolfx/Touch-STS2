# Testing and remaining checks

## Evidence levels

Offline state-machine tests, API compatibility checks, mouse-driven game tests, and physical touch tests establish different things. A method still existing in a game assembly does not prove that its event timing or behavior remains compatible.

The recorded game baseline is Windows STS2 v0.111.0, MegaDot 4.5.1-m.14, and .NET 9. Historical runtime tests used isolated test saves and mouse/keyboard input. They did not inject `InputEventScreenTouch` or `InputEventScreenDrag`, and they were not hardware touch tests.

## Current offline checks

Version 0.2.5 builds in Release with zero warnings and zero errors. The checks cover:

| Group | Coverage |
|---|---|
| 417 gesture checks | Four viewport sizes; release policy, cancellation, card switching, positioning, drag/hold arbitration, and legal-target inputs outside the ordinary play area |
| 102 localization checks | All 16 game languages, nonempty labels, exact locale fixtures, case handling, and fallback |
| 28 cursor checks | Initial opacity, rotation, pointer following, repeated presses, held fade, release, cleanup, and snap timing at 30/60/144 Hz |
| 67 settings adapter checks | Registration, all translated labels, preserved preferences, real defaults, two-way synchronization, redundant-write suppression, and BaseLib callback dispatch |
| Optional plugin contracts | Installed BaseLib 3.4.7 and ModConfig 0.2.3 public APIs resolve; the BaseLib adapter type can be generated without starting the engine |
| Cursor resource | Embedded texture matches the STS1 PC SHA-256 |
| 18 patch targets | Actual game method signatures and associated mouse, position, scroll, target, and reward-preview contracts resolve |
| Embedded localization | Release DLL contains all 16 language tables |

The co-op policy tests receive target validity as an input. They do not prove real friend/enemy classification or synchronization in a multiplayer room. Native validation calls and source inspection support the implementation, but a real session remains necessary.

The cursor tests establish resource identity and fade-state behavior. They do not establish cross-engine pixel equivalence or identical visibility in every STS1 state; see [the STS1 reference](STS1_REFERENCE.md).

## Continuous integration

GitHub Actions builds the managed DLL on one Ubuntu runner using a pinned, locked compile-only reference package. The job runs the pure checks and inspects the final ZIP's allowlist, manifest, assembly identity/version, embedded translations, and cursor hash. Six malformed-package fixtures verify rejection of unexpected payloads and missing or changed files. Each successful build uploads the installation ZIP, a checksum, and a per-file verification report.

The 18 runtime patch-contract checks require actual installed game assemblies and remain part of the installed-game build. Reference-only CI reports this distinction explicitly. The cloud job does not start the game. See [build and release](BUILD.md) for commands and artifact details.

The Workshop script's dry run verifies the package again and stages the metadata, flat install payload, cover, and three ordered GIFs. Compact previews and the cover are checked against the uploader's 1 MB limit. The actual Steam submission remains untested until the first upload; dry runs never contact Steam.

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
- **Pointer cleanup:** verify neutral synthetic motion clears hover even if the platform ignores the hardware warp, including after overlays appear.
- **Multiplayer:** test host and client submission, party-sidebar targets, disconnects, and action completion timing.
- **Continuous holds:** validate the 0.55-second inspector with physical input and slow frames, including release after the threshold and dragging away.
- **Native UI details:** check dropdowns, credits, timeline scrolling, transform selection, and context cleanup on the real device. Existing source coverage should not be reported as a runtime pass.

Release summaries belong in [CHANGELOG.md](../CHANGELOG.md). Keep new test reports tied to the exact version, input source, display backend, and observed outcomes.
