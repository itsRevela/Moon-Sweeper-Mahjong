# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- **Mod loader for Unity 6000.6 / IL2CPP metadata v108.** No released loader supports this game's Unity version.
  BepInEx 6 now runs Cpp2IL `development` (which reads v108) out of process, because that build needs a newer
  AsmResolver than Il2CppInterop. Il2CppInterop gained native-struct handlers for 6000.6's changed `Il2CppClass` and
  `Il2CppAssembly`. Without these, interop generation and class injection fail. See `docs/loader.md` and `patches/`.
- `scripts/loader.py`: installs the loader into the game folder without overwriting anything, records what it added,
  and uninstalls exactly that (`--purge` also removes generated files), so the game folder can always be restored.
- Riichi engine port (`src/MoonMahjong.Core`): tiles, wall, round and game state, hand reading, yaku, fu and scoring
  from Practice-Riichi-Mahjong, plus porting conventions in `docs/porting.md`, so the rules match the reference
  implementation the user maintains. The original HandTests and ScoringTests are ported to xUnit.
- Authoritative server, call validation and priority, bots and the client/server protocol, ported the same way. The
  bots run on the game loop (`Tick()`) instead of their own threads, so they are safe to drive from Unity.
- `MatchHost` (rounds → score → next round) with `MatchSeat`s: `BotSeat` for computer players and `ClientGame` +
  `LocalClientSeat` for the local human, who uses the same protocol a remote crewmate will in Phase 2.
  127 engine tests, including full bot-only hanchans and games with a scripted human seat, check that the games
  finish, that points are conserved and that every seat agrees with the host.
- Table layout model (`src/MoonMahjong.Table`, `TableLayout`): a Unity-free port of OpenRiichi's 3D table layout and
  animation (walls, wall split and dead wall, dora and ura-dora flips, hands, ponds, calls, riichi sticks, centre
  piece) on a minimal scene graph with OpenRiichi's quaternion maths, curves and action queue. The plugin feeds it
  round events with the current time and reads one pose per tile, already converted to Unity's axes (observer on -Z,
  one unit = one tile width), so tiles sit and move exactly as in the reference client. Time is continuous rather than
  frame-stepped, so results do not depend on the frame rate. Coordinates, proportions and every deviation are in
  `docs/table-layout.md`; 165 tests in `tests/MoonMahjong.Table.Tests` cross-check it against Core's wall.
- In-game table: the chess corner of each of the base's four lounges becomes a mahjong table with NaoMahjong's felt,
  two chairs are added on the free sides, and the seated camera views the table from whichever of the four seats
  the player chose. The chess logic is switched off on those tables. Seats 0–6 keep the indices that crewmates
  without the mod use (they ignore the new seats 7 and 8).
- 3D table top built from NaoMahjong's flat table painting (requested: the flat felt looked bland):
  `scripts/table_mesh.py` sweeps a frame → raised rosewood rail → dark lip → recessed felt profile around the
  table with mitred corners, plus a bevelled centre console. `prepare_assets` cuts the painting into frame, rail,
  felt and console textures, and inpaints the console's painted outline out of the felt (the console is now
  geometry). Per-material tints bring the lounge's lighting up to the painted colours.
- `DevAutoPlay` config option (off by default): continues the current save, sits at the nearest mahjong table and
  saves screenshots, so in-game changes can be checked without manual play.
- Asset pipeline: `scripts/fbx_mesh.py` converts NaoMahjong's binary FBX tile into a JSON mesh oriented for Unity
  (face on +Z, unmirrored), and `scripts/prepare_assets.py` gathers the 37 tile textures and the table felt. This
  avoids an AssetBundle build that could mismatch the game's URP shaders.
- Reverse-engineering tooling: `scripts/RvaDump` (method names for 55k code addresses), `scripts/ghidra/MsDecompile.java`
  and `scripts/decompile.py`, to read how seating, the camera and crew sync work. Notes are in
  `docs/reverse-engineering.md`.
- Smoke-test plugin (`LoungeProbe`) that verifies plugin loading, class injection and Harmony patching in-game, and
  logs the lounge layout and materials.
- No per-turn time limit for real players (requested): `ServerSettings.UnlimitedDecisionTime` (a mod addition to
  the ported engine; off by default for parity with OpenRiichi) stops the server from arming its decision timer,
  so it waits for a person's own discard or call instead of playing a default for them. The plugin turns it on
  through `[Game] DecisionTimeLimit = 0` (the default); a positive value restores a timer of that many seconds.
  Bots still act immediately.
- First playable match in-game (`MatchController`, `TableView`, `MahjongHud`): the local player against three bots,
  tiles drawn at `TableLayout`'s poses, tile picking with the mouse through the client's selection groups, call
  buttons and score, result and game-over plates built with the game's own UI kit (`MoonWorkshopKit`).

- Corner indices on every tile face (requested), so tiles read at a glance without knowing the kanji: the number on
  suited tiles (a red 5 on red fives), E S W N on winds, and their colour on dragons: G (green), R (red) and Wh
  (white, since "W" is West). `scripts/tile_index.py` bakes them in at asset-build time over the unchanged art, with a face-coloured
  outline where they overlap it; the 42 x 59 px source faces are upscaled 4x first so the index stays crisp.

- The round, honba and riichi counts, tiles left, and every seat's wind and score are shown on the table's centre
  console instead of a screen plate (requested). `ConsoleLayout` (Table.Presentation, unit-tested) arranges them as on
  an automatic table: the round block in the middle facing the local player, each score along its own seat's edge
  facing that seat, the current turn's score in orange, all inside the line where riichi sticks rest.
  `CenterDisplay` draws them as world-space TextMeshPro text in the game's own fonts.
