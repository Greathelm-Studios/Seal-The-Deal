Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
function Sheet($files, $destination, $columns, $tileWidth, $tileHeight) {
    $rows = [int][Math]::Ceiling($files.Count / $columns)
    $bmp = New-Object Drawing.Bitmap ($columns * $tileWidth), ($rows * $tileHeight)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.Clear([Drawing.Color]::White)
    $g.InterpolationMode = 'HighQualityBicubic'
    $font = New-Object Drawing.Font 'Arial', 15
    for ($i=0; $i -lt $files.Count; $i++) {
        $img = [Drawing.Image]::FromFile($files[$i])
        $x = ($i % $columns) * $tileWidth
        $y = [int][Math]::Floor($i / $columns) * $tileHeight
        $factor = [Math]::Min(($tileWidth-12)/$img.Width, ($tileHeight-42)/$img.Height)
        $g.DrawImage($img, [single]($x+6), [single]($y+36), [single]($img.Width*$factor), [single]($img.Height*$factor))
        $g.DrawString([IO.Path]::GetFileNameWithoutExtension($files[$i]), $font, [Drawing.Brushes]::Black, [single]($x+8), [single]($y+6))
        $img.Dispose()
    }
    $bmp.Save($destination, [Drawing.Imaging.ImageFormat]::Png)
    $font.Dispose(); $g.Dispose(); $bmp.Dispose()
}
$captures = @(Get-ChildItem (Join-Path $root 'ArtSource/Kit/review/unity_office') -Filter '*.png' | Sort-Object Name | ForEach-Object FullName)
Sheet $captures (Join-Path $PSScriptRoot 'source_capture_review.png') 3 800 495
$refs = @(Get-ChildItem (Join-Path $root 'ArtSource/Kit/concepts/Office/refs') -Filter '*.png' | Sort-Object Name | ForEach-Object FullName)
Sheet $refs (Join-Path $PSScriptRoot 'source_art_review.png') 3 700 620
Write-Output "Reviewed-source sheets: $($captures.Count) captures, $($refs.Count) art references."
