Add-Type -AssemblyName System.Drawing
$sizes = @(16,20,24,32,48,64,128,256)
$frames = @()
foreach ($size in $sizes) {
    $bitmap = [Drawing.Bitmap]::new($size,$size)
    $g = [Drawing.Graphics]::FromImage($bitmap)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform($size/64.0,$size/64.0)
    $navy = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(23,37,61))
    $blue = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(80,179,247))
    $mint = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(99,232,191))
    $white = [Drawing.Pen]::new([Drawing.Color]::FromArgb(233,246,255),3)
    $g.FillRectangle($navy,5,9,54,40)
    $g.FillEllipse($mint,40,16,9,9)
    $g.FillPolygon($blue,[Drawing.PointF[]]@([Drawing.PointF]::new(8,44),[Drawing.PointF]::new(25,24),[Drawing.PointF]::new(43,44)))
    $g.FillPolygon($mint,[Drawing.PointF[]]@([Drawing.PointF]::new(28,44),[Drawing.PointF]::new(43,30),[Drawing.PointF]::new(56,44)))
    $g.DrawRectangle($white,5,9,54,40)
    $g.DrawLine($white,32,50,32,56)
    $g.DrawLine($white,22,57,42,57)
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
    $frames += ,$stream.ToArray()
    $stream.Dispose(); $g.Dispose(); $bitmap.Dispose(); $navy.Dispose(); $blue.Dispose(); $mint.Dispose(); $white.Dispose()
}
$output = [IO.File]::Create((Join-Path $PSScriptRoot '..\WallpaperLight\Assets\WallpaperLight.ico'))
$writer = [IO.BinaryWriter]::new($output)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16*$sizes.Count
for ($i=0; $i -lt $sizes.Count; $i++) {
    $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
    $offset += $frames[$i].Length
}
foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
$writer.Dispose()
