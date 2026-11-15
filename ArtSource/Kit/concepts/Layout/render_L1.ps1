param([switch]$ContactOnly)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
function Brush($hex) { New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml($hex)) }
function Pen($hex,$width,$dash) {
    $p = New-Object Drawing.Pen ([Drawing.ColorTranslator]::FromHtml($hex)), ([single]$width)
    if ($dash) { $p.DashStyle = [Drawing.Drawing2D.DashStyle]::Dash }
    return $p
}
function Render($file) {
    $data = Get-Content -LiteralPath $file -Raw -Encoding utf8 | ConvertFrom-Json
    $bmp = New-Object Drawing.Bitmap ([int]$data.width), ([int]$data.height)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    foreach ($d in $data.commands) {
        $br=$null; $pn=$null
        if ($d.fill -and $d.fill -ne 'none') { $br=Brush $d.fill }
        if ($d.stroke -and $d.stroke -ne 'none') { $pn=Pen $d.stroke $d.sw $d.dash }
        switch ($d.type) {
            'rect' {
                if ($br) { $g.FillRectangle($br,[single]$d.x,[single]$d.y,[single]$d.w,[single]$d.h) }
                if ($pn) { $g.DrawRectangle($pn,[single]$d.x,[single]$d.y,[single]$d.w,[single]$d.h) }
            }
            'circle' {
                if ($br) { $g.FillEllipse($br,[single]($d.x-$d.r),[single]($d.y-$d.r),[single](2*$d.r),[single](2*$d.r)) }
                if ($pn) { $g.DrawEllipse($pn,[single]($d.x-$d.r),[single]($d.y-$d.r),[single](2*$d.r),[single](2*$d.r)) }
            }
            'line' { $g.DrawLine($pn,[single]$d.a[0],[single]$d.a[1],[single]$d.b[0],[single]$d.b[1]) }
            'poly' {
                [Drawing.PointF[]]$pts = @($d.points | ForEach-Object { New-Object Drawing.PointF ([single]$_[0]), ([single]$_[1]) })
                if ($br) { $g.FillPolygon($br,$pts) }
                if ($pn) { $g.DrawLines($pn,$pts) }
            }
            'text' {
                $style = if($d.bold){[Drawing.FontStyle]::Bold}else{[Drawing.FontStyle]::Regular}
                $font=New-Object Drawing.Font 'Arial', ([single]$d.size), $style, ([Drawing.GraphicsUnit]::Pixel)
                $tb=Brush $d.color; $format=New-Object Drawing.StringFormat
                $format.LineAlignment=[Drawing.StringAlignment]::Center
                $format.Alignment=switch($d.align){'center'{[Drawing.StringAlignment]::Center}'right'{[Drawing.StringAlignment]::Far}default{[Drawing.StringAlignment]::Near}}
                $g.DrawString([string]$d.value,$font,$tb,[Drawing.PointF]::new([single]$d.x,[single]$d.y),$format)
                $format.Dispose();$font.Dispose();$tb.Dispose()
            }
        }
        if($br){$br.Dispose()};if($pn){$pn.Dispose()}
    }
    $out=$file -replace '\.draw\.json$','.png'
    $bmp.Save($out,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
    Write-Output "Rendered $([IO.Path]::GetFileName((Split-Path $file)))"
}
if(-not $ContactOnly) {
    Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -Filter '*.draw.json' | ForEach-Object { Render $_.FullName }
}
# Compare at a common plan scale: each technical sheet has the same native 40px/m.
$tileW=1340;$sheetW=4020;$sheetH=2900
$sheet=New-Object Drawing.Bitmap $sheetW,$sheetH
$g=[Drawing.Graphics]::FromImage($sheet);$g.Clear([Drawing.Color]::White);$g.InterpolationMode='HighQualityBicubic'
$font=New-Object Drawing.Font 'Arial',32,([Drawing.FontStyle]::Bold)
$small=New-Object Drawing.Font 'Arial',22
$names=@('L1_A','L1_B','L1_C')
for($i=0;$i -lt 3;$i++) {
    $x=$i*$tileW
    $folder=Join-Path $PSScriptRoot $names[$i]
    $g.DrawString($names[$i],$font,[Drawing.Brushes]::MidnightBlue,[single]($x+40),[single]20)
    foreach($row in @(@('plan.png',80,940),@('flow.png',1020,940))) {
        $img=[Drawing.Image]::FromFile((Join-Path $folder $row[0]))
        $g.DrawImage($img,[Drawing.Rectangle]::new($x,[int]$row[1],1340,[int]$row[2]))
        $img.Dispose()
    }
    $view=Join-Path $folder 'view.png'
    if(Test-Path -LiteralPath $view) {
        $img=[Drawing.Image]::FromFile($view)
        $scale=[Math]::Min(1260/$img.Width,840/$img.Height)
        $g.DrawImage($img,[single]($x+40),[single]1970,[single]($img.Width*$scale),[single]($img.Height*$scale));$img.Dispose()
    }
}
$g.DrawString('Plans share one metre scale. View images convey mood; geometry and metrics come from the code-drawn plans.',$small,[Drawing.Brushes]::MidnightBlue,40,2860)
$sheet.Save((Join-Path $PSScriptRoot 'contact_sheet_L1.png'),[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$small.Dispose();$g.Dispose();$sheet.Dispose()
Write-Output 'Contact sheet rendered.'
$source=[Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'L1_B/plan.png'))
$crop=$source.Clone([Drawing.Rectangle]::new(2110,240,570,1490),$source.PixelFormat)
$crop.Save((Join-Path $PSScriptRoot 'qa_sidebar.png'),[Drawing.Imaging.ImageFormat]::Png)
$crop.Dispose();$source.Dispose()
