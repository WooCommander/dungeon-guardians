Add-Type -AssemblyName System.Drawing
$menuImg = [System.Drawing.Bitmap]::FromFile("c:\Projects\2026\game\dungeon-guardians\Assets\Resources\Backgrounds\menu.png")
$playSprite = [System.Drawing.Bitmap]::FromFile("c:\Projects\2026\game\dungeon-guardians\Assets\Resources\UI\menu_play.png")
$settingsSprite = [System.Drawing.Bitmap]::FromFile("c:\Projects\2026\game\dungeon-guardians\Assets\Resources\UI\menu_settings.png")
$plateSprite = [System.Drawing.Bitmap]::FromFile("c:\Projects\2026\game\dungeon-guardians\Assets\Resources\UI\menu_plate.png")

Write-Host "menu.png: $($menuImg.Width) x $($menuImg.Height)"
Write-Host "menu_play.png: $($playSprite.Width) x $($playSprite.Height)"
Write-Host "menu_settings.png: $($settingsSprite.Width) x $($settingsSprite.Height)"
Write-Host "menu_plate.png: $($plateSprite.Width) x $($plateSprite.Height)"

$menuImg.Dispose()
$playSprite.Dispose()
$settingsSprite.Dispose()
$plateSprite.Dispose()