- Leaving the table pauses the game (requested): the match runs on its own clock (`MatchClock`) that advances only
  while the local player is seated, so standing up freezes the server, the bots and the tiles, and sitting back down
  carries on from the same moment (the table turns to whichever chair they take). The table keeps showing the paused
  game. `[Developer] DevPauseTest` exercises it from the dev loop.
- A start screen (requested: sitting down used to deal at once): the first player to sit picks East only (4 rounds)
  or East + South (8 rounds), then deals. `[Game] RoundCount` sets which is preselected (`GameLength`, unit-tested).
- A redesigned round result (requested: it looked messy). `ResultSheet` (unit-tested) structures it: who won and
  how, who dealt in and the round, each yaku with its han in aligned columns, the han/fu/limit summary and the points,
  and everyone's points with green or red changes (or the final ranking after the last round). The winners' hands
  show as real 3D tiles: `TileStudio` films the tile meshes with their materials, in a small lit set at the world's
  origin, into a texture on a tray across the plate, with the winning tile set apart and underlined, melds after
  it with the called tile sideways and a closed kan's outer tiles face down (`WinningHand`, unit-tested).
- Tile sounds (requested): OpenRiichi's slide, draw, discard, dora flip, riichi, reveal, chii, pon, kan, ron and
  tsumo sounds, cued exactly where its GameScene plays them (`TableLayout.TakeSounds`, unit-tested) and played at the
  table through the game's own `MoonOneShot`, so they follow its audio settings. `scripts/sound_assets.py` turns the
  WAVs into raw float samples at asset-build time (no WAV parser in the plugin).
- Rounded tiles with a hairline gap (requested: tiles sat perfectly flush): `scripts/tile_shape.py` rebuilds the tile
  as a box with 1.6 mm rounded edges and smooth normals, 0.8 mm narrower and shorter so neighbours never touch (the
  thickness is unchanged, so the walls' layers still rest on each other), keeping every face's texture mapping.

- A release installer for players (`installer/`, packaged by `scripts/package_release.py` into
  `dist/MoonSweeperMahjong-v<version>.zip`): double-click one .bat that finds Moon Sweepers through Steam's registry
  and library folders (or asks for the folder), refuses to run while the game is open, and installs or uninstalls.
  Install records every file with its SHA-256 in the same manifest as the developer installer (`scripts/loader.py`);
  uninstall removes exactly that plus what BepInEx generated, asks whether to keep the mod's settings, and restores a
  mod loader it moved aside (a foreign BepInEx is backed up only with consent). The .bat is a batch header that hands
  the rest of the file to Windows PowerShell 5.1, since plain batch can't reliably read Steam's library list or a
  manifest. Tested end to end against fake Steam libraries and game folders, including running the packaged .bat.
- Public-repository files: the GPLv3 `LICENSE`, a player-facing README, and `installer/THIRD-PARTY-NOTICES.txt`
  with every bundled component's licence (texts in `installer/licenses/`).
- Phase 2 groundwork: `MessageCodec`, a compact binary wire format for every engine message (crew play sends them
  between crewmates). Decoding treats the bytes as untrusted and rejects unknown types, truncated or trailing data,
  out-of-range tiles, seats and tile types, overlong names and other format versions.

### Changed
- The lounge's seat prompt says "Play mahjong" instead of "Play chess" at the mahjong chairs (requested): a prefix on
  `MoonLoungePlayer.Show` swaps the translated chess label.
- A round's result now stays up until every person at the table presses Next round (requested); OpenRiichi also moved
  on by itself after the round-end delay. Bots and disconnected seats count as ready, and a Next round that arrives
  while the server round is still finishing is no longer lost. After the last round the host finishes at once and each
  player's final result stays up until they close it.
- The local player's own hand uses a 1.25x brighter material: seen face-on, its gloss reflects little of the bright
  lounge, so it looked duller than the tiles lying on the table (measured ivory 153 against 195).
- The centre console's honba and riichi counts get a line each, in the heavier font, and every centre line is cream:
  as one line they shrank to about 5 px tall at 1080p.
- Hands and melds fit the 3D table (requested: the rail clipped tiles). They move 0.45 tile widths toward their owner,
  centred on the frame outside the rail instead of cutting 8 mm into it, and stand on the frame's top, 8 mm above the
  felt, instead of sinking into it (`TableLayout`'s new optional `handAreaRise`, measured from the table meshes). The
  freshly drawn tile now stands upright just right of the hand instead of lying sideways beyond it, where the rail cut
  it. OpenRiichi's layout is unchanged otherwise; see `ModLayout` and docs/table-layout.md.
- Called melds now run in a row along the table edge, leftwards from each player's right corner (requested: a second
  pon stacked toward the centre, as OpenRiichi lays them out).

### Removed
- The screen's info plate (round, counters, names and scores): replaced by the centre console. Player names now
  appear only on the round result plate.

### Fixed
- A declared riichi stick vanished once it reached the centre: OpenRiichi's stick height fits its centre piece's
  sloped edge, but the 3D console is flat-topped, so the stick ended up inside it. It now lies on the console's top.
- Tiles were invisible in-game: Unity's `Resources.UnloadUnusedAssets` (run when a save loads) destroyed the
  runtime-made tile mesh and materials because no scene object used them before the first deal. Every runtime asset
  is now flagged `DontUnloadUnusedAsset`. A one-time diagnostic (`TableView.Describe`) found it.
