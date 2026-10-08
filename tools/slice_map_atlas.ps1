Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\ocnelavoc\.gemini\antigravity-ide\brain\bae25978-78bc-4125-8b60-955986487bb6\.user_uploaded\media_1791489441438.png"
$atlas = [System.Drawing.Bitmap]::FromFile($srcPath)
Write-Host "Atlas resolution: $($atlas.Width) x $($atlas.Height)"

# Grid segmentation based on atlas layout:
# Top row: 4 circles / buttons
# Middle row: check, lock, helmet, divider, card top
# Bottom row: play banner, track, fill, card bottom

function Save-SpriteRegion($x1, $y1, $x2, $y2, $name) {
    # Trim white borders within the region
    $minX = $x2; $maxX = $x1; $minY = $y2; $maxY = $y1
    for ($x = $x1; $x -lt $x2; $x++) {
        for ($y = $y1; $y -lt $y2; $y++) {
            $c = $atlas.GetPixel($x, $y)
            if ($c.R -lt 240 -or $c.G -lt 240 -or $c.B -lt 240) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    
    if ($minX -ge $maxX -or $minY -ge $maxY) {
        Write-Host "Empty region for $name"
        return
    }
    
    $pad = 2
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
            
            if ($c.R -gt 250 -and $c.G -gt 250 -and $c.B -gt 250) {
                $cropped.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
            } elseif ($dist -lt 35) {
                $alpha = [int]([Math]::Min(255, [Math]::Max(0, ($dist / 35.0) * 255)))
                $cropped.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, $c.R, $c.G, $c.B))
            }
        }
    }
    
    $outPath = "c:\Projects\2026\game\dungeon-guardians\Assets\Resources\UI\$name.png"
    $cropped.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $cropped.Dispose()
    Write-Host "-> Sliced $name ($w x $h) from [$minX,$minY,$maxX,$maxY]"
}

$W = $atlas.Width
$H = $atlas.Height

# 1. Back button (top-left)
Save-SpriteRegion 0 0 ([int]($W*0.25)) ([int]($H*0.35)) "map_back_btn"

# 2. Node Gold (top 2nd)
Save-SpriteRegion ([int]($W*0.25)) 0 ([int]($W*0.50)) ([int]($H*0.35)) "map_node_gold"

# 3. Node Cyan (top 3rd)
Save-SpriteRegion ([int]($W*0.50)) 0 ([int]($W*0.75)) ([int]($H*0.35)) "map_node_cyan"

# 4. Node Locked Stone (top right)
Save-SpriteRegion ([int]($W*0.75)) 0 $W ([int]($H*0.35)) "map_node_locked"

# 5. Icon Checkmark (middle left)
Save-SpriteRegion 0 ([int]($H*0.35)) ([int]($W*0.25)) ([int]($H*0.70)) "map_icon_check"

# 6. Icon Lock (middle 2nd)
Save-SpriteRegion ([int]($W*0.25)) ([int]($H*0.35)) ([int]($W*0.48)) ([int]($H*0.70)) "map_icon_lock"

# 7. Icon Helmet (middle 3rd)
Save-SpriteRegion ([int]($W*0.48)) ([int]($H*0.35)) ([int]($W*0.72)) ([int]($H*0.70)) "map_icon_helmet"

# 8. Divider (middle right)
Save-SpriteRegion ([int]($W*0.70)) ([int]($H*0.35)) $W ([int]($H*0.60)) "map_divider"

# 9. Large Play Banner (bottom left)
Save-SpriteRegion 0 ([int]($H*0.70)) ([int]($W*0.36)) $H "map_play_btn"

# 10. Progress Track (bottom 2nd)
Save-SpriteRegion ([int]($W*0.35)) ([int]($H*0.70)) ([int]($W*0.58)) $H "map_progress_track"

# 11. Progress Fill (bottom 3rd)
Save-SpriteRegion ([int]($W*0.56)) ([int]($H*0.70)) ([int]($W*0.75)) $H "map_progress_fill"

# 12. Card Frame (bottom right)
Save-SpriteRegion ([int]($W*0.74)) ([int]($H*0.55)) $W $H "map_card_frame"

$atlas.Dispose()
Write-Host "All map UI sprites generated successfully!"
