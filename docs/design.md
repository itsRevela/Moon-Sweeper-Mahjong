# Moon Mahjong design

## Goals (from the request)

- Replace the lounge chess set with an interactive Riichi mahjong table.
- Four seats: keep the two chess chairs and add two on the table's other sides.
- Up to four players; bots fill empty seats when a game starts.
- Assets from NaoMahjong; rules from Practice-Riichi-Mahjong.

Delivery is phased (agreed 2026-10-25): **Phase 1**, one human against three bots, fully playable and checked
in-game. **Phase 2**, crew play over the game's Steam session.

## Layers

```
MoonMahjong.Core      Rules engine (Vala port, GPLv3). No Unity, no I/O, no threads.
  RoundState / GameState / TileRules ...      the reference logic, unchanged in behaviour
  Server/ServerRoundState, Validator, Bots    authoritative round driver; emits events, takes player actions

MoonMahjong.Table     Unity-free session and presentation model (testable).
  MatchHost           owns GameState + ServerRoundState; seats = Human(local) | Human(remote, Phase 2) | Bot
  SeatView            per-seat client view built only from events that seat may see (no hidden-tile leaks)
  TableLayout         where every tile sits (wall slots, hands, ponds, melds, dora), from OpenRiichi's Render*.vala;
                      animated pose per tile at a given time (Layout/, see table-layout.md)

MoonMahjong.Plugin    BepInEx/Unity glue. Thin: converts model → GameObjects, input → actions.
  LoungeInstaller     swaps the chess table for the mahjong table, adds two chairs (Harmony on lounge setup)
  MahjongAssets       loads build/assets (tile mesh JSON, 37 PNGs, felt) with the game's own shader
  TableRenderer       one GameObject per tile; each frame, places it at TableLayout's pose for Time.time
  SeatCamera / Input  camera over the local seat; raycast tile picking; call buttons (Unity UI)
```

The host is authoritative even when local: the human player sends the same actions a remote client would, and sees
only what a remote client would see. Phase 2 then only adds a transport for those messages.

## Running a match (as built)

```
MatchHost(startInfo, settings, rnd, seats[4], time)     // GameState + one RegularServerGameRound per round
  seats: LocalClientSeat(ClientGame)  – the local human; the plugin wires ClientGame.RoundCreated to the view/HUD
         BotSeat(new SimpleBot(rnd))  – empty seats
host.Process(Time.time) every frame   // server step, then every seat ticks (bots decide, the client handles messages)
```

UI rule: read the `ClientRoundState`'s button/selection state **after** `Process` returns, not inside its events:
it raises `SetTimerState(true)` before it publishes the selectable tile groups. Between rounds the HUD shows the
result and calls `ClientGame.ScoresAcknowledged()`. The host starts the next round only once every person has done
so (bots and disconnected seats count as ready; OpenRiichi also moved on after a delay).

Pausing: the plugin drives the match with a `MatchClock` that advances only while the local player sits at the table,
so standing up freezes everything and sitting back down resumes at the same moment. Phase 1 has one person per table,
so the paused game follows them to whichever chair they take.

Where the state shows: the screen HUD (`MahjongHud`, the game's UI kit) has only what needs a click or is
transient: the start plate (East only or East + South, then Deal), call buttons, the status line (furiten; a
countdown only with `DecisionTimeLimit`) and the round result (`ResultPlate` lays out a `ResultSheet`; the winners'
hands are real tiles filmed by `TileStudio`, an offscreen camera at the world's origin). Kit text boxes clip lines
that do not fit, so the HUD sizes them to their text (`KitText.Fit`). The round, counters, tiles left and scores lie on the centre console (`ConsoleLayout` arranges them,
`CenterDisplay` draws them in TextMeshPro): each score along its own seat's edge facing that seat, inside the riichi
sticks' line.

## Time

The engine is driven by `Process(time)` from Unity's `Update` with `Time.time`. The server's animation delays
(`AnimationTimings`) keep bots from acting faster than the tiles can move; decision time comes from `ServerSettings`.

## In-game hook points (from reverse engineering, see reverse-engineering.md)

- The lounge is the Resources prefab `Moon/GameLounge/GameLounge`; the chess table is `MoonChessTable` on
  `Chess table`; its chairs are `MoonLoungeSpot`s of kind `ChessWhite`/`ChessBlack`.
- Seating, the camera and input-blocking are `MoonLoungePlayer`'s job; we reuse its seat flow and override the view
  while the local player sits at the mahjong table.
- Crew sync (Phase 2) is `MoonCrewSession` over Steam Networking Messages. The mod uses its own message channel so
  players without the mod are unaffected; only crewmates who have the mod can sit at the mahjong table.

## Phase 2 seating (decided 2026-10-25)

With more than one person at a table, every game seat belongs to its chair:

- Anyone standing up pauses the game for everyone; it resumes once every person is back in their own chair.
- A person who sits in a chair that is not theirs is not seated into the game: the prompt points them to their own
  chair. People back in place see who the table is waiting for ("Waiting for <name>").
- Taking over a bot's seat by sitting in its chair was considered and rejected (too easy to grab another hand by
  accident).

With one person at a table (Phase 1), the paused game keeps following them to whichever chair they take.

## Defaults

Rules default to `ServerSettings.Default` (aka dora on, open riichi off, multiple ron on, triple-ron draw on), with
no decision time limit for people (`[Game] DecisionTimeLimit` 0; bots answer at once). Game defaults are
OpenRiichi's (`ServerMenu.create_start_info`): 25,000 starting points, uma +20/+10, starting dealer seat 0. The game's
length is chosen on the start plate, East only (4 rounds) or East + South (8); `[Game] RoundCount` sets which one is
preselected.
