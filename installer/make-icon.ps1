$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-Icon {
    param([string]$Path, [int]$Sizes)

    $bmp = New-Object System.Drawing.Bitmap($Sizes, $Sizes, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = [float]$Sizes
    $pad = $s * 0.06
    $radius = $s * 0.20

    # Rounded square background (blue gradient)
    $rect = New-Object System.Drawing.RectangleF($pad, $pad, ($s - 2 * $pad), ($s - 2 * $pad))
    $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $gp.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $gp.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $gp.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $gp.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $gp.CloseFigure()

    $brushBg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 58, 123, 213),
        [System.Drawing.Color]::FromArgb(255, 32, 88, 175),
        45.0)
    $g.FillPath($brushBg, $gp)

    # Draw three "menu lines" (the shrinking menu)
    $lineX1 = $s * 0.28
    $lineW = $s * 0.44
    $lineH = $s * 0.075
    $lineGap = $s * 0.085
    $lineYStart = $s * 0.30
    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $dim = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(150, 255, 255, 255))

    # two solid lines
    for ($i = 0; $i -lt 2; $i++) {
        $y = $lineYStart + $i * ($lineH + $lineGap)
        $r = New-Object System.Drawing.RectangleF($lineX1, $y, $lineW, $lineH)
        $rr = New-Object System.Drawing.Drawing2D.GraphicsPath
        $dd = $lineH
        $rr.AddArc($r.X, $r.Y, $dd, $dd, 180, 90)
        $rr.AddArc($r.Right - $dd, $r.Y, $dd, $dd, 270, 90)
        $rr.AddArc($r.Right - $dd, $r.Bottom - $dd, $dd, $dd, 0, 90)
        $rr.AddArc($r.X, $r.Bottom - $dd, $dd, $dd, 90, 90)
        $rr.CloseFigure()
        $g.FillPath($white, $rr)
    }

    # third line dimmed + strikeout (hidden item)
    $y3 = $lineYStart + 2 * ($lineH + $lineGap)
    $r3 = New-Object System.Drawing.RectangleF($lineX1, $y3, $lineW, $lineH)
    $rr3 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $dd3 = $lineH
    $rr3.AddArc($r3.X, $r3.Y, $dd3, $dd3, 180, 90)
    $rr3.AddArc($r3.Right - $dd3, $r3.Y, $dd3, $dd3, 270, 90)
    $rr3.AddArc($r3.Right - $dd3, $r3.Bottom - $dd3, $dd3, $dd3, 0, 90)
    $rr3.AddArc($r3.X, $r3.Bottom - $dd3, $dd3, $dd3, 90, 90)
    $rr3.CloseFigure()
    $g.FillPath($dim, $rr3)

    # red strike-through
    $red = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 90, 90), [Math]::Max(2.0, $s * 0.035))
    $red.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $red.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($red, [float]($lineX1 - $s * 0.02), [float]($y3 + $lineH / 2), [float]($lineX1 + $lineW + $s * 0.02), [float]($y3 + $lineH / 2))

    $g.Dispose()

    # Save as PNG then build ICO from multiple sizes
    return $bmp
}

# Build a multi-resolution icon (16,32,48,64,128,256)
$sizes = @(16, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($sz in $sizes) {
    $bmp = New-Icon -Path "" -Sizes $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,($ms.ToArray())
    $bmp.Dispose()
}

$outPath = 'D:\shiyan\rightSlim\src\app.ico'
$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)            # reserved
$bw.Write([UInt16]1)            # type = icon
$bw.Write([UInt16]$sizes.Count) # count
# header entries (6 bytes each) + need offset
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $dim = 0
    if ($sz -ge 256) { $dim = 0 } else { $dim = $sz }
    $bw.Write([Byte]$dim)       # width (0 means 256)
    $bw.Write([Byte]$dim)       # height
    $bw.Write([Byte]0)          # palette
    $bw.Write([Byte]0)          # reserved
    $bw.Write([UInt16]1)        # planes
    $bw.Write([UInt16]32)       # bpp
    $bw.Write([UInt32]$pngs[$i].Length)
    $bw.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush(); $bw.Close(); $fs.Close()

Write-Host ("ICO created: " + $outPath + "  size=" + (Get-Item $outPath).Length)