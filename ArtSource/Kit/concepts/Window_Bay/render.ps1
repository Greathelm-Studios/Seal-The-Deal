Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
function Brush($hex) { [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($hex)) }
function Rect($x,$y,$w,$h,$color) {
    $b = Brush $color
    $g.FillRectangle($b,[single]$x,[single]$y,[single]$w,[single]$h)
    $b.Dispose()
}
function Label($x,$y,$value,$size=17,$color='#33445A') {
    $b = Brush $color
    $f = [Drawing.Font]::new('Segoe UI',[single]$size,[Drawing.GraphicsUnit]::Pixel)
    $g.DrawString($value,$f,$b,[single]$x,[single]$y)
    $f.Dispose()
    $b.Dispose()
}
function Line($x,$y,$xx,$yy,$color='#66768B') {
    $p = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($color))
    $g.DrawLine($p,[single]$x,[single]$y,[single]$xx,[single]$yy)
    $p.Dispose()
}
function Poly($points,$color) {
    $b = Brush $color
    $g.FillPolygon($b,[Drawing.PointF[]]$points)
    $b.Dispose()
}
function Pt($x,$y) { [Drawing.PointF]::new($x,$y) }
function Person($x,$y,$s,$color='#111B38') {
    $b = Brush $color
    $g.FillEllipse($b,[single]($x-.09*$s),[single]($y-1.8*$s),[single](.18*$s),[single](.22*$s))
    $b.Dispose()
    $shape = @(-.055,1.59,-.21,1.52,-.25,1.01,-.17,.99,-.13,1.31,-.14,.89,-.12,0,-.02,0,0,.80,.04,.0,.15,0,.14,.94,.13,1.31,.18,.99,.25,1.01,.21,1.52,.055,1.59)
    $points = for ($i=0;$i -lt $shape.Count;$i+=2) { Pt ($x+$shape[$i]*$s) ($y-$shape[$i+1]*$s) }
    Poly $points $color
    Poly @((Pt ($x-.047*$s) ($y-1.57*$s)),(Pt ($x+.047*$s) ($y-1.57*$s)),(Pt $x ($y-1.38*$s))) '#F3F4F4'
}
function Cross($x,$y) {
    Line ($x-7) $y ($x+7) $y '#244FB9'
    Line $x ($y-7) $x ($y+7) '#244FB9'
}
function DimX($a,$b,$y,$caption) {
    Line $a $y $b $y
    Line $a ($y-5) $a ($y+5)
    Line $b ($y-5) $b ($y+5)
    Label (($a+$b)/2-21) ($y+5) $caption 14
}
function DimY($x,$a,$b,$caption) {
    Line $x $a $x $b
    Line ($x-5) $a ($x+5) $a
    Line ($x-5) $b ($x+5) $b
    Label ($x-55) (($a+$b)/2-10) $caption 14
}
function Elevation($ox,$oy,$kind,$trim,$run) {
    $s=94; $x=$ox+96; $y=$oy+485
    Line ($ox+20) $y ($ox+566) $y
    if ($kind -eq 'front') {
        Rect $x ($y-4.3*$s) ($run*$s) (4.3*$s) '#F8FAFB'
        Person ($x+.57*$run*$s) $y $s '#242D49'
        Rect $x ($y-$s) ($run*$s) $s '#F3F4F4'
        foreach ($h in @(1,4.25)) {
            Rect ($x+.2*$s) ($y-($h+.05)*$s) (($run-.4)*$s) (.05*$s) $trim
        }
        foreach ($edge in @(0,$run)) {
            Rect ($x+($edge-.2)*$s) ($y-4.3*$s) (.4*$s) (4.3*$s) '#E6E9EB'
            Line ($x+($edge+.2)*$s) ($y-4.3*$s) ($x+($edge+.2)*$s) $y '#C9CED3'
        }
        DimX $x ($x+$run*$s) ($y+23) ('{0:0.0} m' -f $run)
        DimY ($x-25) ($y-$s) $y '1.0 m'
        Person ($ox+535) $y $s
    } else {
        $x=$ox+250
        if ($kind -eq 'side') {
            Rect ($x-.02*$s) ($y-4.3*$s) (.24*$s) (4.3*$s) '#E6E9EB'
            DimX ($x-.02*$s) ($x+.22*$s) ($y+23) '0.24 m'
            Label ($x+33) ($y-225) 'Façade column' 15
            $cx=$ox+405
            Rect $cx ($y-4.3*$s) (.24*$s) (4.3*$s) '#F3F4F4'
            DimX $cx ($cx+.24*$s) ($y+23) '0.24 m'
            Label ($cx-24) ($y-4.3*$s-25) 'Corner L/R' 15
        } else {
            Rect $x ($y-$s) (.2*$s) $s '#F3F4F4'
            Rect ($x+.09*$s) ($y-4.25*$s) (.02*$s) (3.2*$s) '#C9D9E5'
            foreach ($h in @(1,4.25)) {
                Rect ($x+.05*$s) ($y-($h+.05)*$s) (.1*$s) (.05*$s) $trim
            }
            DimX $x ($x+.2*$s) ($y+23) '0.20 m'
            DimY ($x-30) ($y-$s) $y '1.0 m'
            Label ($x+32) ($y-260) '20 mm glass' 15
            Label ($x+32) ($y-103) '50 x 100 mm rail' 15
        }
        Person ($ox+530) $y $s
    }
    DimY ($ox+65) ($y-4.3*$s) $y '4.3 m'
    Cross $x $y
    Label ($ox+22) ($y+5) 'y = 0' 13
}
function Project($x,$y,$z) {
    Pt ($anchorX+$x*$u+$sign*$z*.66*$u) ($anchorY-$y*$u+$sign*$x*.24*$u-$z*.39*$u)
}
function Face($points,$color) {
    $projected = foreach ($p in $points) { Project $p[0] $p[1] $p[2] }
    Poly $projected $color
}
function Box([double]$x,[double]$y,[double]$z,[double]$w,[double]$h,[double]$d,$color,$top,$edge) {
    $a=$x+$w; $b=$y+$h; $c=$z+$d
    $f=$z; $e=$a
    if ($sign -lt 0) { $f=$c; $e=$x }
    Face @(@($e,$y,$z),@($e,$y,$c),@($e,$b,$c),@($e,$b,$z)) $edge
    Face @(@($x,$y,$f),@($a,$y,$f),@($a,$b,$f),@($x,$b,$f)) $color
    Face @(@($x,$b,$z),@($a,$b,$z),@($a,$b,$c),@($x,$b,$c)) $top
}
function Perspective($ox,$oy,$trim,$street) {
    $u=86; $sign=1; $anchorX=$ox+92; $anchorY=$oy+432
    if ($street) { $sign=-1; $anchorY=$oy+514 }
    $base=Project 0 0 0
    Line ($ox+22) $base.Y ($ox+88) $base.Y
    if ($street) {
        Box 0 -.2 -.43 4 .2 .63 '#E5E9EC' '#F3F4F4' '#CDD4DA'
    }
    Face @(@(.2,1.05,.1),@(3.8,1.05,.1),@(3.8,4.25,.1),@(.2,4.25,.1)) '#EEF5FA'
    $behind=Project 2.3 0 $(if($street){-.31}else{.42})
    $panePath=[Drawing.Drawing2D.GraphicsPath]::new()
    $panePath.AddPolygon([Drawing.PointF[]]@((Project .2 1.05 .1),(Project 3.8 1.05 .1),(Project 3.8 4.25 .1),(Project .2 4.25 .1)))
    $saved=$g.Save()
    $g.SetClip($panePath)
    Person $behind.X $behind.Y $u '#242D49'
    $g.Restore($saved)
    $panePath.Dispose()
    Box 0 0 0 4 1 .2 '#F3F4F4' '#FFFFFF' '#DCE1E5'
    foreach ($h in @(1,4.25)) {
        Box .2 $h .05 3.6 .05 .1 $trim $trim '#9BA7B2'
    }
    foreach ($end in @(-.2,3.8)) {
        Box $end 0 -.02 .4 4.3 .24 '#E8EBED' '#FAFBFC' '#D6DCE1'
    }
    if ($street) {
        Box 0 4.3 -.43 4 .2 .63 '#E5E9EC' '#F3F4F4' '#CDD4DA'
    } else {
        Box 4.55 0 -.02 .42 4.3 .24 '#E8EBED' '#FAFBFC' '#D6DCE1'
        Label ($ox+475) ($oy+172) 'Corner' 15
    }
    $p=Project 5.3 0 0
    Person $p.X $p.Y $u
    Cross $base.X $base.Y
    Label ($ox+23) ($base.Y+6) 'y = 0' 13
}
$files = @('front','side','three_quarter','section')
$panels = @()
foreach ($kind in $files) {
    $width = if ($kind -eq 'front') { 1800 } else { 1200 }
    $bmp=[Drawing.Bitmap]::new($width,720)
    $g=[Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode='AntiAlias'
    $g.Clear([Drawing.ColorTranslator]::FromHtml('#F7F9FB'))
    $view=@{front='Front elevation';side='Side elevation';three_quarter='Three-quarter views';section='Mid-run section'}[$kind]
    Label 25 13 'CLOSE THE DEAL  /  ENVELOPE STUDY' 15 '#244FB9'
    Label 25 36 $view 33 '#111B38'
    Line 26 88 ($width-26) 88 '#C9CED3'
    if ($kind -eq 'three_quarter') {
        Label 25 103 'Room side  /  4 m bay and corner' 20
        Label 625 103 'Street side  /  4 m bay and slab edges' 20
        Perspective 0 126 '#C9CED3' $false
        Perspective 600 126 '#C9CED3' $true
        Label 25 683 'Both angles use A. The front elevation compares A and B at the same width.' 16
    } else {
        Label 25 103 'A  /  Silver Grey' 20
        Label 625 103 'B  /  Deep Navy' 20
        Elevation 0 125 $kind '#C9CED3' 4
        Elevation 600 125 $kind '#111B38' 4
        if ($kind -eq 'front') {
            Label 25 681 'Clear pane width is 3.60 m on the 4 m module and 1.60 m on the 2 m module.' 16
        } elseif ($kind -eq 'side') {
            Label 25 681 'Looking from +X. End column masks the rails and glazing. Corner width along X is 0.42 m.' 16
        } else {
            Label 25 681 'Room side at left. Section crosses the pane at mid-run. Pivot cross projected onto the cut.' 16
        }
    }
    if ($kind -eq 'front') {
        Line 1200 98 1200 665 '#DEE4E9'
        Label 1225 103 '2 m module  /  A' 20
        Elevation 1200 125 'front' '#C9CED3' 2
    }
    Line 600 98 600 665 '#DEE4E9'
    $g.Dispose()
    $bmp.Save((Join-Path $PSScriptRoot "$kind.png"),[Drawing.Imaging.ImageFormat]::Png)
    $panels += $bmp
}
$sheet=[Drawing.Bitmap]::new(3000,1440)
$g=[Drawing.Graphics]::FromImage($sheet)
$g.Clear([Drawing.ColorTranslator]::FromHtml('#F7F9FB'))
for ($i=0;$i -lt 4;$i++) {
    $offsetX = @(0,1800,0,1200)[$i]
    $g.DrawImageUnscaled($panels[$i],$offsetX,720*[int][Math]::Floor($i/2))
    $panels[$i].Dispose()
}
$g.Dispose()
$sheet.Save((Join-Path $PSScriptRoot 'sheet.png'),[Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()


