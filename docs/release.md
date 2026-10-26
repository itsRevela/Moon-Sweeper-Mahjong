# Releasing

A release is one zip: `MoonSweeperMahjong-v<version>.zip`, holding the installer .bat, the updater .bat, the payload
they copy into the game folder, the licence, the third-party notices and a README for players. The updater finds
releases through GitHub's "latest release" API, so a published release must carry that zip as an asset (GitHub's
SHA-256 for it is what the updater checks).

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
   with the CHANGELOG section as the notes. Then run the previous release's updater on a real game: it should find
   the new release, download it and reinstall with the settings kept.

## The installer

`installer/MoonSweeperMahjong.ps1` is the whole installer; `scripts/package_release.py` puts `installer/header.bat` in
front of it and stamps the version, giving the .bat. The header is a batch/PowerShell polyglot: `cmd` runs its batch
lines (which read the file and hand it to PowerShell), and PowerShell skips them as a `<# ... #>` comment. The file
must stay plain ASCII with CRLF line endings, which packaging enforces.

It finds the game through `HKCU\Software\Valve\Steam\SteamPath` (and the 32/64-bit `InstallPath`s), every library in
`steamapps\libraryfolders.vdf`, and `appmanifest_5335510.acf`'s `installdir`. Switches (`-Action`, `-GamePath`,
`-SteamPath`, `-Payload`, `-Yes`, `-Settings`, `-GameProcess`, `-ReleaseApi`) exist for automation and for
`tests/test_installer.py`, which runs it against fake Steam libraries and game folders.

The updater .bat is the same file with `-Action Update` put before its arguments. It reads the installed version
from the manifest and asks `https://api.github.com/repos/itsRevela/Moon-Sweeper-Mahjong/releases/latest` for the
latest one; when that is newer, it downloads the release zip, refuses it unless it matches the asset's `digest`
(`sha256:...`), and runs that release's own installer (the .bat, run as a PowerShell script) to uninstall with the
settings kept and install the new payload. `tests/test_updater.py` serves a fake release over local HTTP.
