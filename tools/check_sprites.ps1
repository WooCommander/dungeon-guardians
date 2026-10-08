Add-Type -AssemblyName System.Drawing
foreach ($file in (Get-ChildItem Assets/Resources/UI/map_*.png)) {
    $img = [System.Drawing.Image]::FromFile($file.FullName)
    Write-Host "$($file.Name) : $($img.Width)x$($img.Height)"
    $img.Dispose()
}
