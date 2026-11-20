[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-RoundedPath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

$iconsDirectory = Join-Path (Split-Path $PSScriptRoot -Parent) "icons"
New-Item -ItemType Directory -Path $iconsDirectory -Force | Out-Null

foreach ($size in @(16, 48, 128)) {
    $scale = $size / 128.0
    $bitmap = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $backgroundBounds = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $backgroundBounds,
        [System.Drawing.Color]::FromArgb(255, 79, 118, 255),
        [System.Drawing.Color]::FromArgb(255, 37, 67, 204),
        45
    )
    $backgroundPath = New-RoundedPath (2 * $scale) (2 * $scale) (124 * $scale) (124 * $scale) (28 * $scale)
    $graphics.FillPath($backgroundBrush, $backgroundPath)

    $folderPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $folderPath.AddLine(24 * $scale, 46 * $scale, 50 * $scale, 46 * $scale)
    $folderPath.AddLine(58 * $scale, 56 * $scale, 104 * $scale, 56 * $scale)
    $folderPath.AddLine(104 * $scale, 97 * $scale, 24 * $scale, 97 * $scale)
    $folderPath.CloseFigure()
    $folderBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(244, 255, 255, 255))
    $graphics.FillPath($folderBrush, $folderPath)

    $folderTabPath = New-RoundedPath (24 * $scale) (35 * $scale) (45 * $scale) (17 * $scale) (4 * $scale)
    $graphics.FillPath($folderBrush, $folderTabPath)

    $accentBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 79, 118, 255))
    $graphics.FillEllipse($accentBrush, 65 * $scale, 62 * $scale, 39 * $scale, 39 * $scale)

    $lensPen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, (7 * $scale))
    $lensPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $lensPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawEllipse($lensPen, 69 * $scale, 66 * $scale, 27 * $scale, 27 * $scale)
    $graphics.DrawLine($lensPen, 92 * $scale, 89 * $scale, 104 * $scale, 101 * $scale)

    $outputPath = Join-Path $iconsDirectory "icon$size.png"
    $bitmap.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)

    $lensPen.Dispose()
    $accentBrush.Dispose()
    $folderBrush.Dispose()
    $folderPath.Dispose()
    $folderTabPath.Dispose()
    $backgroundPath.Dispose()
    $backgroundBrush.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Host "扩展图标已生成：$iconsDirectory" -ForegroundColor Green
