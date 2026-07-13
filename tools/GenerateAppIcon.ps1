param(
    [string]$OutputPath = "src/AutoPowerRunner/Assets/AppIcon.ico"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

function New-ShieldBitmap {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmap.SetResolution(96, 96)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $blue = [System.Drawing.Color]::FromArgb(0x0D, 0x73, 0xE8)
        $blueBrush = [System.Drawing.SolidBrush]::new($blue)
        $whiteBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
        $bluePen = [System.Drawing.Pen]::new($blue, [Math]::Max(1.0, $Size * 0.075))
        $bluePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $bluePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $bluePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

        try {
            $scale = $Size / 64.0

            function P([double]$x, [double]$y) {
                [System.Drawing.PointF]::new([float]($x * $scale), [float]($y * $scale))
            }

            $outer = @(
                (P 32 3), (P 56 13), (P 56 29),
                (P 56 45), (P 45 56), (P 32 61),
                (P 19 56), (P 8 45), (P 8 29), (P 8 13)
            )
            $inner = @(
                (P 32 10), (P 49 18), (P 49 30),
                (P 49 42), (P 41 49), (P 32 54),
                (P 23 49), (P 15 42), (P 15 30), (P 15 18)
            )
            $check = @(
                (P 17 34), (P 28 46), (P 48 24), (P 44 20), (P 28 38), (P 20 30)
            )

            $graphics.FillPolygon($blueBrush, $outer)
            $graphics.FillPolygon($whiteBrush, $inner)
            $graphics.FillPolygon($blueBrush, $check)

            return $bitmap.Clone()
        }
        finally {
            $bluePen.Dispose()
            $blueBrush.Dispose()
            $whiteBrush.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function ConvertTo-IcoDibBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $width = $Bitmap.Width
    $height = $Bitmap.Height
    $stride = $width * 4
    $xorSize = $stride * $height
    $andStride = [Math]::Floor(($width + 31) / 32) * 4
    $andSize = $andStride * $height

    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)

    try {
        $writer.Write([UInt32]40)
        $writer.Write([Int32]$width)
        $writer.Write([Int32]($height * 2))
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]$xorSize)
        $writer.Write([Int32]0)
        $writer.Write([Int32]0)
        $writer.Write([UInt32]0)
        $writer.Write([UInt32]0)

        for ($y = $height - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $width; $x++) {
                $pixel = $Bitmap.GetPixel($x, $y)
                $writer.Write([byte]$pixel.B)
                $writer.Write([byte]$pixel.G)
                $writer.Write([byte]$pixel.R)
                $writer.Write([byte]$pixel.A)
            }
        }

        $writer.Write([byte[]]::new($andSize))
        return ,$stream.ToArray()
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = $sizes | ForEach-Object {
    $bitmap = New-ShieldBitmap -Size $_
    try {
        $bytes = ConvertTo-IcoDibBytes -Bitmap $bitmap
    }
    finally {
        $bitmap.Dispose()
    }

    [pscustomobject]@{
        Size = $_
        Bytes = [byte[]]$bytes
    }
}

$fullOutputPath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputPath))
$outputDirectory = Split-Path -Parent $fullOutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null

$file = [System.IO.File]::Create($fullOutputPath)
$writer = [System.IO.BinaryWriter]::new($file)

try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$images.Count)

    $offset = 6 + (16 * $images.Count)
    foreach ($image in $images) {
        $dimensionByte = if ($image.Size -eq 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimensionByte)
        $writer.Write([byte]$dimensionByte)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]32)
        $writer.Write([UInt32]$image.Bytes.Length)
        $writer.Write([UInt32]$offset)
        $offset += $image.Bytes.Length
    }

    foreach ($image in $images) {
        $writer.Write([byte[]]$image.Bytes)
    }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}

Write-Output $fullOutputPath
