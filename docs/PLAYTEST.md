# Player guide

## Setup and settings

Install the packaged `TouchSts2` folder using the game's mod installation procedure, then restart the game. The package contains the mod DLL, matching manifest, and this guide; it requires no additional mods.

Accept the game's initial mod-loading warning with a mouse or controller. Enabling mods uses a separate modded save tree; the game copies the original saves on the first modded launch.

Input settings provide **Touchscreen mode**, **Hold to inspect**, and **Touch feedback**, translated into the current game language. Touchscreen mode defaults to off and changes immediately. The two optional aids default to on. Preferences are stored in `TouchSts2.cfg` and apply across save slots.

While enabled, mouse input follows the same rules as touch input. Mouse movement does not disable the preference. Controller input temporarily suspends it; clicking restores it. This also supports platforms that expose touches only as mouse events.

## Combat

- Drag a targeted card into the play area: the card stays at the lower center while the targeting arrow follows the pointer. Release over a legal target to play.
- Untargeted cards lift on pickup, then retain the grab offset and move one-to-one with the pointer. Release in the play area to play.
- A first stationary tap selects a card for inspection. Tap a legal target or the play area to submit it.
- Drag back to the bottom or release outside the play area on a subsequent gesture to cancel. Tap another card to switch selection.
- Ally targets can be selected through their battlefield characters or the left party sidebar, subject to the card's native target restrictions.
- Context changes, loss of focus, and controller handoff cancel the owned gesture. Native validation still rejects unplayable cards.
- Numeric shortcuts retain native card-play behavior. Touch overrides apply only to controllers created for a touch gesture.

## Selections and browsing

Card rewards, generic card/relic choices, shop purchases, removal services, and rest-site options use selection followed by a native confirmation button. Existing skip and other reward controls remain available. Selecting another item replaces the pending choice; tapping empty space or pressing Escape clears it.

Deck removal, upgrade, transform, enchantment, and bundle selection keep their existing native preview and confirmation screens. This includes removal initiated by events and relics, not just shops. Selection counts, mandatory choices, and effect-specific confirmation rules remain native.

Drag lists from their entries to scroll. Once the drag threshold is crossed, that gesture cannot also select an item, even if the pointer returns to its starting point. The timeline scrolls horizontally. Map scrolling and node selection keep their native rules.

## Previews and feedback

Hold a supported card or special card reward still for 0.55 seconds to open its native preview. Releasing after inspection does not claim a reward; use a separate short tap to claim it.

Relics, powers, intents, and teammate details reuse existing mouse previews. Stolen-card information is shown only where the native game exposes it. While choosing a card or potion target, native rules may suppress some tips.

Touch feedback uses STS1 PC's original orb texture and fade formula. It fades even while held and does not restart on release or internal click replay. It can be disabled independently.

## Compatibility

The implementation targets STS2 v0.111.0. Earlier releases were tested in-game using mouse input, not injected or physical touch events. Releases 0.2.1 through 0.2.4 received offline checks only.

Physical touch, Windows long-press promotion, multiple fingers, Steam Deck/gamescope, controller hardware, and multiplayer require further device testing. See [testing](TESTING.md) for exact evidence and the manual checklist.

## Diagnostics

After exiting the game, enable input logging in the configuration file:

```ini
[touch]
enabled=true
diagnostics=true
```

`[TouchSts2]` logs patch installation, game/engine versions, display backend, DPI, emulation settings, viewport size, selection, release intent, native completion, and pointer parking. `[TouchSts2:input]` is limited to 40 entries per second and includes event type, device, finger index, and coordinates.

A submission that remains incomplete for three seconds is reported and canceled. Patch-resolution failures roll back installation; runtime failures disable touch overrides and log the exception. Include the game version, hardware/display backend, minimal reproduction steps, and matching logs when reporting a problem.
