param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$base = $PSScriptRoot
$data = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $base 'layouts_r5.json') | ConvertFrom-Json
$navy = [Drawing.ColorTranslator]::FromHtml('#111B38')
$cobalt = [Drawing.ColorTranslator]::FromHtml('#244FB9')
$cyan = [Drawing.ColorTranslator]::FromHtml('#20D4E6')
$pale = [Drawing.ColorTranslator]::FromHtml('#E9EDF2')
$blueGlass = [Drawing.ColorTranslator]::FromHtml('#72ABC7')
$white = [Drawing.Color]::White
function Brush($c) { return [Drawing.SolidBrush]::new($c) }
function Pen($c, $w) { return [Drawing.Pen]::new($c, [single]$w) }
function Font($size, $bold = $false) {
    $style = if ($bold) { [Drawing.FontStyle]::Bold } else { [Drawing.FontStyle]::Regular }
    return [Drawing.Font]::new('Arial', [single]$size, $style, [Drawing.GraphicsUnit]::Pixel)
}
function Text($g, $t, $x, $y, $size = 26, $bold = $false, $c = $navy) {
    $f = Font $size $bold
    $b = Brush $c
    $g.DrawString([string]$t, $f, $b, [single]$x, [single]$y)
    $f.Dispose(); $b.Dispose()
}
function Line($g, $p, $x1, $y1, $x2, $y2) {
    $g.DrawLine($p, [single]$x1, [single]$y1, [single]$x2, [single]$y2)
}
function Rect($g, $b, $x, $y, $w, $h) {
    $g.FillRectangle($b, [single]$x, [single]$y, [single]$w, [single]$h)
}
function Canvas($w, $h) {
    $bmp = [Drawing.Bitmap]::new([int]$w, [int]$h)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.Clear($white)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    return @($bmp, $g)
}
function Draw-Plan($family) {
    $p = $family.plan
    $pair = Canvas 1100 460
    $bmp = $pair[0]; $g = $pair[1]
    Text $g 'MAZE SECTION / TOP DOWN' 16 8 25 $true
    Text $g 'Proposed grid layout; A/B alternatives share the route diagram.' 16 40 18
    $s = if ($p.h -gt 12) { 21 } else { 26 }
    $ox = 32; $oy = 84
    $gridPen = Pen $pale 1
    for ($x = 0; $x -le $p.w; $x += 2) { Line $g $gridPen ($ox+$x*$s) $oy ($ox+$x*$s) ($oy+$p.h*$s) }
    for ($y = 0; $y -le $p.h; $y += 2) { Line $g $gridPen $ox ($oy+$y*$s) ($ox+$p.w*$s) ($oy+$y*$s) }
    $fill = Brush $pale
    $wallPen = Pen $navy 5
    $glassPen = Pen $blueGlass 4
    foreach ($r in $p.blocks) {
        Rect $g $fill ($ox+$r[0]*$s) ($oy+$r[1]*$s) ($r[2]*$s) ($r[3]*$s)
        $g.DrawRectangle($wallPen,[single]($ox+$r[0]*$s),[single]($oy+$r[1]*$s),[single]($r[2]*$s),[single]($r[3]*$s))
    }
    foreach ($r in $p.walls) { Line $g $wallPen ($ox+$r[0]*$s) ($oy+$r[1]*$s) ($ox+$r[2]*$s) ($oy+$r[3]*$s) }
    foreach ($r in $p.glass) { Line $g $glassPen ($ox+$r[0]*$s) ($oy+$r[1]*$s) ($ox+$r[2]*$s) ($oy+$r[3]*$s) }
    $coverFill = Brush $cobalt
    foreach ($r in $p.covers) { Rect $g $coverFill ($ox+$r[0]*$s) ($oy+$r[1]*$s) ($r[2]*$s) ($r[3]*$s) }
    $routePen = Pen $cobalt 3
    $routePen.DashStyle = [Drawing.Drawing2D.DashStyle]::Dash
    $cap = [Drawing.Drawing2D.AdjustableArrowCap]::new(3,4)
    $routePen.CustomEndCap = $cap
    foreach ($route in @($p.route, $p.branch)) {
        for ($i=1; $i -lt $route.Count; $i++) {
            Line $g $routePen ($ox+$route[$i-1][0]*$s) ($oy+$route[$i-1][1]*$s) ($ox+$route[$i][0]*$s) ($oy+$route[$i][1]*$s)
        }
    }
    if ($p.spiral) {
        $r = $p.spiral
        $altPen = Pen $cobalt 2
        $altPen.DashStyle = [Drawing.Drawing2D.DashStyle]::Dash
        $g.DrawEllipse($altPen,[single]($ox+($r[0]-$r[2])*$s),[single]($oy+($r[1]-$r[2])*$s),[single](2*$r[2]*$s),[single](2*$r[2]*$s))
        Text $g 'A' ($ox+7.6*$s) ($oy+7.4*$s) 22 $true $cobalt
        $altPen.Dispose()
    }
    if ($p.stair) {
        $r=$p.stair
        for ($i=0;$i -le 12;$i++) {
            $yy=$r[1]+$i*$r[3]/12
            Line $g $glassPen ($ox+$r[0]*$s) ($oy+$yy*$s) ($ox+($r[0]+$r[2])*$s) ($oy+$yy*$s)
        }
    }
    if ($p.ramp) {
        $r=$p.ramp
        $g.DrawRectangle($glassPen,[single]($ox+$r[0]*$s),[single]($oy+$r[1]*$s),[single]($r[2]*$s),[single]($r[3]*$s))
        Line $g $routePen ($ox+($r[0]+1)*$s) ($oy+($r[1]+$r[3])*$s) ($ox+($r[0]+1)*$s) ($oy+$r[1]*$s)
    }
    $sightPen = Pen $navy 2
    $sightPen.DashStyle = [Drawing.Drawing2D.DashStyle]::Dot
    foreach ($r in $p.sights) {
        Line $g $sightPen ($ox+$r[0]*$s) ($oy+$r[1]*$s) ($ox+$r[2]*$s) ($oy+$r[3]*$s)
        Text $g ([string]$r[4]) ($ox+(($r[0]+$r[2])/2)*$s-28) ($oy+(($r[1]+$r[3])/2)*$s-23) 16 $true
    }
    foreach ($r in $p.labels) { Text $g ([string]$r[2]) ($ox+$r[0]*$s) ($oy+$r[1]*$s) 14 }
    $lx=455
    Text $g 'READING THE PLAN' $lx 82 23 $true
    Line $g $wallPen $lx 128 ($lx+55) 128
    Text $g 'Solid wall / full-height hard cover' ($lx+70) 113 21
    Line $g $glassPen $lx 172 ($lx+55) 172
    Text $g 'Clear pane + high frosted header' ($lx+70) 157 21
    Rect $g $coverFill $lx 204 55 19
    Text $g '1.1 m low cover' ($lx+70) 199 21
    Line $g $routePen $lx 256 ($lx+55) 256
    Text $g 'Loop / branch / alternate route' ($lx+70) 241 21
    Line $g $sightPen $lx 300 ($lx+55) 300
    Text $g 'Exposed sightline length (metres)' ($lx+70) 285 21
    Text $g ('2 m grid / '+$p.w+' x '+$p.h+' m section') $lx 336 22 $true
    Text $g 'Illustrative section, not a finished combat map.' $lx 371 19
    foreach($item in @($gridPen,$fill,$wallPen,$glassPen,$coverFill,$routePen,$cap,$sightPen)) { $item.Dispose() }
    $g.Dispose()
    $bmp.Save((Join-Path $base ($family.folder+'/layout_r5.png')),[Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
foreach ($family in $data.families) {
    Draw-Plan $family
    $raw = [Drawing.Image]::FromFile((Join-Path $base ($family.folder+'/views_r5.png')))
    $pair = Canvas 2560 2480
    $bmp=$pair[0]; $g=$pair[1]
    Text $g ($family.title+' / ROUND 5') 40 20 46 $true
    Text $g ('A / '+$family.a) 40 88 33 $true
    Text $g ('B / '+$family.b) 1290 88 33 $true
    Text $g $family.captionA 40 132 25
    Text $g $family.captionB 1290 132 25
    $cellW=1230; $cellH=820
    for ($row=0;$row -lt 2;$row++) {
        for ($col=0;$col -lt 2;$col++) {
            $dx=40+$col*1250; $dy=185+$row*846
            $dest=[Drawing.Rectangle]::new($dx,$dy,$cellW,$cellH)
            $sx=$col*$raw.Width/2; $sy=$row*$raw.Height/2
            $g.DrawImage($raw,$dest,[single]$sx,[single]$sy,[single]($raw.Width/2),[single]($raw.Height/2),[Drawing.GraphicsUnit]::Pixel)
            $bb=Brush $navy
            Rect $g $bb $dx $dy 290 34
            $label=if($row -eq 0){'HERO / 1.7 m eye'}else{'PLAY / 1.7 m eye'}
            Text $g $label ($dx+9) ($dy+4) 21 $false $white
            $bb.Dispose()
        }
    }
    $raw.Dispose()
    $plan=[Drawing.Image]::FromFile((Join-Path $base ($family.folder+'/layout_r5.png')))
    $g.DrawImage($plan,[Drawing.Rectangle]::new(40,1885,1100,460))
    $plan.Dispose()
    Text $g ('BUILD '+$family.pick+' / PROPOSED') 1200 1895 31 $true $cobalt
    for($i=0;$i -lt $family.notes.Count;$i++) {
        Text $g $family.notes[$i] 1200 (1950+$i*49) 26
    }
    Text $g 'All forms flush or recessed; proud detail 0 mm.' 1200 2160 25 $true
    Text $g '4 m / 2 m modules; standard doors 1.2 x 2.4 m.' 1200 2204 25
    Text $g 'Paintings are proposals; README_r5.md governs dimensions.' 1200 2248 23
    Text $g 'A = glossy 2001 corporate future / B = most abstract playable take' 40 2375 26
    Text $g 'FEEDBACK_05 / concepts for review / no street insets / built-in ImageGen' 40 2418 23
    $g.Dispose()
    $bmp.Save((Join-Path $base ($family.folder+'/sheet_r5.png')),[Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}
$pair=Canvas 3200 2960
$bmp=$pair[0]; $g=$pair[1]
Text $g 'Office labyrinth / Round 5' 40 16 55 $true
Text $g 'Glossy corporate future / 10 directions / 20 eye-level views / 5 maze sections' 40 83 31
for($i=0;$i -lt $data.families.Count;$i++){
    $family=$data.families[$i]
    $y=150+$i*530
    Text $g ($family.title+' / suggested build '+$family.pick) 40 $y 29 $true
    $raw=[Drawing.Image]::FromFile((Join-Path $base ($family.folder+'/views_r5.png')))
    $positions=@(@(0,0,'A / HERO'),@(0,1,'A / PLAY'),@(1,0,'B / HERO'),@(1,1,'B / PLAY'))
    for($j=0;$j -lt 4;$j++){
        $col=$positions[$j][0];$row=$positions[$j][1];$x=40+$j*670
        Text $g ($positions[$j][2]+' / '+$(if($col -eq 0){$family.a}else{$family.b})) $x ($y+40) 22 $true
        $dest=[Drawing.Rectangle]::new($x,($y+75),660,440)
        $g.DrawImage($raw,$dest,[single]($col*$raw.Width/2),[single]($row*$raw.Height/2),[single]($raw.Width/2),[single]($raw.Height/2),[Drawing.GraphicsUnit]::Pixel)
    }
    $raw.Dispose()
    $plan=[Drawing.Image]::FromFile((Join-Path $base ($family.folder+'/layout_r5.png')))
    Text $g 'TOP DOWN' 2750 ($y+40) 22 $true
    $dest=[Drawing.Rectangle]::new(2750,($y+90),400,380)
    $g.DrawImage($plan,$dest,0,65,445,365,[Drawing.GraphicsUnit]::Pixel)
    $plan.Dispose()
}
Text $g 'Suggested mix A / B / A / B / B.  Six image tries, including one atrium rail correction.' 40 2820 29 $true
Text $g '1.7 m eye height requested in all views; concept proportions are approximate. Dimensions and material targets: README_r5.md.' 40 2868 25
Text $g 'Original rounds preserved. Glass shooting rules belong to the shooting lane; the open gap depicts a cleared break state.' 40 2908 25
$g.Dispose()
$bmp.Save((Join-Path $base 'contact_sheet_r5.png'),[Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output 'Composed 5 family sheets, 5 top-down sketches, and contact_sheet_r5.png.'

