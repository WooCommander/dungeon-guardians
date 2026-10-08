Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\ocnelavoc\.gemini\antigravity-ide\brain\bae25978-78bc-4125-8b60-955986487bb6\.user_uploaded\media_1791489441438.png"
$rawImg = [System.Drawing.Bitmap]::FromFile($srcPath)
$W = $rawImg.Width
$H = $rawImg.Height
Write-Host "Atlas: $W x $H"

function Extract-CleanSprite($rect, $name) {
    $crop = New-Object System.Drawing.Bitmap($rect.Width, $rect.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($crop)
    $g.DrawImage($rawImg, (New-Object System.Drawing.Rectangle(0, 0, $rect.Width, $rect.Height)), $rect, [System.Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()

    # Find tight bounding box of non-white pixels
    $minX = $crop.Width; $maxX = -1
    $minY = $crop.Height; $maxY = -1

    for ($x = 0; $x -lt $crop.Width; $x++) {
        for ($y = 0; $y -lt $crop.Height; $y++) {
            $c = $crop.GetPixel($x, $y)
            $isWhite = ($c.R -gt 245 -and $c.G -gt 245 -and $c.B -gt 245)
            if (-not $isWhite) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    if ($maxX -lt $minX -or $maxY -lt $minY) {
        Write-Host "No content found in region for $name"
        $crop.Dispose()
        return
    }

    $pad = 1
    $minX = [Math]::Max(0, $minX - $pad)
    $minY = [Math]::Max(0, $minY - $pad)
    $maxX = [Math]::Min($crop.Width - 1, $maxX + $pad)
    $maxY = [Math]::Min($crop.Height - 1, $maxY + $pad)

    $finalW = $maxX - $minX + 1
    $finalH = $maxY - $minY + 1

    $trimmed = New-Object System.Drawing.Bitmap($finalW, $finalH, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    
    for ($x = 0; $x -lt $finalW; $x++) {
        for ($y = 0; $y -lt $finalH; $y++) {
            $c = $crop.GetPixel($minX + $x, $minY + $y)
            $dr = 255 - $c.R
            $dg = 255 - $c.G
            $db = 255 - $c.B
            $dist = [Math]::Sqrt($dr*$dr + $dg*$dg + $db*$db)

            if ($c.R -gt 248 -and $c.G -gt 248 -and $c.B -gt 248) {
                $trimmed.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
            } elseif ($dist -lt 36) {
                $alpha = [int]([Math]::Min(255, [Math]::Max(0, ($dist / 36.0) * 255)))
                $trimmed.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, $c.R, $c.G, $c.B))
            } else {
                $trimmed.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
            }
        }
    }

    $outPath = "c:\Projects\2026\game\dungeon-guardians\Assets\Resources\UI\$name.png"
    $trimmed.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $trimmed.Dispose()
    $crop.Dispose()
    Write-Host "-> $name ($finalW x $finalH)"
}

# Accurate coordinate bounds for each element in the 1024x576 atlas:
# 1. Back button (top left)
Extract-CleanSprite (New-Object System.Drawing.Rectangle(20, 20, 240, 160)) "map_back_btn"

# 2. Gold Node (top 2nd)
Extract-CleanSprite (New-Object System.Drawing.Rectangle(280, 5, 210, 195)) "map_node_gold"

# 3. Cyan Node (top 3rd)
Extract-CleanSprite (New-Object System.Drawing.Rectangle(510, 5, 210, 195)) "map_node_cyan"

# 4. Stone Locked Node (top right)
Extract-CleanSprite (New-Object System.Drawing.Rectangle(740, 5, 220, 195)) "map_node_locked"

# 5. Checkmark icon (middle left)
Extract-CleanSprite (New-Object System.Drawing.Rectangle(60, 215, 160, 145)) "map_icon_check"

# 6. Lock icon (middle 2nd)
Extract-CleanSprite (New-Object System.Drawing.Rectangle(300, 210, 145, 155)) "map_icon_lock"

# 7. Miner Helmet icon (middle 3rd)
Extract-CleanSprite (New-Object System.Drawing.Rectangle(480, 210, 230, 155)) "map_icon_helmet"

# 8. Golden Crystal Divider (middle right - above the card)
Extract-CleanSprite (New-Object System.Drawing.Rectangle(720, 240, 285, 60)) "map_divider"

# 9. Big Golden Play Button (bottom left)
Extract-CleanSprite (New-Object System.Drawing.Rectangle(10, 400, 360, 105)) "map_play_btn"

# 10. Progress bar track (bottom middle-left)
Extract-CleanSprite (New-Object System.Drawing.Rectangle(360, 420, 220, 60)) "map_progress_track"

# 11. Progress bar fill (bottom middle-right)
Extract-CleanSprite (New-Object System.Drawing.Rectangle(570, 425, 200, 50)) "map_progress_fill"

# 12. Stone Card Frame (bottom right)
Extract-CleanSprite (New-Object System.Drawing.Rectangle(760, 310, 225, 245)) "map_card_frame"

$rawImg.Dispose()
Write-Host "Atlas slicing completed perfectly!"
