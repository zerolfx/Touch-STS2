# Changelog

Release notes describe implementation changes. Offline checks and in-game validation are recorded separately in the [test report](docs/TESTING.md).

## Unreleased

- Add a GitHub Actions build for pushes, pull requests, tags, and manual runs.
- Pin the CI reference-assembly dependency and verify packaged files, versions, embedded resources, and SHA-256 checksums.
- Include the original 845-byte STS1 cursor texture so builds no longer need an STS1 installation or CI secrets.
- Rewrite project documentation in English and keep localized text in i18n resources and fixtures.
- Separate the project overview, build instructions, and release history.
- Organize the initial repository history by implementation and documentation.

## 0.2.4 - 2026-09-20

- Replace the custom yellow touch ring with STS1 PC's original orb texture.
- Match its 32-pixel base size, centered position, press rotation, initial opacity, and frame-based fade formula.
- Follow the pointer during dragging and scrolling without replaying feedback for synthetic confirmation clicks.
- Add cursor timing checks at 30, 60, and 144 Hz and verify the embedded texture hash.

## 0.2.3

- Support targeting allies through both battlefield characters and the left party sidebar.
- Revalidate the native target at release and prevent invalid sidebar entries from selecting objects behind them.
- Add hold-to-preview for special card rewards, including recovered stolen cards, without claiming on release.
- Fall back to the native card inspector when a holder has no alternate-action binding.
- Reuse native relic, power, intent, and teammate detail views.

## 0.2.2

- Add separate confirmation for generic card and relic choices, retaining native skip rules.
- Preserve native deck previews for removal, upgrade, transform, enchantment, and bundle selection.
- Show native insufficient-gold feedback immediately instead of opening purchase confirmation.
- Recheck stock, price, and available gold before submitting a purchase.

## 0.2.1

- Preserve the grab offset and one-to-one pointer movement for untargeted cards; eliminate the jump when crossing the drag threshold.
- Convert viewport coordinates to the card parent's local space and cancel conflicting position tweens.
- Replace the custom confirmation bar with the game's native confirmation button, retaining existing skip and reward controls.
- Localize the three settings in all 16 supported game languages, including live font and language updates.

## 0.2.0

- Add two-step confirmation for card rewards, shop purchases and removal services, and rest-site options.
- Allow dragging lists from child entries while suppressing accidental selection.
- Add optional hold-to-inspect and touch feedback settings.
- Keep selected-card tips visible during inspection and enlarge main-menu touch targets within the available layout.
- Add diagnostics, cleanup on context changes, and noncombat compatibility checks.

## 0.1.1

- Anchor targeted cards at the lower center while aiming, 260 scaled units above the bottom, instead of following the pointer upward.
- Reset the aiming state for each newly selected card.

## 0.1.0

- Introduce the input-settings toggle, drag-to-play, tap-to-inspect, and tap-to-target flows.
- Support cancellation, switching cards, controller handoff, and hover cleanup.
- Preserve native play validation and action submission; scope overrides to card-play controllers owned by the mod.
