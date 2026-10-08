Add-Type -AssemblyName System.Drawing
$refImg = [System.Drawing.Bitmap]::FromFile("C:\Users\ocnelavoc\.gemini\antigravity-ide\brain\bae25978-78bc-4125-8b60-955986487bb6\.user_uploaded\media_1791488244884.jpg")

# The 15 levels layout in 1024x576:
# Row 1 (Mine Shaft):
# 1: X ≈ 248, Y ≈ 98
# 2: X ≈ 395, Y ≈ 110
# 3: X ≈ 536, Y ≈ 120
# Row 2 (City):
# 4: X ≈ 286, Y ≈ 188
# 5: X ≈ 441, Y ≈ 202
# 6: X ≈ 586, Y ≈ 185
# Row 3 (Flooded Temple / Waters):
# 7: X ≈ 250, Y ≈ 268
# 8: X ≈ 410, Y ≈ 280
# 9: X ≈ 570, Y ≈ 286
# Row 4 (Machinery & Water Wheels):
# 10: X ≈ 252, Y ≈ 368
# 11: X ≈ 410, Y ≈ 372
# 12: X ≈ 556, Y ≈ 368
# Row 5 (Magma Core):
# 13: X ≈ 310, Y ≈ 450
# 14: X ≈ 430, Y ≈ 454
# 15: X ≈ 595, Y ≈ 456

$nodes = @(
    @{ id=1; x=248; y=98 },
    @{ id=2; x=395; y=110 },
    @{ id=3; x=536; y=120 },
    @{ id=4; x=286; y=188 },
    @{ id=5; x=441; y=202 },
    @{ id=6; x=586; y=185 },
    @{ id=7; x=250; y=268 },
    @{ id=8; x=410; y=280 },
    @{ id=9; x=570; y=286 },
    @{ id=10; x=252; y=368 },
    @{ id=11; x=410; y=372 },
    @{ id=12; x=556; y=368 },
    @{ id=13; x=310; y=450 },
    @{ id=14; x=430; y=454 },
    @{ id=15; x=595; y=456 }
)

foreach ($n in $nodes) {
    Write-Host "Node $($n.id): Vector2($($n.x)f, $($n.y)f)"
}

$refImg.Dispose()
