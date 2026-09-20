# Touch interaction audit

Audited Touch-STS2 0.2.6 (`2bafafa`) against the locally inspected STS2 v0.111.0 source/scenes and STS1 PC touchscreen branches. This is a source review, not a live multiplayer or physical-device test. No gameplay changes were made during this audit.

The review scanned 68 event model source files, shared selection commands, custom event nodes, multiplayer voting/targeting interfaces, and the STS1 touchscreen call sites. Similar-looking screens do not necessarily share an input callback.

## Resolution in 0.2.7

Simple-grid confirmation, ancient gift confirmation, event hold inspection, and Crystal Sphere area confirmation are now implemented. Source/API and engine-free confirmation checks passed; in-game acceptance is outstanding. Shared-event short taps intentionally retain direct voting, and the reaction-wheel work is deferred. The STS1 fidelity differences below are outside this release.

The findings below describe the audited 0.2.6 behavior and explain the changes.

## Findings in 0.2.6

### Simple-grid card rewards

`EventModel.SelectCardsToAddToDeckFromGrid` calls `CardSelectCmd.FromSimpleGridForRewards`, opening `NSimpleCardSelectScreen`. This is distinct from the patched reward and choose-one screens.

`BrainLeech.ShareKnowledge` requests one card; `RoomFullOfCheese.Gorge` requests two. Their fixed-count `CardSelectorPrefs` do not require manual confirmation. `NSimpleCardSelectScreen.OnCardClicked` completes the choice when the required count is reached. Scroll protection and long-press inspection apply, but the final short tap still submits immediately.

`ChoicesParadox.AfterPlayerTurnStart` uses the simple grid for a generated combat card with the same behavior. Conversely, `SealedDeck` explicitly enables manual confirmation, and `SeaGlass` uses a variable count that enables it by default.

Suggested fix: reuse the simple grid's existing confirmation button for local touch selections. Preserve required counts, mandatory/optional rules, synchronization, and automatic choices where no screen is shown. Do not blanket-change every combat hand/pile selection.

### Ancient gifts and event inspection

Ancient gifts use `NEventOptionButton`, not `NChooseARelicSelection`. `AncientEventModel.RelicOption` supplies the gift; `NEventOptionButton.OnRelease` immediately calls `NEventRoom.OptionButtonClicked`. Generic relic confirmation does not cover it.

`NEventOptionButton.OnFocus` displays `Option.HoverTips`. Holding a finger can display those tips, but lifting it on the button still executes the option. `TouchUi` protects inspection releases for card holders and reward rows only, not event buttons.

Suggested fix: confirm ancient gift choices and provide safe inspection for event options that actually have native hover tips. Ordinary text-only events do not universally need confirmation; STS1 did not have that rule. Preserve locked/proceed options and native multiplayer death prevention.

### Shared event votes

`NEventRoom.OptionButtonClicked` forwards non-proceed choices to `EventSynchronizer.ChooseLocalOption`. Shared events immediately record/broadcast the vote. When all votes exist, the host selects and executes an outcome. `FlashConfirmation` is an animation after the decision, not a user confirmation step.

Affected shared-event paths include `DenseVegetation`, `BattlewornDummy`, `JungleMazeAdventure`, `MorphicGrove`, `PunchOff`, `WarHistorianRepy`, and `TheLanternKey`. The treasure patch only intercepts `NTreasureRoomRelicCollection.PickRelic`.

Suggested fix: stage choices locally before submitting. Validate event/page/option identity and enabled state. Test with all other players already voted, page changes, and disconnects. Preview must never send a vote.

### Crystal Sphere preview

`NCrystalSphereScreen.OnHoverCell` previews the affected area via `CrystalSphereMinigame.SetHoveredCell`. `OnCellClicked` calls `CellClicked`, immediately decrementing the remaining divination count. No existing mod handler stages this operation.

