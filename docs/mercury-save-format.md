# Mercury 1.1 save format (PKHeX.Mercury.Core)

Developer-facing reference. It records the ROM instruction evidence behind `MercurySave`,
`MercuryPokemon` and `MercuryTrainer`. It contains no personal save values and no ROM data dumps; only
addresses, structure offsets and rules. The user-facing facts live in the README: the editor reads
128 KiB saves (plus a 16-byte RTC trailer) and validates section signatures/checksums automatically —
no manual trimming, re-extension or offset handling is required.

Target build (fixed, see `MercuryGameData` contract rule):

```text
Mercury 1.1 ROM — 33,554,432 bytes
SHA-256 131b009df7ab252deff0d6a0518ab82f88e82c940ee68d50d31033a899f7e3dd
```

All offsets below were read from that ROM with a Thumb disassembler; the ROM and save files remain
read-only and are never modified or committed.

## 1. Save container

- 32 flash sectors of 0x1000 bytes (0x20000 total). A `.sav` may append a 16-byte trailer; it is
  preserved verbatim on export.
- Two slots of 14 sections each: slot 0 = sectors 0-13, slot 1 = sectors 14-27.
- Sector footer: section id `u16 @ +0xFF4`, checksum `u16 @ +0xFF6`, signature `u32 = 0x08012025 @ +0xFF8`,
  counter `u32 @ +0xFFC`.
- Section payload sizes (ROM block sizes, literal pool at 0x0804C16C / 0x0804C174 / 0x0804C180):
  `0xF24, 0xFF0, 0xFF0, 0xFF0, 0xD98, 0xFF0 ×8, 0x450`.
  Sums: SaveBlock2 = 0xF24, SaveBlock1 = 0x3D68, PokemonStorage = 0x83D0.
- Section checksum: sum of little-endian `u32` words over the section payload size, folded once
  (`(sum >> 16) + (sum & 0xFFFF)`), stored at `+0xFF6`. Function 0x080DA1A8.
- Slot selection: uniform counters, signature and checksum valid, all 14 unique section ids present.
  Wrap-aware newer test `(int)(candidate - current) > 0`. An invalid newest slot falls back to the
  other valid slot and records a warning.
- "Parasite" tails beyond the payload size are real data consumed by the ROM (0x09D56D08 copies
  section 0 tail `[0xF24..0xFF0)` to 0x0203B174, section 4 tail `[0xD98..0xFF0)` to 0x0203B240 and
  section 13 tail `[0x450..0xFF0)` to 0x0203B498). Unedited tail bytes are preserved; an explicit
  expanded-coins edit changes only its four mapped bytes in the section 13 tail.

### Block offsets

| Block | Sections | Payload |
|---|---|---|
| SaveBlock2 | 0 | 0xF24 |
| SaveBlock1 | 1-4 | 0xFF0 + 0xFF0 + 0xFF0 + 0xD98 |
| PokemonStorage | 5-13 | 0xFF0 ×8 + 0x450 |

Runtime bases (copy routine 0x0804C058): SaveBlock2 = `0x02024588`, SaveBlock1 = `0x0202552C`,
PokemonStorage = `0x02029314`.

## 2. Boxed Pokémon records

- 58 bytes per record, stride 0x3A, 25 boxes × 30 slots.
  `GetBoxedMonPtr` 0x09D549A4 rejects box > 0x18 or slot > 0x1D. Compressed/expanded pair:
  0x09D548D0 (58 -> 80), 0x09D54AFC (80 -> 58). Per-field reader/writer: 0x09D54A2C / 0x09D54CC8.
- The boxed record is stored in plaintext: no XOR encryption and no PID-based substructure shuffle
  (the expand/compress pair writes a fixed block order).
- The 100-byte party record is the same 80-byte expanded form followed by a 20-byte party tail
  (0x50-0x63); verified against real saves.
- Box pointer table 0x09DD819C (25 pointers) gives the record locations:

| Boxes | Location |
|---|---|
| 0-18 | PokemonStorage + 0x4 |
| 19-21 | concatenated sector 30/31 data areas (0xFF0 each) + 0xB0C |
| 22-23 | SaveBlock1 + 0x1F08 |
| 24 | SaveBlock2 + 0xB0 |

Box indices here are the ROM's zero-based ids; the UI numbers boxes 1-25, so internal boxes 19-21 are
the user's boxes 20-22. Boxes 19-21: 0x09D56D08 reads sectors 0x1E/0x1F into 0x0203C038 / 0x0203D028; box 19 pointer
`0x0203CB44 = 0x0203C038 + 0xB0C`. The provided save samples have these boxes empty, so this path is
ROM-instruction proven but not data-sampled.

