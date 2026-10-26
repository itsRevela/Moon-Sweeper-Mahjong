# Releasing

A release is one zip: `MoonSweeperMahjong-v<version>.zip`, holding the installer .bat, the payload it copies into the
game folder, the licence, the third-party notices and a README for players.

## Steps

1. Bump `Plugin.Version` in `src/MoonMahjong.Plugin/Plugin.cs` (the zip, the installer and the in-game log use it) and
   move the CHANGELOG's Unreleased entries under the new version.
2. Run the tests: `dotnet test MoonMahjong.sln` and `python -m pytest tests`.
3. Build the inputs:
   - the loader, staged into `.run/loader-dist` (`docs/loader.md`; only needed when the loader changes),
   - the assets: `python -m scripts.prepare_assets`,
   - the plugin: `dotnet build src/MoonMahjong.Plugin -c Release`.
4. Package: `python -m scripts.package_release` writes `dist/MoonSweeperMahjong-v<version>.zip`.
5. Check it on a real game: unzip, run the .bat, install, play a hand, run the .bat again, uninstall.
6. Publish: `gh release create v<version> dist/MoonSweeperMahjong-v<version>.zip --repo itsRevela/Moon-Sweeper-Mahjong`
   with the CHANGELOG section as the notes.

## The installer

`installer/MoonSweeperMahjong.ps1` is the whole installer; `scripts/package_release.py` puts `installer/header.bat` in
front of it and stamps the version, giving the .bat. The header is a batch/PowerShell polyglot: `cmd` runs its batch
lines (which read the file and hand it to PowerShell), and PowerShell skips them as a `<# ... #>` comment. The file
must stay plain ASCII with CRLF line endings, which packaging enforces.

It finds the game through `HKCU\Software\Valve\Steam\SteamPath` (and the 32/64-bit `InstallPath`s), every library in
`steamapps\libraryfolders.vdf`, and `appmanifest_5335510.acf`'s `installdir`. Switches (`-Action`, `-GamePath`,
`-SteamPath`, `-Payload`, `-Yes`, `-Settings`, `-GameProcess`) exist for automation and for `tests/test_installer.py`,
which runs it against fake Steam libraries and game folders.
