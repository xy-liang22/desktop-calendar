# Offline SVG primitive renderer using the installed Windows WPF assemblies only.
# Generated runtime assets are intentional source resources, not planner images.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$projectRoot = Split-Path $PSScriptRoot -Parent
$assetRoot = Join-Path $projectRoot 'assets\branding'
$previewRoot = Join-Path $projectRoot 'artifacts\branding'
[IO.Directory]::CreateDirectory($previewRoot) | Out-Null
$settings = New-Object Xml.XmlReaderSettings
$settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
$reader = [Xml.XmlReader]::Create((Join-Path $assetRoot 'logo.svg'), $settings)
$svg = New-Object Xml.XmlDocument
try { $svg.Load($reader) } finally { $reader.Dispose() }
if ($svg.DocumentElement.GetAttribute('viewBox') -ne '0 0 256 256') { throw 'Unsupported SVG viewBox.' }
function Number($node, $name) {
    $value = $node.GetAttribute($name)
    if ($value -eq '') { return 0.0 }
    return [double]::Parse($value, [Globalization.CultureInfo]::InvariantCulture)
}
function Brush($value) {
    if ($value -eq '' -or $value -eq 'none') { return $null }
    return [Windows.Media.BrushConverter]::new().ConvertFromInvariantString($value)
}
function Draw-Svg($context, [double]$size, [double]$left, [double]$top) {
    $context.PushTransform([Windows.Media.TranslateTransform]::new($left, $top))
    $context.PushTransform([Windows.Media.ScaleTransform]::new($size / 256, $size / 256))
    foreach ($node in $svg.DocumentElement.ChildNodes) {
        if ($node.NodeType -ne [Xml.XmlNodeType]::Element) { continue }
        $pen = $null
        if ($node.HasAttribute('stroke')) {
            $pen = [Windows.Media.Pen]::new((Brush $node.GetAttribute('stroke')), (Number $node 'stroke-width'))
            if ($node.GetAttribute('stroke-linecap') -eq 'round') { $pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round' }
        }
        switch ($node.LocalName) {
            'rect' {
                $rect = [Windows.Rect]::new((Number $node 'x'), (Number $node 'y'), (Number $node 'width'), (Number $node 'height'))
                $radius = Number $node 'rx'
                $context.DrawRoundedRectangle((Brush $node.GetAttribute('fill')), $pen, $rect, $radius, $radius)
            }
            'line' {
                $context.DrawLine($pen, [Windows.Point]::new((Number $node 'x1'), (Number $node 'y1')), [Windows.Point]::new((Number $node 'x2'), (Number $node 'y2')))
            }
            default { throw "Unsupported SVG primitive: $($node.LocalName)" }
        }
    }
    $context.Pop(); $context.Pop()
}
function Png-Bytes($visual, [int]$width, [int]$height) {
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($width, $height, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    try { $encoder.Save($stream); return ,$stream.ToArray() } finally { $stream.Dispose() }
}
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @()
foreach ($size in $sizes) {
    $visual = [Windows.Media.DrawingVisual]::new(); $context = $visual.RenderOpen()
    Draw-Svg $context $size 0 0; $context.Close()
    [byte[]]$bytes = Png-Bytes $visual $size $size
    $frames += ,$bytes
    [IO.File]::WriteAllBytes((Join-Path $previewRoot "logo-$size.png"), $bytes)
    if ($size -eq 64) { [IO.File]::WriteAllBytes((Join-Path $assetRoot 'logo-64.png'), $bytes) }
}
$ico = [IO.MemoryStream]::new(); $writer = [IO.BinaryWriter]::new($ico)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    [uint32]$offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $side = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$side); $writer.Write([byte]$side); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write($offset); $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    [IO.File]::WriteAllBytes((Join-Path $assetRoot 'DesktopCalendar.ico'), $ico.ToArray())
} finally { $writer.Dispose(); $ico.Dispose() }
$preview = [Windows.Media.DrawingVisual]::new(); $draw = $preview.RenderOpen()
$draw.DrawRectangle((Brush '#111B26'), $null, [Windows.Rect]::new(0, 0, 760, 340))
function Caption($text, $x, $y, $fontSize) {
    $format = [Windows.Media.FormattedText]::new($text, [Globalization.CultureInfo]::InvariantCulture, [Windows.FlowDirection]::LeftToRight,
        [Windows.Media.Typeface]::new('Segoe UI'), $fontSize, (Brush '#C4D4DF'), 1.0)
    $draw.DrawText($format, [Windows.Point]::new($x, $y))
}
Caption 'Desktop Calendar / A' 24 8 18
Draw-Svg $draw 256 24 40; Caption '256 px' 116 306 13
$x = 340
foreach ($size in @(64, 48, 32, 16)) {
    Draw-Svg $draw $size $x (168 - $size / 2)
    Caption "$size px" ($x - 2) 220 13
    $x += 104
}
$draw.Close()
[IO.File]::WriteAllBytes((Join-Path $previewRoot 'logo-preview.png'), (Png-Bytes $preview 760 340))
Write-Output 'Generated SVG-derived ICO (16..256), runtime PNG64 and ignored size previews.'