### 58-byte boxed layout

| Offset | Size | Field |
|---|---|---|
| 0x00 | u32 | PID |
| 0x04 | u32 | OT id (`(SID<<16) | TID`) |
| 0x08 | 10 | nickname |
| 0x12 | u8 | language |
| 0x13 | u8 | bits 0-2 flags (`badEgg`, `hasSpecies`, `isEgg`), bits 3-7 type override |
| 0x14 | 7 | OT name |
| 0x1B | u8 | markings |
| 0x1C | u16 | species (internal id) |
| 0x1E | u16 | held item |
| 0x20 | u32 | experience |
| 0x24 | u8 | PP-up bonuses, 2 bits per move |
| 0x25 | u8 | friendship |
| 0x26 | u8 | ball (full byte; ROM field 0x26 -> 0x09CCE574 -> 0x09D069A0 reads `Growth[0xA]`) |
| 0x27 | 5 | 4 moves packed as 10-bit little-endian |
| 0x2C | 6 | EVs HP, Atk, Def, Spe, SpA, SpD |
| 0x32 | u8 | pokerus |
| 0x33 | u8 | met location |
| 0x34 | u16 | origins: met level (0-6), game (7-10), bits 11-14 used for other data (gigantamax etc., preserved verbatim), OT gender (15) |
| 0x36 | u32 | IVs: HP(0-4), Atk(5-9), Def(10-14), Spe(15-19), SpA(20-24), SpD(25-29), bit 30 egg flag, bit 31 hidden ability |

The expanded (RAM/party) layout puts the same fields at 0x20/0x22/0x24/0x28/0x29/0x2A(ball)/0x2C/0x34/
0x38/0x3E/0x44/0x45/0x46/0x48/0x4C.

Two header flags carry extra ROM semantics:

- Egg: getter is IV-word bit 30 (MON_DATA_IS_EGG field 0x2D -> 0x08040154); the header flags bit 2 is
  kept in sync when writing. Bit 31 (hidden ability) is never touched by the egg flag.
- Ball is the full byte at 0x26/0x2A, not a field of the origins word.

### Type override field (0x13 bits 3-7 <-> expanded 0x1E)

The 5-bit field at 0x13 bits 3-7 is **not** an ability; it is a type override:

- 0x09D60F2C, called at the start of the box-compress path (0x09D54B10), recomputes it only when the
  decoded value is 0: `t = 0x09D60E20(species, PID)` reads the species type (species table 0x0976DFBC,
  stride 0x1C, type1 @+6 / type2 @+7), then stores `EncodeType((t + 1) & 0xFF)`.
- Encode = 0x09D60E98 (`0` -> `0`, `0x1F` -> `0xA51F`, valid type -> `0xA500 | bits`, else `0`).
- Decode = 0x09D60E64; the valid value table (19 bytes at 0x09DDA014) is
  `{0..8, 10..17, 23, 24}`.
- Verified against real party records: the decoded values equal the species types (Normal, Flying,
  Electric, Fire, Grass, Psychic).

Because the recompute happens only for a decoded value of 0, changing the species while an explicit
override is stored would keep a stale type. `MercuryPokemon.Species` therefore clears the 5-bit
override (box 0x13 bits 3-7, expanded 0x1E -> 0) only when the species actually changes, so the real
consumer 0x09D60EBE derives the type for the new species/PID; ordinary edits (including setting the
same species) leave the field untouched. `MercuryPokemon.TypeOverride` exposes it read-only; there is
no API to write an arbitrary override.

### Ability

`GetAbilityBySpecies` 0x09D07C68 (reached from 0x08040D38; its caller 0x08040D7C reads species via
field 0x0B and the ability marker via field 0x2E):

- If IV-word bit 31 is set and the species hidden ability (species table +0x1A) is non-zero, the
  hidden ability is returned.
- Otherwise, when PID bit 0 is set and the species second ability (+0x17) is non-zero, the second
  ability is returned; otherwise the first ability (+0x16).

Gender constants are the ROM values: gender ratio 0x00 = male only, 0xFE = female only, 0xFF = genderless;
otherwise the low PID byte is compared with the ratio (below = female). `SetPersonality` keeps the current
PID when it already satisfies every constraint, otherwise it enumerates the low 16 bits and constructs the
high half from the nature residue (`65536 % 25 == 11`, inverse 16) for non-shiny requests, or from the
8-value shiny xor for shiny requests.

