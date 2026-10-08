# Turns the painted "Язык" row of Backgrounds/settings.png into the camera-scale row.
# Run after tools/cut_settings.py (which rewrites the background), once:
#   powershell -ExecutionPolicy Bypass -File tools/settings_camera_row.ps1
Add-Type -AssemblyName System.Drawing

$path = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\Assets\Resources\Backgrounds\settings.png"))
$loaded = [System.Drawing.Bitmap]::FromFile($path)
$bmp = New-Object System.Drawing.Bitmap $loaded
$loaded.Dispose()

# Copies a strip of the same rows from sx to dx, fading in over `feather` pixels at its left and right ends so no
# seam shows.
function CopyPatch($sx, $dx, $y, $w, $h, $feather) {
    $src = $bmp.Clone((New-Object System.Drawing.Rectangle($sx, $y, $w, $h)), $bmp.PixelFormat)
    for ($i = 0; $i -lt $w; $i++) {
        $a = [Math]::Min(1.0, [Math]::Min(($i + 1) / $feather, ($w - $i) / $feather))
        for ($j = 0; $j -lt $h; $j++) {
            $b = [Math]::Min(1.0, [Math]::Min(($j + 1) / 4.0, ($h - $j) / 4.0))
            $k = $a * $b
            $p = $src.GetPixel($i, $j); $q = $bmp.GetPixel($dx + $i, $y + $j)
            $bmp.SetPixel($dx + $i, $y + $j, [System.Drawing.Color]::FromArgb($q.A,
                [int]($q.R + ($p.R - $q.R) * $k), [int]($q.G + ($p.G - $q.G) * $k), [int]($q.B + ($p.B - $q.B) * $k)))
        }
    }
    $src.Dispose()
}

# The globe icon and the "Язык" label go, covered with plain stone from further right in the same row.
CopyPatch 590 336 669 230 74 14
# "Русский" goes from the dropdown frame, covered with the empty middle of the same frame. The chevron stays.
CopyPatch 1120 978 684 160 44 18

# A golden magnifier where the globe was.
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$cx = 381; $cy = 699; $r = 15
$dark = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(230, 35, 18, 6), 11)
$dark.StartCap = 'Round'; $dark.EndCap = 'Round'
$gold = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 238, 176, 86), 6)
$gold.StartCap = 'Round'; $gold.EndCap = 'Round'
$shine = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200, 255, 230, 170), 2)
$g.DrawEllipse($dark, $cx - $r, $cy - $r, 2 * $r, 2 * $r)
$g.DrawLine($dark, $cx + 12, $cy + 12, $cx + 26, $cy + 26)
$g.DrawEllipse($gold, $cx - $r, $cy - $r, 2 * $r, 2 * $r)
$g.DrawLine($gold, $cx + 12, $cy + 12, $cx + 26, $cy + 26)
$g.DrawArc($shine, $cx - $r + 1, $cy - $r + 1, 2 * $r - 2, 2 * $r - 2, 200, 70)
$g.Dispose()

$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "camera row written to $path"
