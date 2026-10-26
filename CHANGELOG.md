# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.1.5.3] - 2026-10-26

### Added
- A **Mahjong** volume slider in the game's Settings > Audio (requested), after Master volume, Music and Sound effects.
  It sets the level of every table sound (the spoken calls, the tiles and the Reset table sounds) independently of
  Sound effects: the game's Master volume still applies, and at 0% the table is silent. It is the game's own slider row
  (`MoonSettingsScreen.SliderRow`, added by a Harmony postfix on `MoonSettingsScreen.Build`) and is saved in the mod's
  config as `Audio.Volume`.

### Changed
- All table sounds, tile noises included, now play on the table's own audio sources (`MahjongAudio`) instead of the
  game's shared one-shot pool. The game puts Master x Sound effects on the audio listener, so, like the game's music,
  these sources ignore the listener and apply Master x Mahjong themselves; the game's 3D setup
  (`MoonOneShot.Configure`) still shapes how they carry around the table.

## [0.1.5.2] - 2026-10-26

### Changed
- The spoken calls (riichi, chii, pon, kan, ron, tsumo) now sound as if heard over a suit radio (requested: in theme
  with the space suits, and fun). A squelch burst opens the channel; the voice is squeezed into the radio band (450 to
  2,800 Hz, two filter passes each side), soft-clipped for a little crunch, with a faint hiss; a squelch tail closes it.
  `Table.Presentation.RadioVoice` does it in plain sample code when the clips load (mono, the same on every machine),
  with tests; no Unity audio filters, which the game's build may have stripped.

### Fixed
- Spoken calls (riichi, chii, pon, kan, ron, tsumo) could be cut off mid-word ("Rii…") or not heard at all. The table
  played every sound through the game's shared one-shot pool (`MoonOneShot`), which has a fixed number of sources for
  the whole base; when they are all busy it takes over one still playing. The calls now play on an audio source of the
  table's own, set up by the game's `MoonOneShot.Configure` like the Reset table charge, where nothing else can take them
  over. Tile noises still use the shared pool. `TableSounds.IsSpoken` picks the calls, with tests.
- A four-riichi abortive draw showed −1,000 for every player, which read as a penalty. The change column counted each
  player's own riichi deposit, though the deposit is a bet that stays on the table for the next winner. A deposit now
  leaves a player's points the moment it goes down: the centre console drops by 1,000 on declaring
  (`GameState.ShownPoints`), and the result shows only what the round itself paid (`GameScorePlayer.Change`, the
  transfer without the player's own deposit). A four-riichi draw now shows 0 for everyone; a riichi winner's change still
  includes every stick they collect, and a riichi discard that is ronned returns its deposit as before. The scores
  themselves are unchanged. The engine tracks each round's deposit (`GameScorePlayer.Deposit`), with tests.

## [0.1.5.1] - 2026-10-26

### Changed
- The round-end hologram's text is now steady (requested). It no longer shimmers or glitches; the pop-in, rise and
  gentle bob stay.

### Fixed
- Dark boxes showed behind each letter of the round-end hologram, cut off at the letters' edges. The dark halo under
  the text spread wider than the quad TextMeshPro draws each letter on, so it was clipped. The halo is gone; a thin
  dark outline, which stays inside each quad, keeps the text readable against the felt and the lounge instead.

## [0.1.5] - 2026-10-26

### Added
- A round's end now plays out on the table before the result (requested): the call (RON, TSUMO, NAGASHI MANGAN, or
  DRAW with which draw) rises as a large glowing cyan hologram high above each winner's side, or the table's centre,
  facing the viewer, flickering and lighting the felt, with a dark halo so it reads against both the felt and the
  lounge; the winning tile glows gold, bobs and is lit; the winners' hands (or the ready hands, on a draw) open as
  before; and the result screen follows 4.2 s later. `Table.Presentation.RoundEndCall` decides what is called, with
  tests.

### Fixed
- 0.1.4 logged `[Error :Il2CppInterop] Assembly MoonMahjong.Net.dll is not registered in il2cpp` at every launch. The
  table component subscribed to the crew session's reset signal with a lambda, and Il2CppInterop tried to register the
  compiler-made closure along with the component. Nothing broke, but the line looked like a failure to anyone reading
  the log. The handler is now an ordinary method hidden from il2cpp (`MahjongTable.OnResetSignal`).
- The **Reset table** button's label was hard to read near the end of a hold: white text on pale gold. The fill is now
  a deeper gold, and the label turns dark once the fill is under it.

## [0.1.4] - 2026-10-26

