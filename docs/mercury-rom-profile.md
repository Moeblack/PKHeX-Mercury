# Mercury ROM data profile (`PKHeX.Mercury.Core/Data`)

This document is developer-facing. It describes `PKHeX.Mercury.Core` (`Data/`), the data layer used by
the Mercury adapter inside the upstream WinForms host (`PKHeX.WinForms/Mercury/MercuryIntegration.cs`).
The legacy standalone `PKHeX.Mercury` WinExe project is not part of the current release build. Covered
here: what the ROM tables are, how they were located, what a profile contains, and the deliberate
limits of the public build.

Nothing here ships ROM bytes or a full game-data dump; the built-in pack carries extracted Mercury 1.1
resources. The default `MercuryGameData` loads that pack from memory. The ROM / research / profile
paths documented below remain available for development, research and legacy caches.

> **Built-in data.** The app embeds a Mercury 1.1 data pack in `PKHeX.Mercury.Core` resources under
> `Resources/Mercury/1.1`: numeric profile, locations, `sprites.zip`, manifest, and encounters; it is
> loaded directly from memory and attached to the default GameData. The normal menu no longer offers
> ROM / charmap / profile / install prerequisites, does not read the legacy `%LOCALAPPDATA%` profile or
> `data-pack`, and does not infer an older version from a save. Built-in data takes priority over local
> legacy caches. The ROM/profile paths below remain available for development and research.

## 1. Sources and verification

| Source | Entry point | Hash check |
|---|---|---|
| User ROM (mercury 1.1) | `MercuryGameData.FromRom(byte[] rom, MercuryTextCodec?)` | SHA-256 must equal `131b009df7ab252deff0d6a0518ab82f88e82c940ee68d50d31033a899f7e3dd`; otherwise `InvalidDataException` |
| ROM-native research dir | `MercuryGameData.FromResearch(string root)` | `out/MANIFEST.json` and `out/sprites/sprite_manifest.json` `input.sha256` must match the same value |
| Local profile | `MercuryGameData.LoadProfile(string directory)` | profile `romSha256` + the profile format/version |
| No data | `MercuryGameData.NumericOnly()` | internal ids only, `HasData == false`, no sprites/growth |
| Built-in 1.1 data pack | Core resources `Resources/Mercury/1.1`: numeric profile, locations, `sprites.zip`, manifest, encounters; loaded from memory | pack manifest + per-file SHA-256; encounters SHA-256 `37eb8635…71458` |

`FromResearch` consumes the existing outputs (`species.json`, `moves.json`/`move_names.json`,
`items.json`, `ability_names.json`, `ability_display_names.json`, `charmap.json`,
`sprites/sprite_manifest.json`) and does **not** re-run extraction. It re-reads the ROM only from the
path recorded in the manifest, and only after re-verifying its SHA-256.

## 2. ROM tables used by `FromRom`

All addresses are GBA addresses; the reader converts with `addr - 0x08000000`. Pointer slots are
decoded the same way the ROM code does (`stored + key` when `stored` is not already a ROM address).

