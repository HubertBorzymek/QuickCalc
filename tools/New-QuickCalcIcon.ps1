# Generuje src\QuickCalc.App\QuickCalc.ico (wiele rozmiarow PNG w jednym pliku ICO).
param([string]$OutFile = (Join-Path $PSScriptRoot '..\src\QuickCalc.App\QuickCalc.ico'),
      [string]$PreviewFile)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedPath([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Min($r * 2, [Math]::Min($w, $h))
    if ($d -le 0) { $path.AddRectangle((New-Object System.Drawing.RectangleF $x, $y, $w, $h)); return $path }
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Tlo: zaokraglony kwadrat z gradientem niebiesko-fioletowym.
    $s = [single]$size
    $body = New-RoundedPath 0 0 $s $s ($s * 0.22)
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
        (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF $s, $s),
        [System.Drawing.Color]::FromArgb(59, 130, 246), [System.Drawing.Color]::FromArgb(124, 58, 237))
    $g.FillPath($gradient, $body)

    # Wyswietlacz u gory i siatka 2x2 klawiszy; geometria wyrownana do pikseli.
    $pad = [Math]::Max(2, [int][Math]::Round($s * 0.19))
    $gap = [Math]::Max(1, [int][Math]::Round($s * 0.07))
    $inner = $size - 2 * $pad
    $displayH = [Math]::Max(3, [int][Math]::Round($inner * 0.30))
    $keyW = [int][Math]::Floor(($inner - $gap) / 2)
    $keyH = [int][Math]::Floor(($inner - $displayH - 2 * $gap) / 2)
    $radius = [single]([Math]::Max(0, $s * 0.05))
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(245, 247, 250))
    $soft = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(205, 255, 255, 255))
    $accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(251, 146, 60))

    $display = New-RoundedPath $pad $pad $inner $displayH $radius
    $g.FillPath($white, $display)
    $top = $pad + $displayH + $gap
    $right = $pad + $inner - $keyW
    foreach ($cell in @(@($pad, $top, $soft), @($right, $top, $soft),
                        @($pad, ($top + $keyH + $gap), $soft), @($right, ($top + $keyH + $gap), $accent))) {
        $p = New-RoundedPath $cell[0] $cell[1] $keyW $keyH $radius
        $g.FillPath($cell[2], $p)
    }
    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($size in $sizes) {
    $bmp = New-IconBitmap $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
[System.IO.File]::WriteAllBytes([System.IO.Path]::GetFullPath($OutFile), $out.ToArray())
Write-Host "Zapisano: $([System.IO.Path]::GetFullPath($OutFile))"

if ($PreviewFile) {
    $preview = New-Object System.Drawing.Bitmap 520, 280
    $g = [System.Drawing.Graphics]::FromImage($preview)
    $g.InterpolationMode = 'NearestNeighbor'
    $g.Clear([System.Drawing.Color]::FromArgb(32, 32, 32))
    $x = 10
    foreach ($size in 16, 24, 32, 48) {
        $bmp = New-IconBitmap $size
        $g.DrawImage($bmp, $x, 10, $size, $size)
        $g.DrawImage($bmp, $x, 70, $size * 4, $size * 4)
        $x += [Math]::Max(60, $size * 4 + 10)
        $bmp.Dispose()
    }
    $big = New-IconBitmap 256
    $g.DrawImage($big, 400, 150, 110, 110)
    $g.Dispose()
    $preview.Save($PreviewFile, [System.Drawing.Imaging.ImageFormat]::Png)
}