A normal touch release therefore spends a use rather than retaining the area preview. Separate preview from execution and revalidate the tool, cell, and remaining uses before confirming.

Both `MouseReleased` and `Released` are wired to the click path. A future patch must cover the shared operation and ensure one confirmation spends one use. This alone does not establish a native double-spend bug. Do not reveal hidden contents: `NCrystalSphereItem` has no native hover-tip interface to reproduce.

### Multiplayer reaction wheel

`NReactionWheel._Input` opens/closes on `react_wheel`, navigates using relative mouse motion, and warps the cursor back to its origin. The mod has no touch entry point or wheel-specific handling. A plain primary-touch stream cannot invoke that action by itself; device key mappings are an external workaround.

Suggested fix: provide a touch entry point and absolute-position selection while retaining the native wheel and synchronization. Avoid conflicts with card drags and inspection holds.

## STS1 fidelity differences

- **Dragging tips:** `AbstractCard.renderCardTip` and `TipHelper.render` permit tips in some touchscreen dragging states, but suppress them during targeting and in the drop zone. STS2 removes tips in `NMouseCardPlay.StartCardDrag`; the mod recreates/protects them for stationary inspection after release, not throughout those drag states. Matching this needs state-specific handling and visual comparison.
- **Aiming feedback:** STS1's targeted-aim path sets `GameCursor.hidden`; the mod continues showing its orb while aiming. Matching texture/fade does not establish identical visibility.
- **Hand geometry:** STS2 hitboxes and one-to-one movement after lifting are documented integration choices, not newly discovered missing screens.
- **End-turn hold:** STS2 already has `IsLongPressEnabled` and `NEndTurnLongPressBar`. Native ownership is retained, so no duplicate hold implementation is needed.

## Covered or deliberately native

| Area | Evidence and boundary |
|---|---|
| Standard rewards and choose-one screens | Patched `SelectCard` / `SelectHolder` |
| Generic relics and co-op treasure | Separate confirmation patches; treasure added in 0.2.6 |
| Removal, upgrade, transform, enchantment | Shared deck-selection screens provide native preview/confirmation, including event-originated effects |
| Card bundles | `NChooseABundleSelectionScreen.OnBundleClicked` opens native preview/cancel/confirm |
| Fake Merchant | Uses merchant inventory/slots, covered by purchase confirmation; its model currently excludes multiplayer runs |
| Other shops and rest options | Shared callbacks are patched; native validation remains authoritative |
| Mend a teammate | Rest action confirmed first; `MendRestSiteOption.OnSelect` then targets characters or party rows natively |
| Potions for allies or merchant | Popup and `NPotionHolder.TargetNode` already handle creatures, party rows, and merchant buttons |
| Teammate details | Party-row release opens `NMultiplayerPlayerExpandedState` outside targeting |
| Card intent synchronization | Native `NPlayerHand.StartCardPlay` still notifies `HoveredModelTracker`; action queue is retained |
| Map scrolling | Map-point scenes configure 20/30-unit drag rejection, checked by `NButton` |
| Map drawing/erasing | Visible tool buttons start left-button drawing; right/middle shortcuts are not the only entry |
| Map votes and pings | Native tap votes; repeat tap pings. Extra confirmation is a separate co-op UX decision, not an STS1 fidelity requirement |
| Enemy/player information | Reuse native hover content; do not expose hidden information |
| Combat hand/pile selection | Native count/confirmation flags retained; lack of universal extra confirmation is not itself an STS1 mismatch |

## Validation still required

Validate the new simple-grid, ancient gift/event inspection, and Crystal Sphere paths. Shared-event voting remains native by design; reaction-wheel touch support is deferred.

For each staged action, check replacement, cancellation, context changes, disabled controls, repeated confirmation, and host/client completion. Multiplayer previews must not submit gameplay commands. Retest native behavior with touch disabled and with controllers. Build/API checks do not establish these runtime outcomes.