| Table | Base | Stride | Count | Evidence |
|---|---|---|---|---|
| Species names | slot `0x08000144` → `0x0941B350` | 11 | 1554 | pointer slot; boundary at 1554 |
| Base stats | slot `0x080001BC` → `0x0976DFBC` | 28 | 1554 | CFRU `struct BaseStats` |
| Move names | slot `0x08000148` → `0x09D8FC34` | 13 | 1015 | code `0x0803085A` (`13*move + 0x09D8FC34`) |
| Moves | literal `0x08019544` → `0x09DF68E3` | 12 | 1015 | code `0x08019510`/`0x08019516` (`12*move`) |
| Ability names | literal `0x080D8624` → `0x09D87140` | 13 | 300 | code `0x080D8614`; pool boundary proven by `0x09CE98AC` |
| Items | `[0x0809A8D8] + 0x03964096` → `0x087C7E00` | 44 | 750 | code `0x09D3CF88`, bound `>0x2ED`→0 in `0x09D3D618` |
| Level-up learnsets | slot `0x08043E20` → `0x097C32BC` | 4 (ptr) | 1554 | terminator `move==0 && level==0xFF` (code `0x09D4193A`) |
| TM/HM learnsets | slot `0x08043C68` → `0x09400494` | 16 (u32×4) | 1554 | 128 bits |
| Tutor learnsets | slot `0x08120C30` → `0x094065B4` | 20 (u32×5) | 1554 | 160 bits |
| TM/HM move ids | slot `0x08125A8C` → `0x097E87AA` | 2 | 128 | 120 TM + 8 HM |
| Tutor move ids | slot `0x08120BE4` → `0x097E8686` | 2 | 160 | |
| Front sprites | slot `0x08000128`, stored `0x070074DC` + `0x027B3BC8` → `0x097BB0A4` | 8 | — | code `0x08736DAC` |
| Normal palettes | slot `0x08000130` → `0x097D64CC` | 8 | — | code `0x0940E3BC` |
| Shiny palettes | slot `0x08000134` → `0x097E49D4` | 8 | — | code `0x0940E3BC` |
| Growth exp | literal `0x0803E894` → `0x09DFE8CC` | 0x400 (101×u32 used) | 6 rows | code `0x0803E830` (`GetLevelFromBoxMonExp`); level-100 totals validated at load |

### Notes on tables

* **Item table index ≠ embedded id.** The reader keeps `MercuryItem.Id` (table index) and
  `MercuryItem.EmbeddedId` (`+0x0E`) separate; the ROM does not make them equal (index 192/193 embed
  255, index 375 embeds 0).
* **Ability stored id ≠ display index.** `MercurySpecies.Abilities` keeps the stored ids from base
  stats (`+0x16`, `+0x17`, `+0x1A`). `AbilityNameIndices` resolves each stored id to a name-pool index:
  default `index = stored id`; the ROM function `0x09CE94E8(ability, species)` returns a pool entry in
  256..299 for 96 specific `(species, stored ability)` pairs, embedded as a compact numeric rule table
  (`MercuryAbilityAliases`, derived from `out/ability_display_names.json`: 3356 non-zero slots, 106
  special slot records → 96 unique species/ability pairs). Slot is irrelevant: the ROM function only
  takes `(ability, species)`.
* **Growth tables (consumer-proven).** The real tables are at file offset `0x1DFE8CC` (GBA
  `0x09DFE8CC`), proven by the ROM consumer `GetLevelFromBoxMonExp` at `0x0803E830`:
  `0x0803E850 ldr r6,[literal 0x0803E894]=0x09DFE8CC`; the growth index comes from base stats `+0x13`
  (`0x0803E85C`); the row stride is `0x400` (`0x0803E85E..0x0803E862`, `movs r5,#0x20; lsls r5,#5`),
  and the threshold is read at `[level*4]` while `level <= 100`. Six rows of 101 `u32` at
  `0x1DFE8CC + growth*0x400`, in growth-rate order 0..5 (medium-fast, erratic, fluctuating,
  medium-slow, fast, slow). The legacy copy at `0x253AE4` (stride 404) is **not** used: it agrees for
  growth 0/4/5 but differs at 20 levels for growth 1, 4 for growth 2 and 10 for growth 3 (e.g.
  medium-slow level 5 is `134` here vs `135` in the legacy copy; level-100 totals are identical and
  therefore cannot prove the table). A few public HOME `experienceTables` interior entries also
  disagree; the ROM bytes win. Load validates the level-100 totals
  (`1 000 000 / 600 000 / 1 640 000 / 1 059 860 / 800 000 / 1 250 000`) and monotonicity, and reports
  "no growth tables" instead of producing silent bad levels.

  Corrected first levels 0..8 and level 100 per growth rate:

  | growth | 0..8 | 100 |
  |---|---|---|
  | 0 medium-fast | 0,1,8,27,64,125,216,343,512 | 1000000 |
  | 1 erratic | 0,1,15,52,122,237,406,637,942 | 600000 |
  | 2 fluctuating | 0,1,4,13,32,64,112,178,276 | 1640000 |
  | 3 medium-slow | 0,1,9,57,96,134,179,236,314 | 1059860 |
  | 4 fast | 0,1,6,21,51,100,172,274,409 | 800000 |
  | 5 slow | 0,1,10,33,80,156,270,428,640 | 1250000 |
