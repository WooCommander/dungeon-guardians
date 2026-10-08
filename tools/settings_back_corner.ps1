# Clears the painted back arrow from the top-left corner of Backgrounds/settings.png (the game draws it at the
# bottom now, SettingsScreen.cs): the frame is symmetric about x = 835, so the corner is copied mirrored from the
# top-right one; a sliver of the right corner's triangle ornament that would come along is replaced with stone
# from just right of it. Run once after tools/cut_settings.py, with the other settings_*.ps1:
#   powershell -ExecutionPolicy Bypass -File tools/settings_back_corner.ps1
Add-Type -AssemblyName System.Drawing
$path = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\Assets\Resources\Backgrounds\settings.png"))
$src = [System.Drawing.Bitmap]::FromFile($path)
$bmp = New-Object System.Drawing.Bitmap $src
$x0 = 294; $x1 = 404; $y0 = 98; $y1 = 198; $f = 6
function Mirrored($x, $y) {
  if ($x -lt 314 -and $y -ge 140) { return $src.GetPixel(1670 - ($x + 22), $y) }
  return $src.GetPixel(1670 - $x, $y)
}
for ($y = $y0; $y -le $y1; $y++) { for ($x = $x0; $x -le $x1; $x++) {
  $k = [Math]::Min(1.0, [Math]::Min([Math]::Min(($x - $x0 + 1) / $f, ($x1 - $x + 1) / $f), [Math]::Min(($y - $y0 + 1) / $f, ($y1 - $y + 1) / $f)))
  $p = Mirrored $x $y; $q = $src.GetPixel($x, $y)
  $bmp.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, [int]($q.R + ($p.R - $q.R) * $k), [int]($q.G + ($p.G - $q.G) * $k), [int]($q.B + ($p.B - $q.B) * $k)))
} }
$src.Dispose()
$bmp.Save($path)
$bmp.Dispose()
Write-Host "back arrow cleared in $path"
