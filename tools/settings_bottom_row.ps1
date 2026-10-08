# Clears the painted "Начать заново" and "ГОТОВО" buttons from the bottom of Backgrounds/settings.png, so the
# game can lay a single centred "ГОТОВО" over it (SettingsScreen.cs). The stone is rebuilt from the clean gap
# between the two painted buttons: overlapping, randomly shifted strips, each column evened out to the gap's mean
# brightness so no stripes show. Run once after tools/cut_settings.py, with tools/settings_camera_row.ps1:
#   powershell -ExecutionPolicy Bypass -File tools/settings_bottom_row.ps1
Add-Type -AssemblyName System.Drawing

$path = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\Assets\Resources\Backgrounds\settings.png"))
$loaded = [System.Drawing.Bitmap]::FromFile($path)
$bmp = New-Object System.Drawing.Bitmap $loaded
$loaded.Dispose()
$src = $bmp.Clone((New-Object System.Drawing.Rectangle(0, 0, $bmp.Width, $bmp.Height)), $bmp.PixelFormat)

# Panel between the camera row and the bottom frame, inside the corner ornaments.
$x0 = 356; $x1 = 1314; $y0 = 750
# The bottom frame steps down between the corners: the panel reaches y 886 in the middle, 868 at the ends.
function Bottom($x) {
    if ($x -lt 378 -or $x -gt 1292) { return 868 }
    if ($x -lt 397) { return 868 + ($x - 378) * 18 / 19 }
    if ($x -gt 1273) { return 886 - ($x - 1273) * 18 / 19 }
    return 886
}
$h = 136
# Clean stone between the painted buttons.
$sx0 = 796; $sx1 = 828
$tile = 22; $step = 12
$rand = New-Object System.Random 7

# Mean of every gap column, and of the whole gap, per channel.
$colR = @{}; $colG = @{}; $colB = @{}; $mR = 0.0; $mG = 0.0; $mB = 0.0
for ($c = $sx0; $c -lt $sx1; $c++) {
    $r = 0.0; $g = 0.0; $b = 0.0
    for ($y = $y0 + 8; $y -lt 878; $y++) { $p = $src.GetPixel($c, $y); $r += $p.R; $g += $p.G; $b += $p.B }
    $n = 878 - $y0 - 8
    $colR[$c] = $r / $n; $colG[$c] = $g / $n; $colB[$c] = $b / $n
    $mR += $colR[$c]; $mG += $colG[$c]; $mB += $colB[$c]
}
$mR /= ($sx1 - $sx0); $mG /= ($sx1 - $sx0); $mB /= ($sx1 - $sx0)

$w = $x1 - $x0
$sumR = New-Object 'double[,]' $w, $h; $sumG = New-Object 'double[,]' $w, $h
$sumB = New-Object 'double[,]' $w, $h; $sumW = New-Object 'double[,]' $w, $h
for ($t = -$tile; $t -lt $w; $t += $step) {
    $sx = $rand.Next($sx0, $sx1 - $tile)
    $dy = $rand.Next(-6, 7)
    $mirror = $rand.Next(2) -eq 1
    for ($i = 0; $i -lt $tile; $i++) {
        $x = $t + $i
        if ($x -lt 0 -or $x -ge $w) { continue }
        $c = $sx + $(if ($mirror) { $tile - 1 - $i } else { $i })
        $k = [Math]::Max(0.05, [Math]::Min(1.0, [Math]::Min(($i + 1) / 6.0, ($tile - $i) / 6.0)))
        $bottom = Bottom ($x0 + $x)
        for ($j = 0; $j -lt $h; $j++) {
            # The strip keeps the panel's top-to-bottom shading, pinned to this column's own bottom edge.
            $sy = [int]([Math]::Min(885, [Math]::Max($y0, $y0 + $j + $dy + (886 - $bottom))))
            $p = $src.GetPixel($c, $sy)
            $sumR[$x, $j] += ($p.R - $colR[$c] + $mR) * $k
            $sumG[$x, $j] += ($p.G - $colG[$c] + $mG) * $k
            $sumB[$x, $j] += ($p.B - $colB[$c] + $mB) * $k
            $sumW[$x, $j] += $k
        }
    }
}
$feather = 14
for ($x = 0; $x -lt $w; $x++) {
    $a = [Math]::Min(1.0, [Math]::Min(($x + 1) / $feather, ($w - $x) / $feather))
    $bottom = Bottom ($x0 + $x)
    for ($j = 0; $j -lt $h; $j++) {
        $y = $y0 + $j
        if ($y -ge $bottom) { break }
        $b = [Math]::Min(1.0, [Math]::Min(($j + 1) / 3.0, ($bottom - $y) / 3.0))
        $k = $a * $b
        $q = $bmp.GetPixel($x0 + $x, $y)
        $n = $sumW[$x, $j]
        $vr = $sumR[$x, $j] / $n; $vg = $sumG[$x, $j] / $n; $vb = $sumB[$x, $j] / $n
        $r = [Math]::Max(0, [Math]::Min(255, $vr))
        $g = [Math]::Max(0, [Math]::Min(255, $vg))
        $bl = [Math]::Max(0, [Math]::Min(255, $vb))
        $bmp.SetPixel($x0 + $x, $y, [System.Drawing.Color]::FromArgb($q.A,
            [int]($q.R + ($r - $q.R) * $k), [int]($q.G + ($g - $q.G) * $k), [int]($q.B + ($bl - $q.B) * $k)))
    }
}
$src.Dispose()
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "bottom row cleared in $path"
