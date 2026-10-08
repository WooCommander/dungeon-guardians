Add-Type -AssemblyName System.Drawing

$dir = "c:\Projects\2026\game\dungeon-guardians\Assets\Resources\UI"
$files = Get-ChildItem -Path $dir -Filter "map_*.png"

foreach ($f in $files) {
    $bmp = [System.Drawing.Bitmap]::FromFile($f.FullName)
    $minX = $bmp.Width; $maxX = 0; $minY = $bmp.Height; $maxY = 0
    
    for ($x = 0; $x -lt $bmp.Width; $x++) {
        for ($y = 0; $y -lt $bmp.Height; $y++) {
            $p = $bmp.GetPixel($x, $y)
            if ($p.A -gt 15) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    
    if ($minX -le $maxX -and $minY -le $maxY) {
        $w = $maxX - $minX + 1
        $h = $maxY - $minY + 1
        $rect = New-Object System.Drawing.Rectangle($minX, $minY, $w, $h)
        $trimmed = $bmp.Clone($rect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $bmp.Dispose()
        $trimmed.Save($f.FullName, [System.Drawing.Imaging.ImageFormat]::Png)
        $trimmed.Dispose()
        Write-Host "Trimmed $($f.Name) -> $w x $h"
    } else {
        $bmp.Dispose()
    }
}
