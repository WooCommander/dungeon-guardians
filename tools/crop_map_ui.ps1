Add-Type -AssemblyName System.Drawing
$refImg = [System.Drawing.Bitmap]::FromFile("C:\Users\ocnelavoc\.gemini\antigravity-ide\brain\bae25978-78bc-4125-8b60-955986487bb6\.user_uploaded\media_1791488244884.jpg")

Write-Host "Image size: $($refImg.Width) x $($refImg.Height)"

# Let's crop the right card illustration, the back button frame, and the bottom play button frame
$cardRect = New-Object System.Drawing.Rectangle(750, 125, 235, 430)
$cardBmp = $refImg.Clone($cardRect, $refImg.PixelFormat)
$cardBmp.Save("c:\Projects\2026\game\dungeon-guardians\Assets\Resources\UI\map_card_frame.png", [System.Drawing.Imaging.ImageFormat]::Png)
$cardBmp.Dispose()

$btnRect = New-Object System.Drawing.Rectangle(300, 492, 424, 60)
$btnBmp = $refImg.Clone($btnRect, $refImg.PixelFormat)
$btnBmp.Save("c:\Projects\2026\game\dungeon-guardians\Assets\Resources\UI\map_play_btn.png", [System.Drawing.Imaging.ImageFormat]::Png)
$btnBmp.Dispose()

$backRect = New-Object System.Drawing.Rectangle(22, 22, 70, 70)
$backBmp = $refImg.Clone($backRect, $refImg.PixelFormat)
$backBmp.Save("c:\Projects\2026\game\dungeon-guardians\Assets\Resources\UI\map_back_btn.png", [System.Drawing.Imaging.ImageFormat]::Png)
$backBmp.Dispose()

Write-Host "UI elements cropped successfully."
$refImg.Dispose()
