# Architecture

## Scope and evidence

This document describes the current implementation against STS2 v0.111.0, release commit `41cef1ea`, using MegaDot 4.5.1-m.14 and .NET 9. Reference material included the game assembly, its XML documentation, scene definitions, project settings, and runtime logs. Decompiled line numbers depend on the extraction tool and are not treated as stable API identifiers.

Touch-STS2 adds interaction policy while preserving native card validation, selection effects, and action submission. The reference for interaction choices is [STS1 PC's touchscreen mode](STS1_REFERENCE.md). Optional inspection gestures and scroll protection are documented additions.

The [interaction audit](TOUCH_AUDIT.md) records the gaps found in 0.2.6 and their disposition in 0.2.7. Shared-event direct voting is retained; touch access to multiplayer reactions is deferred.

## Components

| Component | Responsibility |
|---|---|
| `ModEntry` | Load settings and install patches once; roll back on initialization failure |
| `GameAdapter` | Resolve and install 25 version-sensitive patch targets and validate their contracts |
| `TouchRuntime` | Own combat gestures, coordinate input and native card-play controllers, resolve targets, and clean up |
| `TouchGesture` | Pure combat gesture and card-position policy in viewport coordinates |
| `TouchPress` | Pure tap, drag, and hold arbitration for noncombat UI |
| `TouchUi` | Route supported list scrolling and inspection through native controls |
| `TouchConfirmation` | Keep one pending choice and present a native confirmation button |
| `TouchCursor` / `TouchCursorFeedback` | Render one touch orb and apply the STS1 fade state |
| `TouchSettings` / `SettingsUi` | Persist preferences and add translated settings with native fonts |
| `SettingsIntegrations` / `RitsuSettingsAdapter` | Register an optional RitsuLib settings page with live bindings to canonical preferences |
| `TouchText` | Resolve embedded translations with English fallback |

## Input and coordinates

Native input settings use fixed 28-point labels to avoid shrinking during initial layout. RitsuLib's Mod Settings receives three toggles through its public page and callback-binding APIs. Labels resolve in the current game language; reads, writes, and resets use the canonical preferences. Reflection keeps RitsuLib optional. BaseLib and ModConfig registrations were removed in 0.2.9.

The inspected game has no dedicated screen-touch gesture implementation. Touch-STS2 uses the primary touch-to-mouse path and stays in `MouseAndKeyboard` mode. Controller and keyboard-only navigation disable recursive mouse handling in the native UI, so they cannot be used as a substitute touch mode.

The project's design resolution is 1920 by 1080 with `canvas_items` stretching and an expanding aspect ratio. Gesture coordinates are viewport coordinates; card positions are transformed into the holder parent's local space before assignment. The existing position tween is canceled while the mod owns the card to avoid competing movement.

Input is observed through `NGame` and `NControllerManager` before GUI handling. Event instance IDs prevent one event from being processed twice through different patched entry points. Godot's emulated mouse stream is the command path; raw touch events provide cancellation tracking. Internal click replay and pointer parking do not restart touch feedback.

The user preference does not change on mouse motion. Explicit controller input suspends touch behavior; a left or right mouse press restores it. An analog magnitude above 0.5 counts as controller input. While a pointer is held or its release is pending, `TouchInputOwnership` consumes controller events before native mode detection. They are discarded, not replayed later. After completion, a fresh controller event can take over normally. Focus loss and touch cancellation release the pointer state.

Combat pointer parking still requests the usual neutral cursor position, but `TouchHover` also clears the viewport's native hover through its exit/entry notifications. This clears internal hover and emits native exit notifications even when the OS cursor has not moved. The next pointer event can hover normally; cleanup does not synthesize a click or change focus.

Native action input uses `InputEventAction` and `MegaInput` rather than ordinary Godot InputMap bindings. The mod reuses that path where needed, including switching selected cards. It does not introduce another game input-mode enum value.

## Combat ownership and submission

The native chain is:

```text
NPlayerHand -> NHandCardHolder -> NMouseCardPlay
    -> NCardPlay.TryPlayCard
    -> CardModel.TryManualPlay
    -> PlayCardAction -> ActionQueueSynchronizer
```

`NMouseCardPlay` is temporary and owns native cleanup. The mod tracks only controllers created for its current gesture, leaving numeric-shortcut controllers and ordinary mouse behavior outside its zone overrides.

The first stationary release inspects instead of playing. Dragging can commit on the first release. A subsequent gesture may commit, cancel, or switch cards. Release coordinates are retained until GUI hover has caught up; submission does not depend on a potentially stale hardware pointer position.

The native controller checks `IsCardInPlayZone()` after target selection. Its original implementation reads the viewport mouse position, which can reject a valid touch-selected target while the hardware pointer remains over the hand. Scoped play/cancel-zone patches resolve this for owned gestures. Changing only the target node would not be sufficient.

Native mouse/controller detection signals can cancel a card-play controller. Replayed input therefore must not accidentally switch modes mid-submission. Completion, screen changes, loss of focus, ending combat, and controller takeover all clear the owned state. A three-second completion timeout reports and cancels a stuck submission.

## Target validation and co-op

Use the game's `CanPlay`, `CanPlayTargeting`, `TargetType`, and `NTargetManager.AllowedToTargetNode` rules. Do not infer validity solely from whether an object looks like an enemy. Untargeted effects pass no creature target; supplying an arbitrary creature can fail native validation.

Target nodes may be either `NCreature` or `NMultiplayerPlayerState`. The latter maps to `Player.Creature` in the native controller. The left party sidebar takes precedence over battlefield objects behind it; an invalid entry must not fall through to an enemy underneath. A legal ally entry may lie outside the usual untargeted play rectangle.

Before release submission, the mod resolves the target again and checks the native `HoveredNode`. Native restrictions still govern self-targeting, dead creatures, and enemy/ally relationships. Without a held card, the party sidebar retains its normal expanded teammate view.

Actual play continues through the native action queue and synchronization. `PeerInput` mirrors remote pointer and presence information; it is not the authoritative gameplay command path. Client-side visual completion may wait for the host's action to return. Real multiplayer validation remains outstanding.

## Hover and inspection

Most native buttons require focus for mouse press/release handling. Hand holders subscribe to raw enabled mouse signals and differ from that button path. A successful synthetic button press cannot be assumed to work without correct hover.

Releasing a touch does not necessarily generate mouse-exit. After a completed card interaction, the mod requests a neutral pointer position and sends a synthetic motion event to clear hover even if the platform refuses the hardware warp. The normal cursor is hidden while touch mode is active and restored when it ends.

Relics, powers, intents, and teammate details keep native focus/unfocus behavior and dynamic updates. The mod does not create a second generic tooltip layer. `SwipePower.ExtraHoverTips` already exposes a stolen card through `HoverTipFactory.FromCard`; this is reused rather than revealing additional information.

Ordinary card holds prefer the native `AltPressed` signal and fall back to the native card inspector if the signal has no binding. Special rewards are `NRewardButton` instances rather than card holders. Their long-press path displays native `Reward.HoverTips`, consumes release, and avoids replaying a claim click. Hold expiry is checked both during ticking and on release, with the original content and screen revalidated.

## Confirmations and selection boundaries

One pending confirmation uses the game's own confirmation-button scene, animation, and sounds. It is cleared when its content, screen, focus, or input mode becomes invalid. Existing skip controls and game-provided text remain in place.

| Selection | Handling |
|---|---|
| Card rewards and generic card choices | Select, then confirm; preserve skip and the generic screen's opening guard |
| Generic relic choices | Select, then confirm; preserve native skip rules |
| Simple card grids | Suppress automatic completion during touch input and enable the native confirmation button at valid counts; leave selection preferences unchanged |
| Ancient gifts | Confirm non-shared, unlocked, non-proceed choices through the original event callback |
| Event hover tips | Optional hold inspection consumes release; short taps on ordinary/shared event options stay native |
| Crystal Sphere | Stage a hidden cell and pin native area highlights; revalidate tool/count/cell and serialize confirmed reveals |
| Co-op treasure relics | Stage locally, then call native `PickRelic` on confirmation; no vote is sent during preview |
| Shop cards, relics, potions, removal service | Confirm only affordable choices; recheck stock, price, and gold on submission |
| Rest-site options | Confirm the option, then enter the native effect or selection screen |
| Deck removal from shops, events, or relics | Keep native selection, preview, back, and confirmation |
| Upgrade, transform, enchantment, bundle | Keep existing native preview and confirmation |
| Combat hand or pile selection | Preserve required counts and `RequireManualConfirmation` |
| Event text and ordinary reward claims | Preserve native direct selection |
| Potions, map nodes, character selection | Preserve native menus, travel rules, and start confirmation |

Permanent removal converges on `FromDeckForRemoval`, `FromDeckGeneric`, and `NDeckCardSelectScreen`, regardless of whether a shop, event, or relic initiated it. Mandatory operations still permit revising a preview without becoming skippable. Effects with no meaningful choice may resolve without opening a screen; random or automatic removal is unchanged.

## Scrolling

Child controls can consume pointer input before a scroll parent receives it. `TouchUi` tracks a gesture starting on a supported child, updates the existing scroll field, and suppresses the deferred click once movement exceeds 14 scaled units. Returning to the original point does not restore click eligibility. Native limits, easing, and rebound remain responsible for layout.

Supported surfaces include scrollable containers, card grids, dropdowns, rewards, credits, and horizontal timeline slots. Sliders and scrollbars keep their native ownership. Maps already have their own drag protection and travel/drawing guards, so the mod leaves that path alone. No inertial scrolling is added.

## Loading, resources, and compatibility

The game loads the matching manifest and DLL, then invokes the attributed initializer. Touch-STS2 embeds localization and the cursor texture, and needs no additional resource pack or mod dependency. It defines no custom Godot node subclasses; settings, confirmation controls, and cursor rendering use existing native types.

`min_game_version` is a lower bound, not a guarantee of compatibility with future releases. Stable assembly identity does not prevent runtime API or behavioral changes. Patch signatures, relevant private fields, scroll contracts, the native target property, and the reward-preview entry are checked before installation. A mismatch disables the overrides with a named error; static checks alone are not proof of runtime correctness.