`MercuryPokemon.AbilitySlot`/`HiddenAbility` therefore expose the **stored marker** only
(`2` = hidden flag set, else `PID & 1`). They cannot resolve the fallbacks (species has no second /
hidden ability) without the species table, so the effective/displayed slot is decided by the
data/UI layer from the species ability table; the mon must not claim it returns the effective slot.

## 3. Party

- Count byte at SaveBlock1 + 0x34, six 100-byte records at SaveBlock1 + 0x38.
- Lossless editing: `FromParty` requires exactly 100 bytes and keeps the original record as a template plus
  an initial canonical snapshot. For party-origin records `ToPartyBytes` starts from that template and maps
  back only fields whose canonical value changed (header bytes, flags low 3 bits vs type override
  separately, moves individually as 16-bit values, EVs, misc), then re-applies this object's party tail,
  current PP, contest and ribbon arrays. Expanded bytes the boxed form does not carry (0x1C/0x1D, 0x2B,
  mail, status, ...) therefore survive a round trip. `Clone` copies the template independently and
  `ToBox` drops it. `FromBox` requires exactly 58 bytes, so a retail 80-byte .pk3 is rejected instead of
  being silently truncated.
- Tail (offsets inside the 100-byte record): 0x50 status (u32), 0x54 level, 0x55 mail,
  0x56 current HP, 0x58 max HP, 0x5A Atk, 0x5C Def, 0x5E Spe, 0x60 SpA, 0x62 SpD.
- `RecalculatePartyStats` mirrors the ROM main stat path (0x09D073A4; HP path 0x09D072AC):
  `stat = ((2*base + IV + EV/4) * level) / 100 + 5`, nature modifier (+/-10%) applied to the
  non-HP stat; `HP = ((2*base + IV + EV/4) * level) / 100 + level + 10`.
  `RecalculatePartyStats(int[] baseStats, byte level, int mode = 0)` and `ToParty(...)` accept the
  save stat mode (default 0 = backward compatible); `MercurySave.StatMode` exposes it read-only:

  | Mode | ROM gate | HP | Other stats |
  |---|---|---|---|
  | 0 | flag/VAR unset or unhandled | `(2*base + IV + EV/4)*level/100 + level + 10` | `(2*base + IV + EV/4)*level/100 + 5` |
  | 11 | `0x09D31DA0` (VAR 0x5018 == 11) and species != 0x12F | normal | `effective = min(255, base*(600-baseHP)/(BST-baseHP))`, then `(2*effective + IV + EV/4)*level/100 + 5` |
  | 12 | `0x09D31D70` (VAR == 12) and BST <= 350 | effective base `(baseHP*2) & 0xFF` | `(4*base + IV + EV/4)*level/100 + 5` |
  | 13 | `0x09D31D40` (VAR == 13) | effective base 100 | `(200 + IV + EV/4)*level/100 + 5` |

  Mode 12 with BST > 350 falls back to the normal path. Shedinja (species 0x12F) has max HP 1 in every
  mode. The nature modifier of 0x08043698 is applied to the non-HP stats.
  `StatMode` is read from flag 0x930 (section 0 + 0xF2A bit 0) and VAR 0x5018 (section 4 + 0xEFC, u16);
  it is read-only and there is no API to edit the flag.

  Current HP follows ROM 0x09D073F6..0x09D07438: `oldHP == 0 && oldMax == 0` -> new max; `oldHP == 0 &&
  oldMax != 0` -> stays 0; living HP gains the `newMax - oldMax` delta only when `newMax >= oldMax`,
  then is clamped to the new max. Status, mail, PP and all other party bytes are preserved.

## 4. Trainer

| Field | Location |
|---|---|
| Name (8 bytes) | SaveBlock2 + 0x00 |
| Gender | SaveBlock2 + 0x08 |
| TID (u16) / SID (u16) | SaveBlock2 + 0x0A / + 0x0C (`ID32 = (SID<<16) | TID`) |
| Play time hours (u16) | SaveBlock2 + 0x0E |
| Play time minutes / seconds | SaveBlock2 + 0x10 / + 0x11 |
| Encryption key (u32) | SaveBlock2 + 0xF20 |
| Money (u32, XOR key) | SaveBlock1 + 0x290 |
| Coins (u32, no XOR) | section 13 + 0x7CC |

- Money is proven: 0x09D3CFF8 computes `0x290` (`movs r6,#0xA4; lsls r6,#2`) and calls GetMoney
  0x0809FD58 on `gSaveBlock1 + 0x290`; GetMoney/SetMoney XOR with the SaveBlock2 + 0xF20 key.
