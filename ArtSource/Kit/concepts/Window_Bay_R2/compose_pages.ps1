$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = $PSScriptRoot
$meta = Get-Content (Join-Path $root 'render_metadata.json') -Raw | ConvertFrom-Json
$navy = [Drawing.ColorTranslator]::FromHtml('#111B38')
$blue = [Drawing.ColorTranslator]::FromHtml('#244FB9')
$white = [Drawing.ColorTranslator]::FromHtml('#F3F4F4')
$brush = [Drawing.SolidBrush]::new($navy)
$blueBrush = [Drawing.SolidBrush]::new($blue)
$paperBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(235,243,244,244))
$font = [Drawing.Font]::new('Segoe UI',23,[Drawing.FontStyle]::Regular)
$small = [Drawing.Font]::new('Segoe UI',19,[Drawing.FontStyle]::Regular)
$heading = [Drawing.Font]::new('Segoe UI',38,[Drawing.FontStyle]::Bold)
$dimFont = [Drawing.Font]::new('Segoe UI',20,[Drawing.FontStyle]::Bold)
$pen = [Drawing.Pen]::new($blue,2)

function Text($g, $text, $x, $y, $f=$font) { $g.DrawString($text,$f,$brush,[single]$x,[single]$y) }
function New-Page($w,$h) {
    $b=[Drawing.Bitmap]::new($w,$h); $g=[Drawing.Graphics]::FromImage($b)
    $g.Clear($white); $g.SmoothingMode='AntiAlias'; $g.TextRenderingHint='AntiAliasGridFit'
    return @($b,$g)
}
function Picture($g,$path,$x,$y,$w,$h) {
    $im=[Drawing.Image]::FromFile($path)
    $g.DrawImage($im,[Drawing.Rectangle]::new($x,$y,$w,$h)); $im.Dispose()
}
function Annotate($folder,$name) {
    $path=Join-Path $folder "$name.png"
    $entry=$meta.PSObject.Properties[$path].Value
    $im=[Drawing.Image]::FromFile($path)
    $b=[Drawing.Bitmap]::new($im.Width,$im.Height); $g=[Drawing.Graphics]::FromImage($b)
    $g.DrawImage($im,0,0,$im.Width,$im.Height); $im.Dispose()
    $g.SmoothingMode='AntiAlias'; $g.TextRenderingHint='AntiAliasGridFit'
    foreach($d in $entry.dimensions) {
        $ax=[single]$d.a[0]; $ay=[single]$d.a[1]; $bx=[single]$d.b[0]; $by=[single]$d.b[1]
        if([Math]::Abs($bx-$ax) -gt [Math]::Abs($by-$ay)) {
            $yy=[single](($ay+$by)/2+$d.offset)
            $g.DrawLine($pen,$ax,$ay,$ax,$yy); $g.DrawLine($pen,$bx,$by,$bx,$yy)
            $g.DrawLine($pen,$ax,$yy,$bx,$yy)
            foreach($xx in @($ax,$bx)){$g.DrawLine($pen,[single]($xx-5),[single]($yy+6),[single]($xx+5),[single]($yy-6))}
            $tx=($ax+$bx)/2; $ty=$yy+5
        } else {
            $xx=[single](($ax+$bx)/2+$d.offset)
            $g.DrawLine($pen,$ax,$ay,$xx,$ay); $g.DrawLine($pen,$bx,$by,$xx,$by)
            $g.DrawLine($pen,$xx,$ay,$xx,$by)
            foreach($yy in @($ay,$by)){$g.DrawLine($pen,[single]($xx-6),[single]($yy+5),[single]($xx+6),[single]($yy-5))}
            $tx=$xx+8; $ty=($ay+$by)/2
        }
        $size=$g.MeasureString([string]$d.label,$dimFont)
        $tx=[Math]::Max(5,[Math]::Min($b.Width-$size.Width-8,$tx-$size.Width/2))
        $g.FillRectangle($paperBrush,[single]($tx-4),[single]$ty,[single]($size.Width+8),[single]$size.Height)
        $g.DrawString([string]$d.label,$dimFont,$blueBrush,[single]$tx,[single]$ty)
    }
    Text $g 'y = 0 at blue pivot cross | scale figure 1.80 m' 24 18 $small
    $g.Dispose(); $b.Save((Join-Path $folder ($name+'_dimensioned.png'))); $b.Dispose()
}

