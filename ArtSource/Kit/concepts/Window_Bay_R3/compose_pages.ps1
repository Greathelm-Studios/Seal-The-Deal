$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root=$PSScriptRoot
$meta=Get-Content (Join-Path $root 'render_metadata.json') -Raw | ConvertFrom-Json
$ink=[Drawing.ColorTranslator]::FromHtml('#111B38')
$blue=[Drawing.ColorTranslator]::FromHtml('#244FB9')
$white=[Drawing.ColorTranslator]::FromHtml('#F3F4F4')
$brush=[Drawing.SolidBrush]::new($ink)
$blueBrush=[Drawing.SolidBrush]::new($blue)
$whiteBrush=[Drawing.SolidBrush]::new($white)
$paperBrush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(242,243,244,244))
$font=[Drawing.Font]::new('Segoe UI',24,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
$small=[Drawing.Font]::new('Segoe UI',21,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
$head=[Drawing.Font]::new('Segoe UI',46,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$subhead=[Drawing.Font]::new('Segoe UI',29,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$dimFont=[Drawing.Font]::new('Segoe UI',25,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$pen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#61B7ED'),2)
$dirs=@(
    @('01_White_Trays','01 / WHITE TRAYS','Broad porcelain shelves; the piers retreat into the opening.','120 mm sill recess / 160 mm pier setback','PICK / strongest 2001 silhouette; white is still the dominant mass.'),
    @('02_Blue_Spandrel','02 / BLUE SPANDREL','One metre of glossy blue glass wraps each solid sill.','1.00 m backpainted band / flush finish','Bolder colour read; needs a proposed opaque glass material.'),
    @('03_Split_Blades','03 / SPLIT BLADES','Each pier becomes two sharp white fins around a dark void.','180 mm slot depth / 140 mm slot width','Best close profile; finer at 300 px than either horizontal direction.')
)
function Txt($g,$str,$x,$y,$f=$font,$b=$brush){$g.DrawString([string]$str,$f,$b,[single]$x,[single]$y)}
function Page($w,$h){
    $b=[Drawing.Bitmap]::new($w,$h);$g=[Drawing.Graphics]::FromImage($b)
    $g.Clear($white);$g.SmoothingMode='AntiAlias';$g.TextRenderingHint='AntiAliasGridFit';$g.InterpolationMode='HighQualityBicubic'
    return @($b,$g)
}
function Pic($g,$path,$x,$y,$w,$h){
    $im=[Drawing.Image]::FromFile($path);$g.DrawImage($im,[Drawing.Rectangle]::new($x,$y,$w,$h));$im.Dispose()
}
function Save($p,$path){$p[1].Dispose();$p[0].Save($path,[Drawing.Imaging.ImageFormat]::Png);$p[0].Dispose()}
function Header($g,$title,$subtitle,$w){
    $g.FillRectangle($brush,0,0,$w,165)
    Txt $g $title 60 28 $head $whiteBrush
    Txt $g $subtitle 62 99 $font $whiteBrush
}
function Annotate($folder,$name){
    $relative=(Split-Path $folder -Leaf)+'\'+$name+'.png'
    $entry=$meta.PSObject.Properties[$relative].Value
    if(!$entry){throw "Missing metadata: $relative"}
    $im=[Drawing.Image]::FromFile((Join-Path $folder "$name.png"))
    $p=Page $im.Width $im.Height;$b=$p[0];$g=$p[1]
    $g.DrawImage($im,0,0,$im.Width,$im.Height);$im.Dispose()
    foreach($d in $entry.dimensions){
        $ax=[single]$d.a[0];$ay=[single]$d.a[1];$bx=[single]$d.b[0];$by=[single]$d.b[1]
        if([Math]::Abs($bx-$ax) -gt [Math]::Abs($by-$ay)){
            $yy=[single](($ay+$by)/2+$d.offset)
            $g.DrawLine($pen,$ax,$ay,$ax,$yy);$g.DrawLine($pen,$bx,$by,$bx,$yy);$g.DrawLine($pen,$ax,$yy,$bx,$yy)
            foreach($xx in @($ax,$bx)){$g.DrawLine($pen,[single]($xx-5),[single]($yy+6),[single]($xx+5),[single]($yy-6))}
            $tx=($ax+$bx)/2;$ty=$yy+5
        }else{
            $xx=[single](($ax+$bx)/2+$d.offset)
            $g.DrawLine($pen,$ax,$ay,$xx,$ay);$g.DrawLine($pen,$bx,$by,$xx,$by);$g.DrawLine($pen,$xx,$ay,$xx,$by)
            foreach($yy in @($ay,$by)){$g.DrawLine($pen,[single]($xx-6),[single]($yy+5),[single]($xx+6),[single]($yy-5))}
            $tx=$xx+8;$ty=($ay+$by)/2
        }
        $sz=$g.MeasureString([string]$d.label,$dimFont)
        $tx=[Math]::Max(8,[Math]::Min($b.Width-$sz.Width-10,$tx-$sz.Width/2))
        $g.FillRectangle($paperBrush,[single]($tx-4),[single]$ty,[single]($sz.Width+8),[single]$sz.Height)
        Txt $g $d.label $tx $ty $dimFont $blueBrush
    }
    Txt $g 'ORTHOGRAPHIC / metres / blue cross = original bay pivot at floor y = 0' 25 20 $small $whiteBrush
    Save $p (Join-Path $folder ($name+'_dimensioned.png'))
}
foreach($d in ($dirs | Select-Object -First 2)){
    $folder=Join-Path $root $d[0]
    foreach($name in @('front','side','section')){Annotate $folder $name}
    $p=Page 3000 2540;$g=$p[1]
    Header $g ($d[1]+' / ROUND 3') $d[2] 3000
    $panels=@(
        @('rival_B','RIVAL / 12 m / two panes / three floors',70,210),
        @('room_B','ROOM / eye height 1.70 m / three bays',1540,210),
        @('close','PROFILE / raking sun / camera 2.67 m from pier',70,1340),
        @('three_quarter','ONE BAY / shaded street three-quarter',1540,1340)
    )
    foreach($v in $panels){
        Txt $g $v[1] $v[2] $v[3] $subhead
        Pic $g (Join-Path $folder ($v[0]+'.png')) $v[2] ($v[3]+48) 1390 1042
    }
    Txt $g $d[3] 70 2460 $font
    Txt $g 'B / Deep Navy rails shown here; silver / navy comparison on rail_finishes.png.' 1540 2460 $small
    Save $p (Join-Path $folder 'presentation.png')

    $p=Page 3000 2610;$g=$p[1]
    Header $g ($d[1]+' / DIMENSIONED TURNAROUND') 'Fixed kit envelope / 1.80 m navy figures / 4 m bay / 2 m and corners on separate sheet' 3000
    $panels=@(
        @('front_dimensioned','ROOM FRONT / true orthographic',70,205),
        @('side_dimensioned','SIDE / from +X / true orthographic',1540,205),
        @('room_three_quarter','ROOM THREE-QUARTER / slab context',70,1360),
        @('section_dimensioned','MID-BAY SECTION / true orthographic',1540,1360)
    )
    foreach($v in $panels){
        Txt $g $v[1] $v[2] $v[3] $subhead
        Pic $g (Join-Path $folder ($v[0]+'.png')) $v[2] ($v[3]+48) 1390 1042
    }
    Txt $g '4.00 m grid | 4.30 m floor-to-soffit | 4.50 m floor-to-floor | 1.00 m solid sill | 0.20 m slab' 70 2480 $font
    Txt $g 'Pane 3.60 x 3.20 x 0.02 m | rails 0.05 high x 0.10 deep | facade column 0.40 x 0.24 m' 70 2522 $small
    Txt $g 'The section is through the bay centre, not the column; all carved sills retain continuous opaque backing.' 70 2555 $small
    Save $p (Join-Path $folder 'sheet.png')

    $p=Page 3000 2530;$g=$p[1]
    Header $g ($d[1]+' / RAIL FINISH') 'A = existing Silver Grey aluminium     B = proposed Deep Navy     Identical geometry, sun and cameras' 3000
    foreach($i in 0..1){
        $letter=@('A','B')[$i];$x=70+1470*$i
        Txt $g ($letter+' / RIVAL / 12 m') $x 205 $subhead
        Pic $g (Join-Path $folder ('rival_'+$letter+'.png')) $x 253 1390 1042
        Txt $g ($letter+' / ROOM / 1.70 m') $x 1350 $subhead
        Pic $g (Join-Path $folder ('room_'+$letter+'.png')) $x 1398 1390 1042
    }
    Txt $g 'Suit colour #111B38; glass has 95% direct transmission per whole pane; visibility shown through target + observer panes.' 70 2468 $small
    Save $p (Join-Path $folder 'rail_finishes.png')

    $p=Page 2000 1730;$g=$p[1]
    Header $g ($d[1]+' / 2 m + CORNERS') 'Same design section; handed columns remain inside the side-wall corner' 2000
    Pic $g (Join-Path $folder 'infill_corners.png') 70 220 1860 1395
    Txt $g '2.00 m run | 1.60 x 3.20 m pane | CornerL / CornerR: 0.42 x 0.24 x 4.30 m' 70 1638 $font
    Txt $g 'L extent: X -0.02 to +0.40 m | R extent: X -0.40 to +0.02 m | no snap face moved' 70 1678 $small
    Save $p (Join-Path $folder 'infill_corner_sheet.png')
}

$p=Page 3000 2820;$g=$p[1]
Header $g 'CLOSE THE DEAL / WINDOW BAY / ROUND 3' 'Whole-column and whole-sill moves / same sun, glass and cameras / pick: WHITE TRAYS with B navy rails' 3000
foreach($i in 0..1){
    $d=$dirs[$i];$folder=Join-Path $root $d[0];$x=70+$i*1480
    Txt $g $d[1] $x 215 $subhead
    Pic $g (Join-Path $folder 'rival_B.png') $x 270 1380 1035
    Txt $g '12 m / three bays x three floors / two panes' $x 1320 $small
    Pic $g (Join-Path $folder 'room_B.png') $x 1400 1380 1035
    Txt $g 'Room / 1.70 m / carpet + ceiling grid' $x 2450 $small
    Txt $g $d[3] $x 2520 $font
    $rect=[Drawing.RectangleF]::new($x,2580,1380,110)
    $g.DrawString($d[4],$subhead,$brush,$rect)
}
Txt $g 'Shortlist: the two strongest street reads / split-blade study culled at 300 px / see individual presentations and dimensioned sheets.' 70 2750 $small
Save $p (Join-Path $root 'comparison.png')

# Literal 300 px wide images: this file must not substitute larger views labelled as thumbnails.
$p=Page 690 380;$g=$p[1]
Txt $g '12 m / 300 px test / same camera and sun' 25 15 $subhead
foreach($i in 0..1){
    $d=$dirs[$i];$x=25+$i*340
    Txt $g $d[1] $x 67 $small
    Pic $g (Join-Path (Join-Path $root $d[0]) 'rival_B.png') $x 105 300 225
}
Txt $g '01: white shelves       02: blue ribbons' 25 347 $small
Save $p (Join-Path $root 'thumbnail_strip.png')
Write-Output 'Composed two-direction comparison, literal 300 px strip, and four review pages per shortlisted direction.'
