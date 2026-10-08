Add-Type -AssemblyName System.Drawing
$refImg = [System.Drawing.Image]::FromFile("C:\Users\ocnelavoc\.gemini\antigravity-ide\brain\bae25978-78bc-4125-8b60-955986487bb6\.user_uploaded\media_1791488244884.jpg")
$bgImg = [System.Drawing.Image]::FromFile("c:\Projects\2026\game\dungeon-guardians\Assets\Resources\Backgrounds\map.png")

Write-Host "Reference Mockup (media_1791488244884.jpg):" $refImg.Width "x" $refImg.Height
Write-Host "Background Map (map.png):" $bgImg.Width "x" $bgImg.Height

$refImg.Dispose()
$bgImg.Dispose()
