# Moon Sweeper Mahjong

A mod for [Moon Sweepers](https://store.steampowered.com/app/5335510) that replaces the chess set in the moon-base
lounge with a four-seat Japanese (Riichi) mahjong table: you against three computer players.

- Full Riichi rules (a C# port of [OpenRiichi](https://github.com/FluffyStuff/OpenRiichi)'s engine): riichi, calls,
  dora and red fives, yaku and fu scoring, East-only or East + South games.
- A 3D table with rounded tiles, corner indices on every tile, the round and scores on the centre console, and the
  table's own sounds.
- No turn timer for people. Stand up any time to pause the game; sit back down to carry on.
- Round results with the winning hand in 3D, every yaku with its han, and everyone's points.

**Crew play** (up to four crewmates at one table over the game's Steam session) is in progress.

## Install

1. Download the latest `MoonSweeperMahjong-v<version>.zip` from [Releases](https://github.com/itsRevela/Moon-Sweeper-Mahjong/releases) and unzip it.
2. Close Moon Sweepers.
3. Double-click **Install Moon Sweeper Mahjong.bat** and answer its questions.

The installer finds the game through Steam (or asks for its folder), installs everything, and records what it added.
Run it again to uninstall or reinstall: it removes exactly what it installed, asks whether to keep your settings, and
puts back any mod loader the game had before. If Windows says it protected your PC, choose *More info*, then *Run
anyway*.

In the game, walk up to the lounge's table and choose **Play mahjong**.

Settings live in `BepInEx\config\itsrevela.moonsweepers.mahjong.cfg` in the game folder (created on the first run).

## How it works

Moon Sweepers is a Unity 6000.6 IL2CPP game whose metadata version (108) stock mod loaders can't read yet, so the mod
ships a patched [BepInEx](https://github.com/BepInEx/BepInEx) (see [docs/loader.md](docs/loader.md)). The plugin
hooks the lounge's chess table and chairs ([docs/design.md](docs/design.md)), runs the ported rules engine
([docs/porting.md](docs/porting.md)) and draws the table from OpenRiichi's layout ([docs/table-layout.md](docs/table-layout.md)).

## Building from source

Prerequisites: Windows, .NET SDK 10, Python 3.12+ with Pillow, and the game. Third-party sources are cloned into
`tools/` (not committed): see [docs/loader.md](docs/loader.md) for the loader, and `tools/NaoMahjong` and
`tools/Practice-Riichi-Mahjong` for the art and sounds.

```bash
python -m scripts.prepare_assets                                   # tiles, table and sounds -> build/assets
dotnet build src/MoonMahjong.Plugin -c Release -p:DeployToGame=true # build and copy the plugin into the game
python -m scripts.package_release                                  # the release zip -> dist/
```

For development, `python -m scripts.loader install --dist .run/loader-dist` installs the loader alone
([docs/loader.md](docs/loader.md)); the release installer recognises and can uninstall that too.

Tests: `dotnet test MoonMahjong.sln` (rules engine, table layout, presentation) and `python -m pytest tests`
(asset pipeline, loader, installer and packaging).

| Path | What |
|---|---|
| `src/MoonMahjong.Core` | The Riichi rules engine and match host, ported from Practice-Riichi-Mahjong; the wire format for crew play |
| `src/MoonMahjong.Table` | The table layout (OpenRiichi's) and what the table and screens show |
| `src/MoonMahjong.Plugin` | The BepInEx plugin (`MoonMahjong.dll`) |
| `installer/` | The release installer (PowerShell inside a .bat), its README and the third-party licences |
| `scripts/`, `tests/` | Python tooling and the test suites |
| `patches/` | Our changes to BepInEx and Il2CppInterop |
| `docs/` | How things work and why |

## Credits and licences

Moon Sweeper Mahjong is free software under the [GNU GPL v3](LICENSE), because its rules engine and table layout are
ported from [itsRevela/Practice-Riichi-Mahjong](https://github.com/itsRevela/Practice-Riichi-Mahjong), a fork of
[FluffyStuff/OpenRiichi](https://github.com/FluffyStuff/OpenRiichi) (GPLv3); the table sounds are OpenRiichi's too.

- Tile and table art: [HitomiFlower/NaoMahjong](https://github.com/HitomiFlower/NaoMahjong) (MIT).
- Loader: [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1) and [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop)
  (LGPL-3.0), with the changes in `patches/`, and [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) (MIT).
- Every bundled component and its licence: [installer/THIRD-PARTY-NOTICES.txt](installer/THIRD-PARTY-NOTICES.txt).

This mod is not affiliated with the makers of Moon Sweepers, and no game files are included.
