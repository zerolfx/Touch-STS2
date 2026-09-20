# STS1 PC behavior reference

## Evidence and scope

The reference is the touchscreen branch in STS1 PC's `desktop-1.0.jar`, inspected using CFR 0.152. Method names below identify the code behind each finding. This is source evidence; inferred side effects are distinguished from measured behavior.

STS1's touchscreen preference modifies its mouse-driven interface. It does not replace the input pipeline with a general gesture system. Touch-STS2 follows that interaction model while retaining native STS2 behavior where it already works.

## Preference and input ownership

`Settings` reads the touchscreen preference, defaulting to false. `InputSettingsScreen` exposes **Touchscreen Mode**, and `GiantToggleButton` updates it immediately. The original setting is stored per save slot; Touch-STS2's configuration is global.

Entering controller mode normally disables the active touchscreen flag. `InputHelper.leaveControllerMode()` restores the saved preference on a left or right press. Mouse movement does not disable touchscreen mode. The controller analog threshold in the inspected code is 0.5.

The intended relationship is a saved touch preference with controller input temporarily taking ownership. Some STS1 initialization and pause paths update the two flags inconsistently; Touch-STS2 uses explicit active-state checks rather than reproducing those inconsistencies.

## Pointer and hover

`ScrollInputProcessor` sets press/release latches; coordinates come from `Gdx.input.getX()` and `getY()`. The inspected PC path ignores touch pointer indices and has no general multitouch gesture recognizer.

`InputHelper.moveCursorToNeutralPosition()` moves the pointer to `(10, HEIGHT / 2)` and clears cursor opacity when touchscreen mode is active and controller mode is not. It is used during several interaction-completion and cancellation paths. The midpoint is above the bottom cancellation threshold.

Hover remains the mechanism for ordinary tips. Pointer motion, neutral positioning, and screen changes determine when those tips disappear; it is inaccurate to describe every tooltip as permanently pinned after release.

## Cards

The original coordinates have a bottom-left origin. Touch-STS2 converts them to Godot's top-left viewport coordinates.

| Behavior | STS1 PC reference |
|---|---|
| Pickup | `CardGroup` uses `isHoveredInHand(0.7f)` |
| Retention | Release checks can use `isHoveredInHand(1.0f)` |
| Regrab delay | `releaseCard()` sets a 0.25-second hover lockout |
| Initial inspection | Half the card height: 210 scaled units above the bottom |
| Dragged-card offset | `clickAndDragCards()` places the center 270 scaled units above the pointer during dragging |
| Touch play area | Pointer above 350 scaled units and below `CARD_DROP_END_Y`; a hovered monster also satisfies the target condition |
| Upper play limit | `CARD_DROP_END_Y` is initialized to 0.81 of screen height |
| Targeted aiming | Card switches to the horizontal center, 260 scaled units above the bottom, at draw scale 1 |
| Bottom cancellation | 50 scaled units, with the relevant drag/aim state guards |

The 270-unit offset belongs to the dragging phase. Once a targeted card enters `inSingleTargetMode`, `updateSingleTargetInput()` replaces the drag-follow path. Continuing to raise the card with the finger during aiming is not the reference behavior.

The touchscreen release branch increments `touchscreenInspectCount`. A release in a valid play context may submit the card; otherwise, the first release retains inspection and a subsequent release can cancel or transfer selection. The count represents releases, not separate inspection commands. The original mouse release branch explicitly excludes touchscreen mode.

`releaseCard()` clears the current input edges as well as selection, preventing cancellation from clicking through to another element. Other cancellation paths include right click, the bottom boundary, unavailable targets, opening a pile, and ending the turn.

In the inspected single-target path, pointer interpolation toward a position beyond the bottom can eventually cross a cancellation boundary. The timing depends on platform and frame behavior and has not been measured here. Touch-STS2 deliberately avoids pointer-drift cancellation so tap-to-inspect remains usable. It also resets gesture state for each card instead of copying uncertain release-counter carryover.

Touch-STS2 retains native STS2 hitboxes rather than replacing the hand's full geometry with STS1's pickup/retention boxes. For untargeted cards it preserves the grab offset and one-to-one movement after lifting on press, preventing a second jump at the drag threshold. These are integration choices, not claims that every STS1 internal coordinate operation was copied.

## Touch cursor

`GameCursor` normally draws the gold pointer. In its touchscreen branch it uses `ImageMaster.WOBBLY_ORB_VFX`, loaded from the `orb.png` asset. The original 845-byte image is included unchanged and embedded during the build.