### Added
- Reset table (requested): when a game is stuck (paused for someone who isn't back, or its host gone), anyone sitting
  at the table can hold **Reset table** for 5 seconds to end it and open the table again. The hold button fills, its
  glow races, it shakes near the end and counts down; everyone at the table hears it charge (quiet sounds made in code,
  `Table.Presentation.ResetSounds`) and sees who is resetting. New crew messages `ResetHold` and `Reset`, accepted
  only from someone at the table and never for a running game; with the host gone each client ends its copy at once.
  Tests in `tests/MoonMahjong.Net.Tests/ResetTests.cs` and `ResetSoundsTests.cs`; details in `docs/crew-play.md`.

## [0.1.3] - 2026-10-26

### Added
- Chii chooser (requested, like the waits popup): when pressing Chii leaves more than one sequence possible, each is
  shown as 3D tiles (the discard underlined) and clicking one makes that call; the button reads "Cancel chii" until
  then. `ClientRoundState.ChiiOptions` / `ClientChooseChii`, with tests (the hand-tile way of choosing still works).

### Fixed
- Unity logged "RenderPass: Attachment 1 was created with 1 samples but 4 samples were requested" and "EndRenderPass:
  Not inside a Renderpass" every frame a tile studio filmed (the round result's 3D hand, and the popups): its texture
  asked for 4x MSAA, but the game's pipeline gives the camera's depth buffer one sample (reported; 2150 pairs while one
  result was up). The studio now renders without MSAA at twice the resolution, and scaling it down smooths the edges.

## [0.1.2] - 2026-10-25

Table aids from Mahjong Soul, and an updater.

### Added
- Waits popup (requested, as in Mahjong Soul): while choosing a riichi discard, hovering one of the allowed tiles
  shows the tiles the hand would then win on, as 3D tiles filmed like the round result's, each with how many are left
  (four less the copies the player can see in their hand, the ponds, the calls and the dora indicators). The counting
  is `Table.Presentation.Waits`, with tests; the popup has its own tile studio, apart from the round result's.
- The discard the player may chii, pon or kan glows warm while the call buttons are up (requested).
- Hovering a tile in the hand greys its copies on the board (requested, as in Mahjong Soul): the face-up tiles of that
  type in the ponds, the calls and the dora indicators (`Table.Presentation.BoardTiles`, with tests). Tiles have two
  new looks for these, highlighted and dimmed, beside the table and own-hand ones.
- After declaring riichi, each drawn tile is thrown by itself after a moment (requested, as in Mahjong Soul), unless
  it lets the player tsumo or kan, which they still decide (`ClientRoundState.RiichiAutoDiscard`, with tests).
- "Update Moon Sweeper Mahjong.bat" in the release zip (requested): when the mod is installed, it asks GitHub for the
  latest release and, if newer, downloads it, checks it against the SHA-256 GitHub publishes for it, and runs that
  release's own installer to reinstall with the settings kept. It is the installer script with `-Action Update`;
  nothing changes if GitHub can't be reached, the download doesn't match, or the game is running.

## [0.1.1] - 2026-10-25

### Fixed
- The tables at three of the base's four stations showed chess pieces and would not start a game (each try failed,
  and after three the table turned itself off). The game builds the other lounges by cloning one that the mod had
  already set up: a clone carries the mod's component and table top, but none of the component's fields, and its own
  chess table spawns a fresh set of pieces, so the mod skipped setting it up. Any table not yet set up is now set up
  (copied table top replaced, chess pieces hidden), including at its first frame. Found by a crew at the east station;
  every earlier test had used the station the save starts at.
- Pressing Riichi with more than one possible riichi discard changed nothing on screen (reported: "the button didn't
  react"), and pressing it again silently took the riichi back. Now the button reads "Cancel riichi", the status line
  says to choose the tile, and the discards that keep the hand ready (the only ones that can be picked) stand raised.

### Added
- `[Developer] DevTable`: with `DevAutoPlay`, sit at a given station's table (north, south, east or west) instead of
  the nearest, so the other stations' tables can be checked too.
- A log line at each of the local player's turns (whether riichi and tsumo were offered, and the hand), so a report
  like "riichi wasn't offered" can be checked. New engine tests drive a real client on fixed walls and confirm riichi
  is offered on the draw that makes it possible (also after passing on a call), and only ready-keeping discards can
  be chosen after pressing it.

## [0.1.0] - 2026-10-25

The first release: the mahjong table, played alone against computer players or with up to three crewmates.

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
  uninstall removes exactly that plus what BepInEx generated, asks whether to keep the mod's settings (a later install
  picks kept settings up again rather than mistaking them for another mod loader), and restores a mod loader it moved
  aside (a foreign BepInEx is backed up only with consent). The .bat is a batch header that hands
  the rest of the file to Windows PowerShell 5.1, since plain batch can't reliably read Steam's library list or a
  manifest. Tested end to end against fake Steam libraries and game folders, including running the packaged .bat.
