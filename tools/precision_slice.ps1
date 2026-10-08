Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\ocnelavoc\.gemini\antigravity-ide\brain\bae25978-78bc-4125-8b60-955986487bb6\.user_uploaded\media_1791489441438.png"
$atlas = [System.Drawing.Bitmap]::FromFile($srcPath)
Write-Host "Atlas resolution: $($atlas.Width) x $($atlas.Height)"

# Let's inspect where the divider and card frame actually reside
# Divider is horizontally located around Y=0.45-0.55, X=0.70-0.99
# Card frame is located at bottom right: X=0.75-0.98, Y=0.56-0.98

function Crop-And-Clean($x1, $y1, $x2, $y2, $name) {
    $minX = $x2; $maxX = $x1; $minY = $y2; $maxY = $y1
    for ($x = $x1; $x -le $x2; $x++) {
        for ($y = $y1; $y -le $y2; $y++) {
            $c = $atlas.GetPixel($x, $y)
            if ($c.R -lt 242 -or $c.G -lt 242 -or $c.B -lt 242) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    
    if ($minX -gt $maxX -or $minY -gt $maxY) {
        Write-Host "Empty region for $name"
        return
    }
    
    $pad = 1
    $minX = [Math]::Max(0, $minX - $pad)
    $minY = [Math]::Max(0, $minY - $pad)
    $maxX = [Math]::Min($atlas.Width - 1, $maxX + $pad)
    $maxY = [Math]::Min($atlas.Height - 1, $maxY + $pad)
    
    $w = $maxX - $minX + 1
    $h = $maxY - $minY + 1
    
    $cropRect = New-Object System.Drawing.Rectangle($minX, $minY, $w, $h)
    $cropped = $atlas.Clone($cropRect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    
    # Transparency pass: smooth alpha transition around borders
    for ($x = 0; $x -lt $cropped.Width; $x++) {
        for ($y = 0; $y -lt $cropped.Height; $y++) {
            $c = $cropped.GetPixel($x, $y)
            $dr = 255 - $c.R
            $dg = 255 - $c.G
            $db = 255 - $c.B
            $dist = [Math]::Sqrt($dr*$dr + $dg*$dg + $db*$db)
            
            if ($c.R -gt 248 -and $c.G -gt 248 -and $c.B -gt 248) {
                $cropped.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
            } elseif ($dist -lt 40) {
                $alpha = [int]([Math]::Min(255, [Math]::Max(0, ($dist / 40.0) * 255)))
                $cropped.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, $c.R, $c.G, $c.B))
            }
        }
    }
    
    $outPath = "c:\Projects\2026\game\dungeon-guardians\Assets\Resources\UI\$name.png"
    $cropped.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $cropped.Dispose()
    Write-Host "-> Sliced $name ($w x $h) from [$minX,$minY] to [$maxX,$maxY]"
}

$W = $atlas.Width
$H = $atlas.Height

# 1. Back button (top-left)
Crop-And-Clean 0 0 ([int]($W*0.26)) ([int]($H*0.34)) "map_back_btn"

# 2. Node Gold (top 2nd)
Crop-And-Clean ([int]($W*0.26)) 0 ([int]($W*0.50)) ([int]($H*0.34)) "map_node_gold"

# 3. Node Cyan (top 3rd)
Crop-And-Clean ([int]($W*0.50)) 0 ([int]($W*0.74)) ([int]($H*0.34)) "map_node_cyan"

# 4. Node Locked Stone (top right)
Crop-And-Clean ([int]($W*0.74)) 0 $W ([int]($H*0.34)) "map_node_locked"

# 5. Icon Checkmark (middle left)
Crop-And-Clean 0 ([int]($H*0.34)) ([int]($W*0.25)) ([int]($H*0.68)) "map_icon_check"

# 6. Icon Lock (middle 2nd)
Crop-And-Clean ([int]($W*0.25)) ([int]($H*0.34)) ([int]($W*0.46)) ([int]($H*0.68)) "map_icon_lock"

# 7. Icon Helmet (middle 3rd)
Crop-And-Clean ([int]($W*0.46)) ([int]($H*0.34)) ([int]($W*0.72)) ([int]($H*0.68)) "map_icon_helmet"

# 8. Divider (middle right - thin horizontal bar)
Crop-And-Clean ([int]($W*0.70)) ([int]($H*0.40)) $W ([int]($H*0.56)) "map_divider"

# 9. Large Play Banner (bottom left)
Crop-And-Clean 0 ([int]($H*0.68)) ([int]($W*0.36)) $H "map_play_btn"

# 10. Progress Track (bottom 2nd)
Crop-And-Clean ([int]($W*0.35)) ([int]($H*0.72)) ([int]($W*0.57)) $H "map_progress_track"

# 11. Progress Fill (bottom 3rd)
Crop-And-Clean ([int]($W*0.56)) ([int]($H*0.72)) ([int]($W*0.75)) $H "map_progress_fill"

# 12. Card Frame (bottom right)
Crop-And-Clean ([int]($W*0.75)) ([int]($H*0.56)) $W $H "map_card_frame"

$atlas.Dispose()
Write-Host "Done precision slicing!"