$directions=@(
    @('01_Dark_Bands','01 / DARK BANDS','White piers are scored with deep reveals; navy belts stitch the street elevation together.'),
    @('02_Lit_Piers','02 / LIT PIERS','Soft-edged white piers hold a recessed luminous ribbon above a floating silver sill cap.'),
    @('03_Cobalt_Corners','03 / COBALT CORNERS','A diagonal cobalt graphic marks corners; the repeating field stays white and silver.')
)
foreach($d in $directions) {
    $folder=Join-Path $root $d[0]
    foreach($name in @('front','side','section')) { Annotate $folder $name }
    $p=New-Page 3000 2420; $b=$p[0]; $g=$p[1]
    Text $g ($d[1]+' / SHADED TURNAROUND') 70 38 $heading
    Text $g $d[2] 70 110 $font
    $panels=@(
        @('front_dimensioned','ROOM FRONT / orthographic',70,185,'4 m bay + facade piers; A silver rails'),
        @('side_dimensioned','SIDE / orthographic from +X',1540,185,'0.40 m pier width; 0.24 m total depth'),
        @('room_three_quarter','ROOM THREE-QUARTER / shaded',70,1230,'B navy rails; 0.20 m slabs are context; extra street angle in folder'),
        @('section_dimensioned','MID-BAY SECTION / orthographic',1540,1230,'40 mm cut slice; solid sill; no pier in cut')
    )
    foreach($v in $panels) {
        $x=[int]$v[2];$y=[int]$v[3]
        Text $g $v[1] $x $y $font
        Picture $g (Join-Path $folder ($v[0]+'.png')) $x ($y+48) 1390 943
        Text $g $v[4] $x ($y+998) $small
    }
    Text $g 'Exact source sizes | Unity: X along run, Y up, room is -Z | Blue cross: bay pivot projected at floor level' 70 2315 $small
    Text $g 'Rails 0.05 high x 0.10 deep | pane 3.60 x 3.20 x 0.02 m | fixed snap faces | all dimensions in metres' 70 2355 $small
    $g.Dispose();$b.Save((Join-Path $folder 'sheet.png'));$b.Dispose()

    $p=New-Page 3000 2490; $b=$p[0];$g=$p[1]
    Text $g ($d[1]+' / RAIL FINISH COMPARISON') 70 38 $heading
    Text $g 'A = Silver Grey aluminium     B = Deep Navy finish proposal     Same geometry, lights and camera in each pair.' 70 112 $font
    foreach($i in 0..1) {
        $letter=@('A','B')[$i];$x=70+1470*$i
        Text $g ($letter+' / ROOM HERO - camera height 1.70 m') $x 190 $font
        Picture $g (Join-Path $folder ('hero_'+$letter+'.png')) $x 240 1390 943
        Text $g ($letter+' / STREET - camera 12.00 m across gap') $x 1245 $font
        Picture $g (Join-Path $folder ('street_'+$letter+'.png')) $x 1295 1390 943
    }
    Text $g 'Three bays / two floors | floor-to-floor 4.50 m | 1.80 m navy figures stand behind the target pane.' 70 2300 $font
    Text $g 'Street camera also looks through an observer pane: two 95% transparent concept surfaces, no mirror reflection.' 70 2350 $small
    Text $g 'Concept visibility comparison only; Unity glass, multiplayer visibility and gameplay have not been tested.' 70 2392 $small
    $g.Dispose();$b.Save((Join-Path $folder 'presentation.png'));$b.Dispose()

    $p=New-Page 2200 1720;$b=$p[0];$g=$p[1]
    Text $g ($d[1]+' / 2 m INFILL + HANDED CORNERS') 60 35 $heading
    Picture $g (Join-Path $folder 'infill_corners.png') 100 150 2000 1357
    Text $g '2.00 m grid run | 1.60 x 3.20 m pane | CornerL X -0.02 to +0.40 | CornerR X -0.40 to +0.02' 60 1540 $small
    Text $g 'Corner columns: 0.42 wide x 0.24 deep x 4.30 high; return walls shown as context; A silver rails.' 60 1585 $small
    Text $g 'Same section at 2 m and 4 m; no intermediate mullions. Decorative relief does not alter snap extents.' 60 1630 $small
    $g.Dispose();$b.Save((Join-Path $folder 'infill_corner_sheet.png'));$b.Dispose()
}

$p=New-Page 3000 1910;$b=$p[0];$g=$p[1]
Text $g 'CLOSE THE DEAL / WINDOW BAY / ROUND 2' 70 35 $heading
Text $g 'Three architectural directions | rendered from the kit dimensions | proposals for Alex to review' 70 110 $font
foreach($i in 0..2) {
    $d=$directions[$i];$folder=Join-Path $root $d[0];$y=190+$i*550
    Text $g $d[1] 70 $y $font
    Picture $g (Join-Path $folder 'hero_A.png') 70 ($y+45) 690 468
    Picture $g (Join-Path $folder 'street_A.png') 790 ($y+45) 690 468
    Picture $g (Join-Path $folder 'hero_B.png') 1510 ($y+45) 690 468
    Picture $g (Join-Path $folder 'street_B.png') 2230 ($y+45) 690 468
}
Text $g 'LEFT PAIR: A silver rails / room + street     RIGHT PAIR: B navy rails / room + street     See each folder for full-size pages.' 70 1850 $small
$g.Dispose();$b.Save((Join-Path $root 'comparison.png'));$b.Dispose()
Write-Output 'Composed 3 shaded turnarounds, 3 A/B presentations, 3 infill/corner pages and comparison.png.'