- Public-repository files: the GPLv3 `LICENSE`, a player-facing README, and `installer/THIRD-PARTY-NOTICES.txt`
  with every bundled component's licence (texts in `installer/licenses/`).
- Spectators for crew play: `MatchHost` takes spectators (told the game start with no seat, then every round), and
  `IServerRoundPlayer.SeesAllHands` (default: `IsSpectator`, as in OpenRiichi, whose spectators are told every hidden
  tile) lets crew play's watchers see hidden hands face down, exactly as the players do.
- Phase 2 groundwork: `MessageCodec`, a compact binary wire format for every engine message (crew play sends them
  between crewmates). Decoding treats the bytes as untrusted and rejects unknown types, truncated or trailing data,
  out-of-range tiles, seats and tile types, overlong names and other format versions.
- **Crew play (Phase 2):** up to four crewmates with the mod play at one table over the game's own Steam session;
  bots take the other seats and anyone else with the mod watches. New `src/MoonMahjong.Net` (no Unity, 89 tests over
  an in-memory network): `TableProtocol` (a small envelope around `MessageCodec`, keyed by table), `TableSession` (the
  lobby: the first to sit hosts, simultaneous claims go to the lower Steam ID, the host picks the length and deals;
  then hosting or following the game), `HostedGame` (the authoritative `MatchHost`, each seat's messages sent only to
  its owner and kept so anyone who asks (on joining, restarting or loading the table late) is replayed up to date, the
  public stream to watchers, and the pause), and `TableNotices` (the HUD's wording). The host accepts a seat's actions
  only from its owner; clients accept game messages only from their game's host, lobby claims only from someone at the
  table, and a game start only from a crewmate who plays in it and hosts or sits at the table; malformed or forged
  messages are counted and dropped. A failed view of a game recovers (the host cancels it for everyone, anyone else
  is replayed it), a missed End is caught from the host's next lobby, and a silent lobby host is forgotten after 10 s.
  Known limits (a modified client bending the rules for its own seat; no resend of a message Steam failed to send)
  are listed in the doc. Decisions as agreed:
  seats belong to chairs, people join before the deal only, anyone not in their own chair pauses everyone (the HUD
  names who the table waits for, and tells someone in the wrong chair where theirs is), a host who leaves the crew
  ends the game after 3 minutes (with a countdown), and a player gone 3 minutes is played automatically so the others
  can finish. Details in `docs/crew-play.md`.
- Plugin transport (`SteamCrewLink`, `CrewHub`): Steam Networking Messages on channel 3, which the game never reads,
  through Steam's flat C API. Each modded client sets the lobby member data `msm` to the protocol version and mod
  messages go only to members showing it, so crewmates without the mod never receive traffic they would leave unread.
  If Steam's networking API is missing, crew play switches off and tables still work with bots.

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

- Every table now runs through its crew-play session (`MahjongTable` + `LoungeChairs`); playing alone is a session
  with nobody else in it, hosting one person and three bots, so solo and crew play share one code path.
  `MatchController` only draws a game it is given. The game itself keeps running while the player stands (paused by
  the host until they sit back down); a lone player's game still follows them to whichever chair they take. Player
  names on the result plate come from Steam ("You" for the local player).
- The start plate lists who the deal would seat ("You, Rin and two computer players"); only the table's host can pick
  the length and deal, and everyone else sees "Waiting for <host> to deal". `[Game] RoundCount` sets the length a new
  table offers first.

### Removed
- The screen's info plate (round, counters, names and scores): replaced by the centre console. Player names now
  appear only on the round result plate.

### Fixed
- A client reading several rounds at once (a crew-play replay, or a slow frame) scored a finished round only after
  the whole batch, so the next round started from stale state and later messages hit the wrong player (an exception).
  `ClientGame` now scores a round before it reads the next round's start.
- With unlimited decision time (the mod's default), a player who disconnected while the game waited on their discard
  or call was never answered for: the game hung. The engine now answers with their defaults as soon as they are marked
  disconnected, and a seat given up on (`MatchHost.SeatDisconnected`) stays disconnected in later rounds too.
- Hidden tiles (other players' hands seen from the side or behind, as when standing up) looked like white dragons:
  they borrowed the white dragon's texture, which gained a "Wh" index. They now use a blank face of their own
  (`tiles/blank.png`, the white dragon's face without the index).
- A declared riichi stick vanished once it reached the centre: OpenRiichi's stick height fits its centre piece's
  sloped edge, but the 3D console is flat-topped, so the stick ended up inside it. It now lies on the console's top.
- Tiles were invisible in-game: Unity's `Resources.UnloadUnusedAssets` (run when a save loads) destroyed the
  runtime-made tile mesh and materials because no scene object used them before the first deal. Every runtime asset
  is now flagged `DontUnloadUnusedAsset`. A one-time diagnostic (`TableView.Describe`) found it.