| Property | Reference |
|---|---|
| Source region | `(0, 0, 32, 32)` |
| Origin and size | Origin `(16, 16)`; size `32 * Settings.scale` |
| Position | Centered at the current pointer; the extra touch offsets cancel |
| Tint and sampling | White modulation; linear minification and magnification |
| New press | `InputHelper` resets alpha to `0.7` |
| Fade | `alpha += (0 - alpha) * (delta * 3)`; snap to zero below absolute alpha `0.01` |
| Holding | Fade continues while pressed |
| Rotation | Counterclockwise 6 degrees while pressed; zero after release |
| Release | Does not reset opacity or start a separate animation |
| Neutral pointer move | Clears opacity immediately |

The texture SHA-256 is `AB6A47EE0F8742873BC8F17765BF052C3F21EF3AA1A1698EC1188D412242536B`.

`GameCursor.render()` also has an outer hidden/controller guard; the original targeted-aim transition sets `GameCursor.hidden`. These visibility gates are distinct from the orb's texture and fade formula. Touch-STS2 currently displays its orb during active touch dragging, including targeting, and adds a separate feedback toggle. Thus matching the asset and timing is not proof of identical visibility in every STS1 state.

Touch-STS2 renders one noninteractive sprite above the UI, converts the rotation sign for Godot's downward Y axis, and ignores internal replay when resetting the fade. Loss of focus, controller takeover, canceled touch, and disabling feedback clean up the visual. Actual cross-engine pixel rendering has not been compared in-game.

## Selection and confirmation

STS1 adds a separate selection-and-confirmation step on specific touchscreen screens; it is not a universal rule for every click.

| Screen | Source behavior |
|---|---|
| Card rewards and choose-one rewards | `CardRewardScreen.cardSelectUpdate()` selects first, then exposes confirmation; skip and other reward controls remain |
| Shop cards, relics, potions | `ShopScreen`, `StoreRelic`, and `StorePotion` confirm affordable purchases; insufficient funds produce native failure feedback |
| Shop removal service | `updatePurge()` confirms entry into the removal flow |
| Boss relic choices | `AbstractRelic.update()` and `BossRelicSelectScreen` select before confirmation |
| Rest-site options | `CampfireUI.updateTouchscreen()` and `AbstractCampfireOption` select before execution |
| Single-card removal, upgrade, transform | `GridCardSelectScreen` already provides preview/confirmation for mouse input too |
| Ordinary hand or grid selections | Confirmation depends on count and effect flags, including fast-hand confirmation settings |
| Event text and ordinary reward claims | No universal extra touchscreen confirmation step |

Removal is not exclusive to shops. Touch-STS2 preserves STS2's shared deck-selection preview for events, relics, and shops, including native multi-selection behavior. It does not turn mandatory choices into skippable rewards or force a UI onto automatic effects.

## Tips, inspection, and potions

STS1 keeps hover-driven tips. Card rendering permits tips while dragging in touchscreen mode, enabling selected-card reading. `TipHelper`, monster rendering, and card-targeting state suppress some tips; the exact visible result of those combined conditions needs runtime validation.

There is no general hold-to-inspect interaction in the inspected PC code. The separate optional end-turn hold uses 0.4 seconds and defaults to off. Touch-STS2's optional 0.55-second inspection gesture is an addition that calls existing STS2 card and reward previews.

The potion popup and targeting flow have no dedicated touchscreen branch in the inspected PC code. The use/discard popup already acts as a decision step. Targeted potions submit on a press and have their own cancellation boundaries. Touch-STS2 therefore keeps STS2's native potion flow instead of adding another universal confirmation layer.

## Scrolling, maps, and layout

STS1's map distinguishes taps from drags using a 0.4-second time threshold and 30 scaled units of movement. Most other lists start grabbing immediately, using direct or interpolated following without inertia. That can allow scrolling and selection to happen together. Touch-STS2 adds drag protection to supported lists and keeps STS2's existing map protection.

The inspected PC touchscreen layout enlarges main-menu buttons: row height increases from 50 to 100 scaled units, with wider labels and corresponding highlighting. It does not generally enlarge potion slots, map nodes, or creature hitboxes. Touch-STS2 increases menu targets within the available menu height and restores native sizes when disabled.

## Fidelity boundary

The target is familiar STS1 interaction using STS2's native UI and validation. Source-derived constants, documented integration choices, and physical-device validation are separate evidence levels. Remaining checks are listed in [testing](TESTING.md); current implementation boundaries are described in [architecture](ARCHITECTURE.md).
