# Mercury verification / 验证说明

## Scope

The Mercury check runner is `Tests/PKHeX.Mercury.Tests`. It uses synthetic save sectors by default, without redistributing a ROM or private save. It checks:

- 128 KiB and 128 KiB + 16-byte RTC containers; unedited byte-identical export.
- All 750 PC positions, including records crossing section or special-sector boundaries.
- Edited-only byte ranges and regenerated checksums; untouched backup slots, special footers, Hall of Fame sectors, parasite data and RTC trailer.
- Four 10-bit move slots, IV/EV packing, PP-ups, egg/hidden flags, the expanded 8-bit ball field, and type-override handling on species changes.
- PID constraints across ordinary/single-gender/genderless species, nature, shininess and ability storage markers.
- Corrupt, duplicate and torn sections, visible fallback to a valid older save, counter wrap.
- Independent editing copies, trainer changes, expanded 32-bit coins, and the actual stat-mode mappings.
- Party add/edit/delete and lossless preservation of party-only opaque bytes.
- Text-code byte limits, unknown glyph tokens and reserved control bytes.

Optional local inputs extend this to the exact supported ROM, existing ROM-native research data, profile save/reload, and unmodified private save files. Private-save edits take place **only in memory**, and the runner re-reads the original file to check that it has not changed. No private save fixture or its trainer details are included in the repository.

## Commands

```powershell
dotnet build PKHeX.Mercury.slnx -c Release
dotnet run --project Tests/PKHeX.Mercury.Tests -c Release --no-build -- --report artifacts/verification.json
```

Optional local-only inputs:

```powershell
dotnet run --project Tests/PKHeX.Mercury.Tests -c Release --no-build -- --rom "path/to/your.gba" --research "path/to/research" --save "path/to/your.srm" --profile-output "path/to/local-profile" --report artifacts/verification-local.json
```

`--save` may be repeated for multiple read-only inputs. `--profile-output` creates local data (including the user's ROM cache); do not commit or include that directory in a release.

## Interpretation

A passing binary/semantic check is not a claim that every modified Pokémon is obtainable in-game, that retail PKHeX legality applies, or that the game has been played successfully on hardware. The current scope does not include an emulator or console gameplay session. Preview graphics use the first frame and first palette page and are not a full animation/state simulation.

The original upstream tests are retained but are not used as evidence of Mercury compatibility. Mercury has its own test entry, solution and build workflow. Check results should always be tied to the source revision that generated them, rather than copied from earlier resource-extraction reports.

## v0.1.0 local release checks

- The complete Mercury solution built with **0 warnings and 0 errors** using .NET SDK 10.0.401.
- **20 binary/data scenarios passed**, including all 750 box positions, the supported ROM, comparison to the existing ROM-native research, and read-only roundtrips of three local save samples.
- **14 WinForms assertions passed**: real window/control creation, loading a synthetic file, draft isolation, Apply/Discard, copy to box 25 slot 30, cut/move, default new output filename, and export/reopen. Run this entry with `dotnet run --project Tests/PKHeX.Mercury.UITests -c Release -- artifacts/ui-checks`.
- The **self-contained published executable** opened the expected main window and closed normally with exit code 0. No private save was opened in that executable startup check.
- No emulator or physical-console gameplay acceptance was performed. These results establish the stated file/data/UI scenarios, not arbitrary edited-game behavior.

Machine-readable reports accompany the release. Local outputs are under `artifacts/` (ignored by Git); they contain check names and outcomes, not private trainer names or IDs.
