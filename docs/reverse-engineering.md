# Reverse-engineering Moon Sweepers (for the mahjong mod)

Game: `C:\Program Files (x86)\Steam\steamapps\common\Moon Sweepers` (Steam app 5335510), Unity 6000.6.0f1, IL2CPP,
metadata v108, build GUID `ae952d63335147dc91736f44358c115c` (2026-10-25). Everything under `extracted/` is derived
from the game and git-ignored; regenerate it with the commands below.

## Type signatures (no method bodies)

```bash
B=tools/Cpp2IL/Cpp2IL/bin/Release/net10.0
$B/Cpp2IL.exe --game-path "<game dir>" --output-as diffable-cs --output-to extracted/cs        # readable C#
$B/Cpp2IL.exe --game-path "<game dir>" --use-processor attributeinjector --output-as dll_default \
    --output-to extracted/cpp2il-dll                                                            # DLLs with RVAs
```

Game code is `extracted/cs/DiffableCs/Assembly-CSharp/MoonCleaners/` (363 types, namespace `MoonCleaners`).
BepInEx's interop assemblies keep that namespace and expose private members publicly
(e.g. `MoonGameLounge._Chess_k__BackingField`).

## Method bodies (Ghidra)

1. Names: `dotnet run --project scripts/RvaDump -c Release -- <GameAssembly.dll> <global-metadata.dat> 6000.6.0f1 extracted/rva/names.tsv`
   (55,190 code addresses, 70,706 methods; identical-code-folded aliases are joined with ` | `).
2. One-time analysis (≈20 min): copy `GameAssembly.dll` to `extracted/ghidra/input/` and run
   `analyzeHeadless extracted/ghidra/project GameAssembly -import extracted/ghidra/input/GameAssembly.dll -max-cpu 20`
   (Ghidra 12.1.4 + Temurin 25, from the LordAndMaidenWork toolset).
3. `python -m scripts.decompile "MoonCleaners.MoonLoungePlayer$$LateUpdate"` or `--type MoonCleaners.MoonChessTable`
   writes `extracted/decompiled/<Type$$Method>.c`. The project is opened read-only.

Ghidra function names come from `names.tsv`; when several methods share code, the first name wins, so a call to
`Foo$$Bar` may really be any method listed on that line.

4. `dotnet run --project scripts/RvaDump -c Release -- annotate <GameAssembly.dll> <global-metadata.dat> 6000.6.0f1 extracted/decompiled`
   decodes every `DAT_` metadata-usage slot (v27+ slots hold `type << 29 | index << 1 | 1` until first use) and
   appends string literals, types, fields and methods as a comment block to each `.c` file.

For a quick look without the 22-minute analysis, import with `-noanalysis` into `extracted/ghidra/quick` (40 s) and
pass `--project extracted/ghidra/quick`; call names still resolve through `names.tsv`.

Float constants live in `.rdata`: map `DAT_181e...` to a file offset through the PE section table (see the snippet
history in this doc's git log) and read 4 bytes.

## Seating, camera and chess (decompiled 2026-10-25)

- `MoonGameLounge.Awake`: `Spots = GetComponentsInChildren<MoonLoungeSpot>(true)`; each gets `Lounge = this` and
  `Index = i`; `occupants = new ulong[Spots.Length]`; `Chess`/`Hockey` via `GetComponentInChildren`; every child
  renderer gets `renderingLayerMask = 2`.
- `MoonLoungePlayer.Chess` = spot kind `ChessWhite`(1) or `ChessBlack`(2); `Busy` = kind ≠ `Seat`(0).
- `MoonLoungePlayer.Enter(spot)`: `lounge.Take(index)` (broadcasts `MoonCrewSession.LoungeSpot`), `walker.Rest(...)`,
  and if `Busy`: `walker.uiBlocked = true`, `Cursor.lockState = Chess ? None : Locked`, `Cursor.visible = Chess`;
  then `MoonChessTable.LocalSat(...)` or `MoonAirHockey.LocalJoined(...)`.
- `MoonLoungePlayer.Update`: while seated, the `Interact` binding (`MoonInput` static field 0) stands up
  (`Leave` → `walker.GetUp` → `Release` → `lounge.Leave(index)`, cursor locked again).
- `MoonLoungePlayer.LateUpdate`: for chess seats, `chess.ViewFrom(kind == ChessWhite ? 1 : -1, out pos, out rot)`;
  camera = lerp/slerp from the pose at sit time by `SmoothStep(0, 1, (unscaledTime − enteredAt) / 0.45)`;
  near clip 0.03; cursor unlocked.
- `MoonChessTable.ViewFrom(colour)`: eye = `TransformPoint(0, 1.321, colour × 0.6)`, target =
  `TransformPoint(0, 0.761, colour × 0.05)`, `LookRotation(target − eye, transform.up)`. `Top` = 0.761.
- `MoonChessTable.Update` runs everything chess: `AdvanceSlides`, `ShowHud`, `Input`, `Computer`, `ShowMarks`.
  `Awake` builds piece templates and `Rebuild`s the pieces under the table.
- `MoonGameLounge.Take/Leave/CrewSpot` → `Changed(spot)` → `MoonChessTable.SeatsChanged` / `MoonAirHockey.SidesChanged`.
  `CrewSpot` ignores indices ≥ its `occupants.Length`, so crewmates without the mod ignore spots 7 and 8.
- `MoonSession.StartShift()` continues the current save (used by the game's `-moonbench` mode and our dev loop).
- Class static fields are at `Il2CppClass + 0x98` in 6000.6 (was `0xB8`), consistent with the new struct layout.

## What matters for the mod

| Thing | Where | Notes |
|---|---|---|
| Lounge prefab | Resources `Moon/GameLounge/GameLounge`, installed by `MoonGameLounge.Install(MoonCollector)` | lives in the moon base, not the main menu |
| Chess table | GameObject `Chess table` (local pos −13.55, 0.5, 5.15; scale 1.1) with `BoxCollider` + `MoonChessTable` | `pieceTemplates` child holds 12 piece meshes |
| Chess chairs | `Chair \| chess white` (z +0.88 from table, facing −z) and `Chair \| chess black` (z −0.88) | `MoonLoungeSpot` kind `ChessWhite`/`ChessBlack`; mesh `Lounge Chair` |
| Seating | `MoonLoungePlayer` (exec order 210): `Aimed`, `Enter(spot)`, `Leave`, `LateUpdate` (camera blend, 0.45 s) | occupants are Steam IDs in `MoonGameLounge.occupants` |
| Crew sync | `MoonCrewSession`: Steam Networking Messages, byte `Kind` (LoungeSpot=39, Chess=40, ChessAsk=41), `Protocol = 38` | crew max 16 (`MoonSteamLobby.MaxCrew`) |

Seat layout from the prefab (`extracted/` UnityPy dump): the two new chairs go on the table's ±X sides at the same
0.88 × 1.1 offset, facing the table.

**There are four lounges** (observed 2026-10-25, one save): the base sits on a ~2,000 m sphere and each station has
its own `Game lounge`, rotated to its local up (e.g. world (0, 1992, −11), (0, −2001, −11), (2014, −11, 0),
(−1974, 11, 0)) with slightly different scales (chess table lossy scale 1.09–1.11). Everything the mod places must
be parented to and positioned in the lounge's (or table's) local space. `MoonChessTable.Awake` runs once per lounge.
Lounge spot order (index = sync index): 0 HockeyLeft, 1 HockeyRight, 2 ChessWhite, 3 ChessBlack, 4–6 cafe Seats.