* **Learnsets.** Level-up entries are read as `{u16 move; u8 level}` in ROM order and stop only on
  `move==0 && level==0xFF`. `MercurySpecies.MachineMoves` and `TutorMoves` contain **move ids** (not
  slot indices): `FromRom` expands the TM/HM and tutor bitmaps straight to move ids; `FromResearch`
  resolves the research slot lists through `tmhm_moves.json` / `tutor_moves.json`. Both paths produce
  identical lists (verified: species 1 TM/HM → `92, 331, 237, …`; tutor → `173, 29, 20, …`).

## 3. Sprites

`GetSpriteRgba` renders the **first frame, first palette page** of the front sprite:

1. Resource index from `GetSpriteIndex(species, pid, runtimeState)`, which mirrors the ROM sprite
   loader (`0x0940E2F0` entry, `0x0940E442..0x0940E48A` for Unown):
   * species `201` (Unown) resolves through `PKHeX.Core.EntityPID.GetUnownForm3(pid)`: form 0 keeps
     index `201`, forms 1..27 use `412 + form`. **This form branch is a workspace change and is not
     in the published v0.2.0 build**, where Unown forms are not distinguished; it must not be
     described as released until a build containing it ships.
   * gender helper (`0x0803F78C`) reads the species gender ratio; a female result (`0xFE`) maps
     `0x1F6→0x2E8`, `0x1F7→0x2E9`, `0x23E→0x2BF`, `0x285→0x2C0`, `0x286→0x2C1`, `0x308→0x33F`;
   * otherwise species `0x338` maps to `0x44D` when the runtime flag is clear (`runtimeState == false`),
     and stays `0x338` otherwise (`null` = no runtime context).
2. LZ77 (GBA type `0x10`) decompression of the front tile block and the selected palette block.
3. Palette page 0 (16 colours), BGR555 → RGB888 with `(v<<3)|(v>>2)`.
4. 64×64, 8×8 tile-major, low nibble = even pixel; palette index 0 is transparent.
5. Normal vs shiny palette is chosen by the four-16-bit-half XOR:
   `((otId>>16) ^ (otId&0xFFFF) ^ (pid>>16) ^ (pid&0xFFFF)) <= 7`.

Output is **RGBA8888**, row-major, top-left origin, `stride = width*4`, `width = height = 64`. The
pipeline was verified against the research sprite PNGs byte-for-byte (see `scripts/extract_sprites.py`
output). Unresolvable resources return `null` — no synthetic grey image. Sprites require a ROM
(`FromRom`, or a profile whose optional `romPath` still points at a verified ROM).

## 4. Text codec

`MercuryTextCodec` handles the ROM's custom encoding: 1- or 2-byte glyph codes terminated by `0xFF`.

* `FromCharmapJson` accepts a plain hex→text object, a full game-data JSON with a `charmap` property,
  or a `game_data.js`/`app.html` assignment such as
  `window.SAVE_HOME_GAME_DATA={...,"charmap":{...}}`. It strips the wrapper textually (brace matching
  that ignores braces inside strings) and never executes JS.
* Reserved bytes are handled before any label lookup: `0xFF` terminates, `0xFA/0xFB/0xFE` are the
  `\l / \p / \n` controls, `0xFC/0xFD` display as `<FC>/<FD>`. An imported charmap can never overwrite
  these.
* Unknown bytes display as `<AB>` and re-encode to the same byte; `Encode`/`CanEncode` measure the
  **byte** length, not the Unicode character count. `Encode(text, byteLength)` fills unused space with
  `0xFF`.
* `Default()` uses PKHeX `StringConverter3`'s English GBA symbol table, so the base Latin/symbol set
  works without any import; CJK needs an import.

### Public HOME charmap URL

The Home web app embeds the game data (including `charmap`) inline:

