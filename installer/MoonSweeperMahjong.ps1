# Moon Sweeper Mahjong installer: finds Moon Sweepers through Steam, then installs or uninstalls the mod (the patched
# BepInEx loader and the mahjong plugin) from the payload folder beside it. Packaging (scripts/package_release.py)
# puts a batch header in front of this script, so players just double-click one .bat file.
#
# Every file it adds is recorded, with its SHA-256, in a manifest in the game folder (the same file and format as the
# developer installer, scripts/loader.py), and uninstall removes exactly that plus what BepInEx made while running.
# A BepInEx it did not install is moved to a backup folder (only with consent) and put back on uninstall.
#
# Run with no arguments for the menu. Switches (for automation and tests):
#   -Action Menu|Install|Uninstall|Status   -GamePath <dir>   -SteamPath <dir>   -Payload <dir>
#   -Yes (answer yes to every question)     -Settings Ask|Keep|Remove (uninstall: keep the mod's settings?)
#   -GameProcess <name> (the process that must not be running; default MoonSweepers)
# Exit codes: 0 done, 1 refused or failed, 2 game not found.
param(
    [ValidateSet('Menu', 'Install', 'Uninstall', 'Status')] [string]$Action = 'Menu',
    [string]$GamePath,
    [string]$SteamPath,
    [string]$Payload,
    [switch]$Yes,
    [ValidateSet('Ask', 'Keep', 'Remove')] [string]$Settings = 'Ask',
    [string]$GameProcess = 'MoonSweepers'   # the game's process name (tests use another, so a running game can't interfere)
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$ModVersion = '0.0.0-dev'   # replaced when packaging
$AppId = '5335510'
$GameExe = 'MoonSweepers.exe'
$ManifestName = '.moonsweepers-mahjong-loader.json'
$SettingsFile = 'BepInEx/config/itsrevela.moonsweepers.mahjong.cfg'
$BackupPrefix = 'MoonSweeperMahjong-backup-'
# What a BepInEx install puts in the game's root folder; a foreign copy of any of these is backed up together.
$LoaderRoots = @('BepInEx', 'dotnet', 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version')
# What BepInEx creates while it runs (never in the manifest); uninstall removes these too.
$RuntimePaths = @('BepInEx/interop', 'BepInEx/unity-libs', 'BepInEx/cache', 'BepInEx/patchers', 'BepInEx/plugins/MoonMahjong',
    'BepInEx/config/BepInEx.cfg', 'BepInEx/LogOutput.log', 'BepInEx/ErrorLog.log')

class InstallerError : System.Exception {
    [int]$Code
    InstallerError([string]$message, [int]$code) : base($message) { $this.Code = $code }
}

function Fail([string]$message, [int]$code = 1) { throw [InstallerError]::new($message, $code) }

function Ask([string]$question) {
    if ($Yes) { return $true }
    if (-not [Environment]::UserInteractive -or [Console]::IsInputRedirected) { return $false }
    $answer = Read-Host "$question [y/N]"
    return $answer -match '^\s*y'
}

function Join-Game([string]$game, [string]$relative) { Join-Path $game ($relative -replace '/', '\') }

# --- Finding the game -------------------------------------------------------------------------------------------

function Get-SteamRoots {
    if ($SteamPath) { return @($SteamPath) }
    $roots = @()
    foreach ($key in @('HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam', 'HKLM:\SOFTWARE\Valve\Steam')) {
        $item = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
        if ($item) {
            foreach ($name in @('SteamPath', 'InstallPath')) {
                if ($item.PSObject.Properties[$name] -and $item.$name) { $roots += ($item.$name -replace '/', '\') }
            }
        }
    }
    return $roots | Select-Object -Unique
}

# Steam's library list (steamapps\libraryfolders.vdf): every "path" value, unescaped.
function Get-SteamLibraries([string]$root) {
    $libraries = @($root)
    $vdf = Join-Path $root 'steamapps\libraryfolders.vdf'
    if (Test-Path -LiteralPath $vdf) {
        foreach ($match in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) {
            $libraries += ($match.Groups[1].Value -replace '\\\\', '\')
        }
    }
    return $libraries | Select-Object -Unique
}

function Find-Game {
    foreach ($root in Get-SteamRoots) {
        foreach ($library in Get-SteamLibraries $root) {
            $manifest = Join-Path $library "steamapps\appmanifest_$AppId.acf"
            $folder = 'Moon Sweepers'
            if (Test-Path -LiteralPath $manifest) {
                $match = [regex]::Match((Get-Content -LiteralPath $manifest -Raw), '"installdir"\s+"([^"]+)"')
                if ($match.Success) { $folder = $match.Groups[1].Value }
            }
            $game = Join-Path $library "steamapps\common\$folder"
            if (Test-Path -LiteralPath (Join-Path $game $GameExe)) { return (Resolve-Path -LiteralPath $game).Path }
        }
    }
    return $null
}

function Resolve-Game {
    if ($GamePath) {
        if (-not (Test-Path -LiteralPath (Join-Path $GamePath $GameExe))) { Fail "There is no $GameExe in $GamePath." 2 }
        return (Resolve-Path -LiteralPath $GamePath).Path
    }
    $found = Find-Game
    if ($found) { return $found }
    if ($Action -eq 'Menu' -and [Environment]::UserInteractive -and -not [Console]::IsInputRedirected) {
        $typed = Read-Host "Could not find Moon Sweepers through Steam. Paste the game's folder (the one with $GameExe)"
        $typed = $typed.Trim('" ')
        if ($typed -and (Test-Path -LiteralPath (Join-Path $typed $GameExe))) { return (Resolve-Path -LiteralPath $typed).Path }
    }
    Fail 'Could not find Moon Sweepers. Is it installed through Steam? You can also pass -GamePath "<folder>".' 2
}

# --- Install state ----------------------------------------------------------------------------------------------

function Read-Manifest([string]$game) {
    $path = Join-Path $game $ManifestName
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Get-Property($object, [string]$name) {
    if ($object -and $object.PSObject.Properties[$name]) { return $object.$name }
    return $null
}

function Show-Status([string]$game) {
    Write-Host "Moon Sweepers: $game"
    $manifest = Read-Manifest $game
    if (-not $manifest) {
        Write-Host 'Moon Sweeper Mahjong: not installed'
        return
    }
    $changed = @($manifest.files | Where-Object {
        $file = Join-Game $game $_.path
        -not (Test-Path -LiteralPath $file) -or ($_.sha256 -and (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $_.sha256.ToUpper())
    })
    $version = Get-Property $manifest 'version'
    $state = if ($changed.Count -eq 0) { 'all files intact' } else { "$($changed.Count) file(s) changed or missing" }
    Write-Host ("Moon Sweeper Mahjong: installed" + $(if ($version) { " ($version)" } else { '' }) + ", $state")
}

function Assert-GameClosed {
    if (Get-Process -Name $GameProcess -ErrorAction SilentlyContinue) { Fail 'Moon Sweepers is running. Close the game first.' }
}

function Assert-Writable([string]$game) {
    $probe = Join-Path $game ('.msm-write-test-' + [guid]::NewGuid().ToString('N'))
    try { [IO.File]::WriteAllText($probe, ''); Remove-Item -LiteralPath $probe -Force }
    catch { Fail "Windows won't let this installer change $game. Right-click the .bat and choose 'Run as administrator'." }
}

# --- Install ----------------------------------------------------------------------------------------------------

# A BepInEx folder holding nothing but our settings (kept by an earlier uninstall) is ours, not another mod loader.
function Test-OnlyOurSettings([string]$game) {
    $files = @(Get-ChildItem -LiteralPath (Join-Game $game 'BepInEx') -Recurse -File -Force)
    return $files.Count -eq 0 -or ($files.Count -eq 1 -and $files[0].FullName -eq (Join-Game $game $SettingsFile))
}

function Get-ForeignLoader([string]$game) {
    return @($LoaderRoots | Where-Object {
            (Test-Path -LiteralPath (Join-Game $game $_)) -and -not ($_ -eq 'BepInEx' -and (Test-OnlyOurSettings $game))
        })
}

function Install-Mod([string]$game) {
    if (Read-Manifest $game) { Fail 'Moon Sweeper Mahjong is already installed. Uninstall it first (or choose Reinstall).' }
    # Inside the release .bat the script runs as text, without a path; the batch header passes its folder instead.
    $here = if ($env:MSM_HOME) { $env:MSM_HOME } else { $PSScriptRoot }
    $source = if ($Payload) { $Payload } else { Join-Path $here 'payload' }
    if (-not (Test-Path -LiteralPath $source -PathType Container)) { Fail "The mod's files are missing: there is no folder $source. Unzip the whole release first." }
    $source = (Resolve-Path -LiteralPath $source).Path
    Assert-GameClosed
    Assert-Writable $game

    $backup = $null
    $foreign = @(Get-ForeignLoader $game)   # @(): a function returns an empty array as $null
    if ($foreign.Count -gt 0) {
        Write-Host "The game already has a mod loader that this installer didn't install: $($foreign -join ', ')."
        if (-not (Ask 'Move it to a backup folder (it is put back when you uninstall Moon Sweeper Mahjong)?')) {
            Fail 'Installation cancelled: the existing mod loader was left as it was.'
        }
        $backup = $BackupPrefix + (Get-Date -Format 'yyyyMMdd-HHmmss')
        New-Item -ItemType Directory -Path (Join-Path $game $backup) | Out-Null
        foreach ($item in $foreign) { Move-Item -LiteralPath (Join-Game $game $item) -Destination (Join-Path $game $backup) }
        Write-Host "Backed up to $backup."
    }

    $files = @(Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object { $_.FullName.Substring($source.Length + 1) -replace '\\', '/' } | Sort-Object)
    $clashes = @($files | Where-Object { Test-Path -LiteralPath (Join-Game $game $_) })
    if ($clashes.Count -gt 0) { Fail "Refusing to overwrite $($clashes.Count) existing file(s), e.g. $($clashes[0])." }

    $dirs = New-Object System.Collections.Generic.SortedSet[string]
    foreach ($rel in $files) {
        $parent = Split-Path $rel -Parent
        while ($parent) {
            if (-not (Test-Path -LiteralPath (Join-Game $game $parent))) { [void]$dirs.Add(($parent -replace '\\', '/')) }
            $parent = Split-Path $parent -Parent
        }
    }

    $copied = New-Object System.Collections.Generic.List[object]
    try {
        foreach ($rel in $files) {
            $target = Join-Game $game $rel
            New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
            Copy-Item -LiteralPath (Join-Game $source $rel) -Destination $target
            Unblock-File -LiteralPath $target   # files from a downloaded zip carry Windows' internet mark
            $copied.Add([ordered]@{ path = $rel; sha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash })
        }
    }
    catch {
        foreach ($entry in $copied) { Remove-Item -LiteralPath (Join-Game $game $entry.path) -Force -ErrorAction SilentlyContinue }
        Remove-EmptyDirs $game @($dirs)
        if ($backup) { Restore-Backup $game $backup }
        Fail "Copying failed and was undone: $($_.Exception.Message)"
    }

    $manifest = [ordered]@{ version = $ModVersion; installer = 'release'; files = $copied; dirs = @($dirs); backup = $backup }
    # UTF-8 without a byte-order mark (Windows PowerShell's Set-Content would add one), as scripts/loader.py writes it.
    [IO.File]::WriteAllText((Join-Path $game $ManifestName), ($manifest | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding $false))
    Write-Host "Installed Moon Sweeper Mahjong $ModVersion ($($files.Count) files). Start Moon Sweepers and sit at the lounge's table to play."
}

# --- Uninstall --------------------------------------------------------------------------------------------------

function Remove-EmptyDirs([string]$game, [string[]]$dirs) {
    foreach ($rel in ($dirs | Sort-Object { ($_ -split '/').Count } -Descending)) {
        $dir = Join-Game $game $rel
        if ((Test-Path -LiteralPath $dir -PathType Container) -and -not (Get-ChildItem -LiteralPath $dir -Force)) {
            Remove-Item -LiteralPath $dir -Force
        }
    }
}

function Restore-Backup([string]$game, [string]$backup) {
    $folder = Join-Path $game $backup
    if (-not (Test-Path -LiteralPath $folder)) { Write-Host "The backup $backup is gone; nothing to restore."; return }
    foreach ($item in Get-ChildItem -LiteralPath $folder -Force) {
        $target = Join-Path $game $item.Name
        if (Test-Path -LiteralPath $target) { Write-Host "Kept the backup of $($item.Name) in ${backup}: ${target} exists."; continue }
        Move-Item -LiteralPath $item.FullName -Destination $target
    }
    if (-not (Get-ChildItem -LiteralPath $folder -Force)) { Remove-Item -LiteralPath $folder -Force }
    Write-Host 'Put the previous mod loader back.'
}

function Uninstall-Mod([string]$game) {
    $manifest = Read-Manifest $game
    if (-not $manifest) { Fail 'Moon Sweeper Mahjong is not installed in this game folder.' }
    Assert-GameClosed
    Assert-Writable $game

    $keep = switch ($Settings) {
        'Keep' { $true }
        'Remove' { $false }
        default { Ask 'Keep your mahjong settings for a later reinstall?' }
    }
    $settingsPath = Join-Game $game $SettingsFile
    $saved = $null
    if ($keep -and (Test-Path -LiteralPath $settingsPath)) { $saved = [IO.File]::ReadAllBytes($settingsPath) }

    foreach ($rel in $RuntimePaths) {
        $path = Join-Game $game $rel
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    }
    foreach ($file in $manifest.files) {
        $path = Join-Game $game $file.path
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    if (-not $keep -and (Test-Path -LiteralPath $settingsPath)) { Remove-Item -LiteralPath $settingsPath -Force }
    # BepInEx itself may predate the install (settings kept from an earlier one); it goes too once it is empty.
    $dirs = @($manifest.dirs) + @('BepInEx/config', 'BepInEx/plugins', 'BepInEx')
    Remove-EmptyDirs $game ($dirs | Where-Object { $_ })
    Remove-Item -LiteralPath (Join-Path $game $ManifestName) -Force

    if ($saved -ne $null) {
        New-Item -ItemType Directory -Force -Path (Split-Path $settingsPath -Parent) | Out-Null
        if (-not (Test-Path -LiteralPath $settingsPath)) { [IO.File]::WriteAllBytes($settingsPath, $saved) }
        Write-Host "Kept your settings in $SettingsFile."
    }
    $backup = Get-Property $manifest 'backup'
    if ($backup) { Restore-Backup $game $backup }
    Write-Host 'Uninstalled Moon Sweeper Mahjong.'
}

# --- Menu -------------------------------------------------------------------------------------------------------

function Show-Menu([string]$game) {
    Write-Host "Moon Sweeper Mahjong $ModVersion"
    Show-Status $game
    Write-Host ''
    if (Read-Manifest $game) {
        $choice = Read-Host '[U]ninstall, [R]einstall, or [Q]uit'
        switch -Regex ($choice) {
            '^\s*u' { Uninstall-Mod $game }
            '^\s*r' { $script:Settings = 'Keep'; Uninstall-Mod $game; Install-Mod $game }
            default { Write-Host 'Nothing changed.' }
        }
    }
    else {
        if (Ask 'Install Moon Sweeper Mahjong?') { Install-Mod $game } else { Write-Host 'Nothing changed.' }
    }
}

try {
    $game = Resolve-Game
    switch ($Action) {
        'Status' { Show-Status $game }
        'Install' { Install-Mod $game }
        'Uninstall' { Uninstall-Mod $game }
        default { Show-Menu $game }
    }
    exit 0
}
catch [InstallerError] {
    Write-Host $_.Exception.Message
    exit $_.Exception.Code
}
catch {
    Write-Host "Something went wrong: $($_.Exception.Message)"
    exit 1
}
