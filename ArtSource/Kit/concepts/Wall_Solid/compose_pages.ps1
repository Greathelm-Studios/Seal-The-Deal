$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = $PSScriptRoot
$meta = Get-Content -LiteralPath (Join-Path $root 'render_metadata.json') -Raw | ConvertFrom-Json
$ink = [Drawing.ColorTranslator]::FromHtml('#111B38')
$blue = [Drawing.ColorTranslator]::FromHtml('#244FB9')
$white = [Drawing.ColorTranslator]::FromHtml('#F3F4F4')
$brush = [Drawing.SolidBrush]::new($ink)
$blueBrush = [Drawing.SolidBrush]::new($blue)
$whiteBrush = [Drawing.SolidBrush]::new($white)
$paperBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(245,243,244,244))
$font = [Drawing.Font]::new('Segoe UI',26,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
$small = [Drawing.Font]::new('Segoe UI',23,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
$head = [Drawing.Font]::new('Segoe UI',46,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$subhead = [Drawing.Font]::new('Segoe UI',30,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$dimFont = [Drawing.Font]::new('Segoe UI',27,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
$pen = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#61B7ED'),2)
$dirs = @(
    @('01_Floating_Field','01 / FLOATING FIELD','An uninterrupted white mass floats between recessed navy head and foot bands.','180 mm navy skirting / 200 mm navy head / 65 mm recess on both room faces'),
    @('02_Framed_Trays','02 / FRAMED TRAYS','Each module is a recessed white tray inside a broad white perimeter.','280 mm white skirting + crown / 100-120 mm navy trough / 40 mm field setback')
)

function Txt($g,$str,$x,$y,$f=$font,$b=$brush) {
    $g.DrawString([string]$str,$f,$b,[single]$x,[single]$y)
}
function Page($w,$h) {
    $bitmap = [Drawing.Bitmap]::new($w,$h)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear($white)
    $graphics.SmoothingMode = 'AntiAlias'
    $graphics.TextRenderingHint = 'AntiAliasGridFit'
    $graphics.InterpolationMode = 'HighQualityBicubic'
    return @($bitmap,$graphics)
}
function Pic($g,$path,$x,$y,$w,$h) {
    $im = [Drawing.Image]::FromFile($path)
    $g.DrawImage($im,[Drawing.Rectangle]::new($x,$y,$w,$h))
    $im.Dispose()
}
function Save($page,$path) {
    $page[1].Dispose()
    $page[0].Save($path,[Drawing.Imaging.ImageFormat]::Png)
    $page[0].Dispose()
}
function Header($g,$title,$subtitle,$w) {
    $g.FillRectangle($brush,0,0,$w,165)
    Txt $g $title 60 28 $head $whiteBrush
    Txt $g $subtitle 62 100 $font $whiteBrush
}
function Annotate($folder,$name) {
    $relative = (Split-Path $folder -Leaf)+'/'+$name+'.png'
    $entry = $meta.PSObject.Properties[$relative].Value
    if (!$entry) { throw "Missing render metadata $relative" }
    $im = [Drawing.Image]::FromFile((Join-Path $folder "$name.png"))
    $page = Page $im.Width $im.Height
    $bitmap = $page[0]
    $g = $page[1]
    $g.DrawImage($im,0,0,$im.Width,$im.Height)
    $im.Dispose()
    foreach ($d in $entry.dimensions) {
        $ax = [single]$d.a[0]
        $ay = [single]$d.a[1]
        $bx = [single]$d.b[0]
        $by = [single]$d.b[1]
        if ([Math]::Abs($bx-$ax) -gt [Math]::Abs($by-$ay)) {
            $yy = [single](($ay+$by)/2+$d.offset)
            $g.DrawLine($pen,$ax,$ay,$ax,$yy)
            $g.DrawLine($pen,$bx,$by,$bx,$yy)
            $g.DrawLine($pen,$ax,$yy,$bx,$yy)
            foreach ($xx in @($ax,$bx)) {
                $g.DrawLine($pen,[single]($xx-5),[single]($yy+6),[single]($xx+5),[single]($yy-6))
            }
            $tx = ($ax+$bx)/2
            $ty = $yy+5
        } else {
            $xx = [single](($ax+$bx)/2+$d.offset)
            $g.DrawLine($pen,$ax,$ay,$xx,$ay)
            $g.DrawLine($pen,$bx,$by,$xx,$by)
            $g.DrawLine($pen,$xx,$ay,$xx,$by)
            foreach ($yy in @($ay,$by)) {
                $g.DrawLine($pen,[single]($xx-6),[single]($yy+5),[single]($xx+6),[single]($yy-5))
            }
            $tx = $xx+8
            $ty = ($ay+$by)/2
        }
        $size = $g.MeasureString([string]$d.label,$dimFont)
        $tx = [Math]::Max(8,[Math]::Min($bitmap.Width-$size.Width-10,$tx-$size.Width/2))
        $g.FillRectangle($paperBrush,[single]($tx-4),[single]$ty,[single]($size.Width+8),[single]$size.Height)
        Txt $g $d.label $tx $ty $dimFont $blueBrush
    }
    Save $page (Join-Path $folder ($name+'_dimensioned.png'))
}

foreach ($d in $dirs) {
    $folder = Join-Path $root $d[0]
    foreach ($name in @('front','side','section','profile_detail')) { Annotate $folder $name }
    $page = Page 3000 2370
    $g = $page[1]
    Header $g ($d[1]+' / SOLID WALL') $d[2] 3000
    $panels = @(
        @('room','ROOM / eye 1.70 m / 4 + 4 + 2 m run',70,205),
        @('corner','INSIDE CORNER / original overlapping walls',1540,205),
        @('close_skirting','SKIRTING / close three-quarter / 2.67 m',70,1235),
        @('close_top','TOP BAND / close three-quarter / slab soffit',1540,1235)
    )
    foreach ($panel in $panels) {
        Txt $g $panel[1] $panel[2] $panel[3] $subhead
        Pic $g (Join-Path $folder ($panel[0]+'.png')) $panel[2] ($panel[3]+48) 1390 927
    }
    Txt $g $d[3] 70 2260 $font
    Txt $g 'Paper White + Trim_Navy / 1.80 m matte navy suit / ceiling, carpet, windows and city are context.' 70 2307 $small
    Save $page (Join-Path $folder 'presentation.png')

    $page = Page 3000 2420
    $g = $page[1]
    Header $g ($d[1]+' / DIMENSIONED TURNAROUND') '4 m + 2 m variants / metres / blue cross = original snap pivot / floor y = 0' 3000
    $panels = @(
        @('front_dimensioned','ROOM FRONT / true orthographic / 4 m and 2 m',70,205),
        @('side_dimensioned','END / true orthographic from +X / snap face',1540,205),
        @('three_quarter','ROOM THREE-QUARTER / 200 mm slabs as context',70,1240),
        @('section_dimensioned','MID-RUN SECTION / true orthographic / blue cut faces',1540,1240)
    )
    foreach ($panel in $panels) {
        Txt $g $panel[1] $panel[2] $panel[3] $subhead
        Pic $g (Join-Path $folder ($panel[0]+'.png')) $panel[2] ($panel[3]+48) 1390 927
    }
    Txt $g 'Envelope 4.00 / 2.00 x 0.20 x 4.30 m / 0 mm proud / unchanged full end, floor and top snap faces.' 70 2265 $font
    Txt $g 'Both room faces share the profile / continuous opaque core at least 70 mm / original box collider required.' 70 2311 $small
    Txt $g 'Full-size orthographic PNGs and enlarged foot section accompany this page. Production assets require Alex approval.' 70 2353 $small
    Save $page (Join-Path $folder 'sheet.png')

    $page = Page 1800 1500
    $g = $page[1]
    Header $g ($d[1]+' / FOOT SECTION') 'Enlarged centre-of-run cut / room faces left and right / original 200 mm envelope' 1800
    Pic $g (Join-Path $folder 'profile_detail_dimensioned.png') 60 220 1680 1120
    Txt $g $d[3] 60 1380 $small
    Txt $g 'Blue is the cut-face convention. Material and complete dimensions are in notes.md.' 60 1423 $small
    Save $page (Join-Path $folder 'profile_sheet.png')
}

$page = Page 3000 2170
$g = $page[1]
Header $g 'CLOSE THE DEAL / SOLID WALL / TWO DIRECTIONS' 'White Trays family / Paper White mass + navy recesses / same geometry envelope, lighting and cameras' 3000
for ($i=0; $i -lt 2; $i++) {
    $d = $dirs[$i]
    $folder = Join-Path $root $d[0]
    $x = 70+1470*$i
    Txt $g $d[1] $x 210 $subhead
    Pic $g (Join-Path $folder 'room.png') $x 265 1390 927
    Pic $g (Join-Path $folder 'close_skirting.png') $x 1270 670 447
    Pic $g (Join-Path $folder 'close_top.png') ($x+720) 1270 670 447
    Txt $g 'SKIRTING' $x 1730 $small
    Txt $g 'TOP / SLAB SOFFIT' ($x+720) 1730 $small
    $rect = [Drawing.RectangleF]::new($x,1805,1390,100)
    $g.DrawString([string]$d[2],$subhead,$brush,$rect)
    $rect = [Drawing.RectangleF]::new($x,1930,1390,120)
    $g.DrawString([string]$d[3],$font,$brush,$rect)
}
Txt $g 'PICK / FLOATING FIELD - strongest continuous white mass; the navy head and foot wrap the corner quietly.' 70 2080 $subhead
Save $page (Join-Path $root 'comparison.png')

# The images below are exactly 300 x 200 pixels, without a crop.
$page = Page 690 320
$g = $page[1]
$thumbFont = [Drawing.Font]::new('Segoe UI',19,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
Txt $g 'SOLID WALL / 300 px room-view comparison' 25 10 $thumbFont
for ($i=0; $i -lt 2; $i++) {
    $d = $dirs[$i]
    $x = 25+340*$i
    Txt $g $d[1] $x 47 $thumbFont
    Pic $g (Join-Path (Join-Path $root $d[0]) 'room.png') $x 80 300 200
}
Txt $g '01 continuous bands        02 individual framed fields' 25 291 $thumbFont
Save $page (Join-Path $root 'thumbnail_strip.png')
Write-Output 'Composed two presentations, two dimensioned turnarounds, two section details, comparison and literal 300 px strip.'
