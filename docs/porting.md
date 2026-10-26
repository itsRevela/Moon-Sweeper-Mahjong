# Porting the Riichi engine from Vala to C#

`src/MoonMahjong.Core` is a C# port of the game logic in
[itsRevela/Practice-Riichi-Mahjong](https://github.com/itsRevela/Practice-Riichi-Mahjong)
(a fork of FluffyStuff/OpenRiichi, **GPLv3**). The port is GPLv3 as a result. The source clone lives in
`tools/Practice-Riichi-Mahjong` (git-ignored); pinned commit: `96a085b` (2026-09-27).

The goal is a **faithful** port: same algorithms, same control flow, same rules. Don't "improve" the logic
while porting; behaviour differences make the ported tests meaningless. Fixes go in separate, documented changes.

## What is ported

| Vala source | C# file(s) | Notes |
|---|---|---|
| `Game/Logic/Tile.vala` | `Tile.cs` | `INT_TO_WIND`/`NEXT_WIND`/... live in `static class Winds`; `TILE_TYPE_TO_STRING` in `TileTypes` |
| `Game/ServerSettings.vala` | `ServerSettings.cs` | immutable record; disk I/O dropped |
| `Engine/Helper/RandomClass.vala` | `RandomClass.cs` | wraps `System.Random`; `IntRange` max is exclusive like GLib |
| `Game/Logic/TileRules.vala` | `TileRules.cs`, `RoundStateCall.cs`, `RoundStateContext.cs`, `PlayerStateContext.cs`, `HandReading.cs`, `TileMeld.cs`, `TilePair.cs`, `Scoring.cs`, `Yaku.cs` | one public type per file |
| `Game/Logic/RoundState.vala` | `RoundState.cs`, `RoundStatePlayer.cs`, `RoundStateWall.cs` | |
| `Game/Logic/GameState.vala` | `GameState.cs`, `GameScorePlayer.cs`, `RoundScoreState.cs`, `RoundFinishResult.cs` | |
| `Game/Logic/GameStartInfo.vala`, `GamePlayer.vala` | `GameStartInfo.cs`, `RoundStartInfo.cs`, `GamePlayer.cs` | `AnimationTimings` dropped from `GameStartInfo` |
| `Game/Logic/ClientAction.vala`, `ServerAction.vala`, `ClientMessage.vala`, `ServerMessage.vala`, `AnimationTimings.vala` | `Protocol/*` | in-game messages only; no serialisation yet (Phase 2) |
| `Game/Logic/ClientRoundState.vala` | `ClientRoundState.cs`, `TileSelectionGroup.cs` | the human player's round state machine |
| `Game/GameController.vala` | `ClientGame.cs` | views removed; transport-agnostic (`Receive` / `MessageSent`) |
| `GameServer/GameState/*` | `Server/ServerRoundState.cs`, `RegularServerRoundState.cs`, `ServerRoundStateValidator.cs`, `ServerRoundStatePlayer.cs`, ... | `LogServerRoundState` and game logs dropped |
| `GameServer/Server/ServerGameRound.vala` | `Server/ServerGameRound.cs`, `RegularServerGameRound.cs` | players behind `IServerRoundPlayer` |
| `GameServer/Server/Server.vala` (`process`) | `Server/MatchHost.cs` | rounds → `GameState` → next round; seats are `MatchSeat` |
| `GameServer/Bots/*` | `Server/Bot.cs`, `SimpleBot.cs`, `NullBot.cs`, `BotConnection.cs`, `BotSeat.cs` | **thread replaced by `Tick()`** (single-threaded, ticked by the host) |
| `Engine/Helper/Timers.vala` (`DelayTimer`) | `Server/DelayTimer.cs` | |
| `Game/Rendering/GameRenderView.vala`, `GameScene.vala`, `Render*.vala`; `Engine/Properties/{Quat,Vec3,Animation,Curve,Path,Transform}.vala`, `Engine/Rendering/World/WorldObject*.vala` | `../MoonMahjong.Table/Layout/*` | layout and animation only; see `docs/table-layout.md` |

Not ported: rendering (except the table's layout and animation, above), menus, audio, the lobby/networking layer, the point-calculation and tenpai-speedrun
training modes. The mod has its own UI (Unity) and transport (Steam via the game's crew session).

## Naming rules (mechanical, so cross-file references can be predicted)

1. **Types** keep their Vala names (`TileRules`, `RoundStateCall`, `HandReading`, ...).
2. **Enum members**: `SCREAMING_SNAKE` → `PascalCase` by splitting on `_` and capitalising each part:
   `TileType.MAN1` → `TileType.Man1`, `Wind.EAST` → `Wind.East`,
   `RoundStateCall.CallType.CLOSED_KAN` → `RoundStateCall.CallType.ClosedKan`,
   `YakuType.DOUBLE_RIICHI` → `YakuType.DoubleRiichi`.
3. **Public/internal methods, properties and fields**: `snake_case` → `PascalCase`
   (`can_ron` → `CanRon`, `tile_type` → `TileType`, `get_chii_groups` → `GetChiiGroups`).
   All-caps identifiers stay (`ID`).
4. **Private fields and locals**: `snake_case` → `camelCase`.
5. **Named constructors** (`RoundState.server(...)`) become static factory methods with the PascalCase name
   (`RoundState.Server(...)`) unless an overload of the main constructor is unambiguous.
6. **Global functions** move into a static class next to the type they belong to (see `Winds`).

## Type mapping

| Vala | C# |
|---|---|
| `Gee.ArrayList<T>` | `System.Collections.Generic.List<T>` |
| `list.size` / `list.add` / `add_all` / `remove_at` / `index_of` / `contains` / `clear` | `Count` / `Add` / `AddRange` / `RemoveAt` / `IndexOf` / `Contains` / `Clear` |
| `list.slice(a, b)` | `list.GetRange(a, b - a)` |
| `list.sort(cmp)` | `list.Sort(cmp)` (keep the comparer semantics) |
| `HashMap<K,V>` | `Dictionary<K,V>` |
| `T[]` | `T[]` |
| `T?` | `T?` (nullable reference types are enabled) |
| `owned` / `unowned` / `weak` | remove |
| `Object` base class | remove (plain class) |
| `Serializable` base class | remove; wire serialisation is designed separately |
| `signal void x(args)` | `public event Action<args>? X;` |
| `ServerSettings.x == OnOffEnum.ON` | `settings.X` (bool) |
| `RandomClass` | `MoonMahjong.Core.RandomClass` |
| `int.abs()` / `int.clamp(a,b)` | `Math.Abs` / `Math.Clamp` |
| `x.to_string()` | `x.ToString()` |

## Mutability

The Vala code mutates shared state (tiles are revealed in place, round state advances in place). The port keeps
that so the algorithms stay identical. New code around the engine (plugin, networking) should treat engine
objects as owned by the round and not share them across threads.

## Tests

`source/Tests/HandTests.vala` and `ScoringTests.vala` are ported to xUnit in `tests/MoonMahjong.Core.Tests`
with the same names (PascalCase) and the same hands. `PointCalcTests.vala` is not ported (training mode).