* **`https://sum-light.github.io/azoth-wiki/home/app.html`** — the accurate public URL; it contains
  `window.SAVE_HOME_GAME_DATA={...,"charmap":{...}}`. Downloading this text and passing it to
  `FromCharmapJson` is sufficient. Mirror:
  `https://raw.githubusercontent.com/Sum-Light/azoth-wiki/main/docs/home/app.html`.
* The sibling `.../home/assets/game_data.js` referenced by the page **returns 404** on the deployed
  site and must not be used.

## 5. Profile format

`SaveProfile(directory)` and `LoadProfile(directory)` are library APIs: the caller chooses the directory
(created if needed). The GUI adapter does not use an arbitrary directory — `MercuryIntegration.ProfileDirectory`
defaults to `%LOCALAPPDATA%\PKHeX-Mercury\profile` and passes that fixed value, so both statements are
consistent: fixed per-user default at the UI layer, caller-supplied path at the API layer.

When the instance still holds ROM bytes, `SaveProfile` also
writes a local `rom-cache.gba` next to the profile and records `romPath` as the **relative** file name;
`LoadProfile` resolves it against the profile directory and re-verifies its SHA-256. The cache is a
per-user local file (~32 MiB): it is never committed, bundled, released or uploaded, and a profile
without it (`romPath` absent) simply reports no sprites and no growth tables rather than pretending to
have them. `WithTextCodec(codec)` re-decodes names: with a ROM present it re-reads the tables through
`FromRom` (numeric rules unchanged); a numeric-only instance stays numeric-only; a research/profile
instance without a ROM throws `NotSupportedException`.

The file is JSON, camelCase keys, versioned. The current profile schema version is
`MercuryProfile.CurrentVersion = 2`; a profile with an older `version` is filled from the verified ROM
on load, and a `version` newer than the running build is rejected:

```jsonc
{
  "format": "PKHeX.Mercury.Profile",
  "version": 2,
  "romSha256": "131b...e3dd",
  "source": "rom" | "research" | "profile" | "numeric",
  "romPath": "rom-cache.gba",          // relative local cache name; re-verified by SHA on load
  "charmap": { "01": "À", "0100": "啊", ... },  // optional imported charmap
  "abilityNames": ["-------", "恶臭", ...],     // 300 entries
  "species": [ { "id": 1, "name": "妙蛙种子", "baseStats": [45,49,49,45,65,65],
                 "genderRatio": 31, "growthRate": 3, "baseFriendship": 50,
                 "abilities": [65,0,34], "abilityNameIndices": [65,0,34],
                 "levelUp": [[1,33],[1,45],...], "tmhm": [...], "tutor": [...],
                 "hasData": true }, ... ],
  "moves":  [ { "id": 1, "name": "拍击", "type": 0, "power": 40, "pp": 35, "accuracy": 100, "priority": 0 }, ... ],
  "items":  [ { "id": 192, "embeddedId": 255, "pocket": 5, "type": 0, "name": "深海之牙" }, ... ],
  "growth": [ [0,1,8,...], ... ]   // 6 × 101, optional
}
```

`levelUp` pairs are `[level, move]`; `baseStats` order is `HP, Atk, Def, Spe, SpA, SpD`. `items`
entries carry `pocket` (ROM `+0x1A`) and `type` (ROM `+0x1B`; for balls this is the stored ball
value); both are optional and absent in legacy profiles without a ROM, matching `MercuryItem`.

Profiles are generated locally and are **not** part of the repository or the released binaries. A
profile can carry the imported charmap so a user who cannot download it can still get Chinese labels;
carrying names/charmap is acceptable because the file lives in the user's own directory.

## 6. Public-build limits

* No ROM, sprite image, full table dump or bundled charmap is compiled into the assembly. Only the
  numeric addresses, decoding keys, table shapes and the 96-entry ability alias rule table are code.
* `NumericOnly()` is the pre-load state: names are decimal ids, `HasData == false`,
  `GetLevel`/`GetExperience` throw `NotSupportedException`, sprites return `null`.
* `FromRom` rejects any ROM whose SHA-256 does not match the supported build; it never silently applies
  fixed offsets to a different version.
