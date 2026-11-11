param([string]$OutIco, [string]$OutPreview)
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function New-RoundedRect([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Render([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.CompositingQuality = 'HighQuality'
    $k = $s / 256.0
    $g.ScaleTransform($k, $k)

    # Tile: diagonal cyan -> purple gradient
    $inset = if ($s -le 16) { 0 } else { 8 }
    $tile = New-RoundedRect $inset $inset (256 - 2 * $inset) (256 - 2 * $inset) 56
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF 256, 256), ([System.Drawing.Color]::FromArgb(255, 57, 208, 255)), ([System.Drawing.Color]::FromArgb(255, 182, 92, 255))
    $g.FillPath($grad, $tile)

    # Soft top highlight for a bit of depth
    if ($s -ge 32) {
        $hl = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, -1), (New-Object System.Drawing.PointF 0, 257), ([System.Drawing.Color]::White), ([System.Drawing.Color]::White)
        $blend = New-Object System.Drawing.Drawing2D.ColorBlend 3
        $blend.Colors = [System.Drawing.Color[]]@([System.Drawing.Color]::FromArgb(60, 255, 255, 255), [System.Drawing.Color]::FromArgb(0, 255, 255, 255), [System.Drawing.Color]::FromArgb(0, 255, 255, 255))
        $blend.Positions = [single[]]@(0, 0.6, 1)
        $hl.InterpolationColors = $blend
        $g.FillPath($hl, $tile)
    }

    # Subtle shadow under the glyph
    $white = [System.Drawing.Color]::White
    $shadow = [System.Drawing.Color]::FromArgb(60, 14, 16, 22)
    $small = $s -le 24

    $speaker = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF 50, 102), (New-Object System.Drawing.PointF 88, 102),
        (New-Object System.Drawing.PointF 134, 62), (New-Object System.Drawing.PointF 134, 194),
        (New-Object System.Drawing.PointF 88, 154), (New-Object System.Drawing.PointF 50, 154))
    $offsetX = if ($small) { 10 } else { 0 }
    $wavePen = if ($small) { 26 } else { 17 }

    foreach ($pass in 'shadow', 'main') {
        $color = if ($pass -eq 'shadow') { $shadow } else { $white }
        $dy = if ($pass -eq 'shadow' -and -not $small) { 6 } else { 0 }
        if ($pass -eq 'shadow' -and $small) { continue }

        $state = $g.Save()
        $g.TranslateTransform($offsetX, $dy)

        $fill = New-Object System.Drawing.SolidBrush $color
        $outline = New-Object System.Drawing.Pen $color, 14
        $outline.LineJoin = 'Round'
        $g.FillPolygon($fill, $speaker)
        $g.DrawPolygon($outline, $speaker)

        $pen = New-Object System.Drawing.Pen $color, $wavePen
        $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
        $radii = if ($small) { @(48) } else { @(42, 78) }
        foreach ($r in $radii) {
            $g.DrawArc($pen, 134 - $r, 128 - $r, 2 * $r, 2 * $r, -48, 96)
        }
        $g.Restore($state)
    }

    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = foreach ($s in $sizes) {
    $bmp = Render $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , @($s, $ms.ToArray())
}

# ICO container with PNG-compressed entries (supported since Windows Vista)
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $s = $img[0]; $data = $img[1]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$data.Length); $w.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($img in $images) { $w.Write([byte[]]$img[1]) }
$w.Flush()
[System.IO.File]::WriteAllBytes($OutIco, $out.ToArray())

# Preview sheet: every size on a light and a dark background
$sheet = New-Object System.Drawing.Bitmap 760, 300
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(243, 243, 243))), 0, 0, 760, 150)
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(32, 32, 32))), 0, 150, 760, 150)
$x = 10
foreach ($img in $images) {
    $bmp = [System.Drawing.Image]::FromStream((New-Object System.IO.MemoryStream (, $img[1])))
    $s = [Math]::Min($img[0], 130)
    $g.DrawImage($bmp, $x, 10, $s, $s)
    $g.DrawImage($bmp, $x, 160, $s, $s)
    $x += $s + 12
}
$sheet.Save($OutPreview, [System.Drawing.Imaging.ImageFormat]::Png)
"wrote $OutIco ($((Get-Item $OutIco).Length) bytes, $($images.Count) sizes)"