- Coins: RAM `0x0203B814` = parasite block `0x0203B498` + 0x37C, so the file location is the active
  slot's section 13 at `0x450 + 0x37C = 0x7CC`, u32, **not XORed**. Consumers: Get `0x09D59F8C`,
  Set `0x09D59F98`, Add `0x09D59FA4` (32-bit, capped at `0x3B9AC9FF`). The field lives outside the
  checksummed payload; `SetTrainer` patches only those 4 bytes and keeps the rest of the parasite data.
  `MercuryTrainer.Coins` is a `uint`.

### Inventory (2026-10-02)

- The native UI crash was caused by the adapter inheriting `SaveFile.Inventory` (an empty bag), while `SAV_Inventory` accesses `Pouches[0].Items[0]`. The adapter now supplies `MercuryPlayerBag`; the native inventory dialog and its operation flow are retained.
- ROM `08099E44` branches to `09D3E49C`, which copies 40 bytes from `09DD7240` into `0203988C`. Five pointer/capacity pairs: Items `0203BB20/450`, KeyItems `0203C228/75`, Balls `0203C354/50`, TMHMs `0203C41C/128`, Berries `0203C61C/75`. Records are four bytes: u16 item and u16 count. The active getter `08099DA0` is `ldrh; bx lr`, not the unreachable old XOR sequence after it.
- `09D56DFC` loads section 13 `[450,FF0)` into `0203B498`; `09D56DC0..09D56DD0` loads sector 30 `[0,FF0)` into `0203C038`. Therefore the 3112 bag bytes map to active section 13 `[AD8,FF0)` followed by sector 30 `[0,710)`. Do not substitute retail SaveBlock1 bag offsets.
- PC items use SaveBlock1+298, thirty four-byte records (`0809A304/0809A33C`); their edits require section 1 checksum updates. The bag tail edits do not change payload checksums. All paths write only changed bytes and preserve unrelated tails/footers and the inactive slot.
- Unified native inventory buffer: Items offset 0, KeyItems 1800, Balls 2100, TMHMs 2300, Berries 2812, PC 3112; length 3232. Quantity cap 999 from `0809A04A` / `0809A0D8`; no retail HM index restriction is reused. Item categories come from this ROM's pocket field.
- Icons: `08098974` reads `[0809899C]=093C8100`, entry `item*8`, tiles/+4 palette. `0809872C` copies three rows of three 8x8 tiles into a four-tile-wide staging buffer: visible source is 24x24, 4bpp. Native dialog now reads these resources instead of retail item conversion. Entry 729 remains undecoded by this direct path; no claim that the item lacks an icon.
- Main verification: real save loaded with 64/11/6/15/10 occupied bag slots and zero PC entries. No-change inventory roundtrip is byte-identical; first/last slots of all six pouches roundtrip in memory, changing only intended bytes plus PC checksum. Source file was unchanged. The native inventory dialog opened and displayed item names/counts/icons; it was cancelled without saving.

#### Item 729 follow-up

Main traced the bag cursor path `081085EE -> 0809A798` (raw u16 item id from the selected pouch), `081085FE -> 080988E8 -> 08098758 -> 08098974`. The examined path does not remap the id. Graphics go via `0800EBB4 -> 081E3B70 -> BIOS SWI 11` (LZ77), with no custom decoder in that chain.

The entry at `093C97C8` points to `09100840` and `09100930`. Raw prefixes are respectively `B6 C8 D9 EC 04 1A 34 4C` and `B6 A9 A2 9C A2 AE BD CE`; neither has the required LZ77 type byte 10. Treating the remaining three bytes as lengths would request 15522248 / 10265257 bytes, not a 288-byte icon / 32-byte palette. These are source-resource inconsistencies on the confirmed path, not a valid alternative encoding established by evidence.

No correct replacement resource or alternate consumer has been established. The editor preserves item 729 and its quantity, does not substitute a retail image or change the ROM, and now gives the native image cell an explicit undecodable-ROM-resource tooltip. Restoring its actual image remains open; this diagnostic is not claimed as image recovery.

## 5. Known limitations / open items

- Boxes 19-21 are ROM-instruction proven but have no populated sample; final validation uses
  synthetic full-coverage data, not real-hardware acceptance.
- The type-override field has no write API; only the automatic clear on species change is implemented.
- All bytes outside explicitly edited fields and their required section checksums are preserved,
  including the inactive slot, sectors 28-29, special-sector footers and any RTC trailer. Editing
  boxes 19-21 changes their mapped data in sectors 30-31; editing coins changes its mapped parasite bytes.
