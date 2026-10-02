# Sources and licensing / 来源与许可

## Code

PKHeX Mercury is an unofficial derivative of [kwsch/PKHeX](https://github.com/kwsch/PKHeX), based on upstream commit `542111fc8584ff29c9d1455553b8acd0e1f8a59a` (2026-09-29). The upstream tree, history, notices and GPL license are preserved. The new Mercury projects use **GPL-3.0-or-later**, consistent with the upstream Core project's package license expression. See the repository [LICENSE](../LICENSE).

The Mercury executable references `PKHeX.Core`. The new save adapter and dedicated WinForms interface are independent additions; Mercury's compressed entities are not passed into the retail `PK3`/`SAV3` implementations. Upstream PKHeX authors have not endorsed or provided official support for this adaptation.

## Format references

- The user-supplied Mercury 1.1 ROM's actual readers, writers and pointer transformations are the authority for this supported format. Format addresses and arithmetic are documented in the save/profile notes; no ROM is distributed.
- [Complete Fire Red Upgrade](https://github.com/Skeli789/Complete-Fire-Red-Upgrade) and [pret/pokefirered](https://github.com/pret/pokefirered) supplied symbol and engine-family references. They are not assumed to match Mercury without current-ROM evidence.
- [Sum-Light/azoth-wiki](https://github.com/Sum-Light/azoth-wiki) and its public HOME page supplied existing format clues and optional text-code-to-Unicode labels. The application does not execute downloaded JavaScript. A user may import the labels locally or explicitly request the HOME page with the UI download button. Numerical species/move/item/experience data continue to come from the supported ROM.

The new Mercury projects do not copy the HOME editor's JavaScript implementation into the application or embed its full name/charmap database. Conditional ability-name mappings in source are small numeric relationships derived from the actual ROM display function.

## Game resources and personal data

A code license does not grant rights to Pokémon game content, logos, music, artwork, full game images, or third-party resource collections. Existing upstream resource notices remain in the original README. The Mercury executable does not reference the upstream drawing/sprite projects; Mercury previews are decoded at runtime from the user's own ROM.

The Mercury changes and release package do **not** add a game ROM, extracted sprite collection, soundtrack, script/text dump, HOME database, or personal save. Profiles and `rom-cache.gba` are created only in the user's local data directory. They are not repository assets and must not be included when contributing source changes.

Pokémon and related names belong to their respective rights holders. This is an independent fan-made editing tool, not an official Pokémon or Project Pokémon release.
