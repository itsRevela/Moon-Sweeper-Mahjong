<#
.SYNOPSIS
  Brings the Moon Sweepers window to the front and saves what it shows as a PNG (testing aid).
.EXAMPLE
  powershell -NoProfile -File scripts/screenshot.ps1 -Out .run/shots/table.png
#>
param(
    [Parameter(Mandatory = $true)][string]$Out
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class MoonWindow {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$process = Get-Process -Name MoonSweepers -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $process -or $process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Moon Sweepers is not running (no main window).' }
[void][MoonWindow]::SetForegroundWindow($process.MainWindowHandle)
Start-Sleep -Milliseconds 700
$rect = New-Object MoonWindow+RECT
[void][MoonWindow]::GetWindowRect($process.MainWindowHandle, [ref]$rect)
$width = $rect.Right - $rect.Left; $height = $rect.Bottom - $rect.Top
$bitmap = New-Object System.Drawing.Bitmap $width, $height
try {
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
    $directory = Split-Path -Parent ([System.IO.Path]::GetFullPath($Out))
    New-Item -ItemType Directory -Force $directory | Out-Null
    $bitmap.Save([System.IO.Path]::GetFullPath($Out), [System.Drawing.Imaging.ImageFormat]::Png)
    "SCREENSHOT OK: ${width}x${height} -> $Out"
} finally {
    $bitmap.Dispose()
}
