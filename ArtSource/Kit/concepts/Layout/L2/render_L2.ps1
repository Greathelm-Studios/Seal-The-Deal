$ErrorActionPreference = 'Stop'
# Reuse L1's renderer definitions only; its recursive L1 render and sheet never run.
$source=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../render_L1.ps1') -Raw
$start=$source.IndexOf('Add-Type')
$end=$source.IndexOf('if(-not $ContactOnly)')
Invoke-Expression $source.Substring($start,$end-$start)
foreach($name in @('plan','flow','stack')) { Render (Join-Path $PSScriptRoot "$name.draw.json") }
# Five tiles at a common 20px/m: full plans at exactly 50%, no per-floor rescaling.
$sheet=New-Object Drawing.Bitmap 4020,2040
$g=[Drawing.Graphics]::FromImage($sheet)
$g.Clear([Drawing.Color]::White)
$g.InterpolationMode='HighQualityBicubic'
$font=New-Object Drawing.Font 'Arial',28,([Drawing.FontStyle]::Bold),([Drawing.GraphicsUnit]::Pixel)
$small=New-Object Drawing.Font 'Arial',22,([Drawing.GraphicsUnit]::Pixel)
$g.DrawString('L2 HYBRID / COMMON SCALE COMPARISON',$font,[Drawing.Brushes]::MidnightBlue,40,20)
$items=@(@('../L1_A/plan.png','L1_A / CENTRAL CORE',0,80),@('../L1_B/plan.png','L1_B / EDGE CORE',1340,80),@('../L1_C/plan.png','L1_C / SPLIT CORES',2680,80),@('plan.png','L2 / TWIN-CORE COURT',0,1070),@('flow.png','L2 / ROUTES + GATES',1340,1070))
foreach($item in $items){
 $img=[Drawing.Image]::FromFile((Join-Path $PSScriptRoot $item[0]))
 $x=[int]$item[2];$y=[int]$item[3]
 $g.DrawImage($img,[Drawing.Rectangle]::new($x,$y,1340,940));$img.Dispose()
 $g.DrawString($item[1],$font,[Drawing.Brushes]::MidnightBlue,$x+35,$y)
}
$g.DrawString('KEEP',$font,[Drawing.Brushes]::MidnightBlue,2750,1150)
$notes=@('A: one dependable continuous ring','B: visible court and lift arrival','C: a nearby enclosed stair per firm','','CHANGE','40m wide; L1_C is 48m','Core faces become two-door rooms','Court has hard cover + solid canopy','Gate aperture on every stair arrival','','20 pixels = 1 metre in every tile','Native technical sheets: 40px/m','Target concept; see L2/README.md')
for($i=0;$i -lt $notes.Count;$i++){$g.DrawString($notes[$i],$small,[Drawing.Brushes]::MidnightBlue,2750,1200+$i*48)}
$sheet.Save((Join-Path $PSScriptRoot '../contact_sheet_L2.png'),[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$small.Dispose();$g.Dispose();$sheet.Dispose()
Write-Output 'Rendered contact_sheet_L2.png; L1 images read only.'
