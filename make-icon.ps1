# Génère icon.ico : un « ¶ » doré veillé par une petite étoile-nounou,
# style famille Stargazer. Aucun asset externe requis.
Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# --- carré arrondi, dégradé bleu nuit ---
$margin = 10
$rect = New-Object System.Drawing.Rectangle($margin, $margin, ($size - 2*$margin), ($size - 2*$margin))
$radius = 52; $d = $radius * 2
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
$path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
$path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
$path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
$path.CloseFigure()
$c1 = [System.Drawing.Color]::FromArgb(255, 26, 34, 74)
$c2 = [System.Drawing.Color]::FromArgb(255, 8, 11, 30)
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush($rect, $c1, $c2, 90)
$g.FillPath($bg, $path)

# --- pied-de-mouche « ¶ » doré, le glyphe du typographe ---
$fontPied = New-Object System.Drawing.Font("Georgia", 128, [System.Drawing.FontStyle]::Bold)
$gold = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 224, 186, 69))
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
$g.DrawString([string][char]0x00B6, $fontPied, $gold,
    (New-Object System.Drawing.RectangleF(6, 14, 256, 236)), $fmt)
$fontPied.Dispose()

# --- guillemets français « » en or clair, de part et d'autre ---
$fontG = New-Object System.Drawing.Font("Georgia", 44, [System.Drawing.FontStyle]::Bold)
$light = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 244, 215, 122))
$g.DrawString([string][char]0x00AB, $fontG, $light, 22, 96)
$g.DrawString([string][char]0x00BB, $fontG, $light, 176, 96)
$fontG.Dispose()

# --- étoile signature (haut droit) ---
function Star-Path($cx, $cy, $r, $w) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddPolygon(@(
        (New-Object System.Drawing.Point($cx, ($cy - $r))),
        (New-Object System.Drawing.Point(($cx + $w), ($cy - $w))),
        (New-Object System.Drawing.Point(($cx + $r), $cy)),
        (New-Object System.Drawing.Point(($cx + $w), ($cy + $w))),
        (New-Object System.Drawing.Point($cx, ($cy + $r))),
        (New-Object System.Drawing.Point(($cx - $w), ($cy + $w))),
        (New-Object System.Drawing.Point(($cx - $r), $cy)),
        (New-Object System.Drawing.Point(($cx - $w), ($cy - $w)))
    ))
    return $p
}
$g.FillPath($light, (Star-Path 200 54 20 4.5))

$g.Dispose()

# --- décliner en plusieurs tailles, empaquetées en ICO (entrées PNG) ---
$sizes = @(256, 128, 64, 48, 32, 24, 16)
$pngs = @()
foreach ($s in $sizes) {
    if ($s -eq 256) { $b = $bmp } else {
        $b = New-Object System.Drawing.Bitmap($s, $s)
        $gg = [System.Drawing.Graphics]::FromImage($b)
        $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $gg.DrawImage($bmp, 0, 0, $s, $s)
        $gg.Dispose()
    }
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,@($s, $ms.ToArray())
    $ms.Dispose()
    if ($s -ne 256) { $b.Dispose() }
}
$bmp.Dispose()

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($e in $pngs) {
    $s = $e[0]; $data = $e[1]
    $dim = if ($s -eq 256) { 0 } else { $s }
    $bw.Write([Byte]$dim); $bw.Write([Byte]$dim); $bw.Write([Byte]0); $bw.Write([Byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($e in $pngs) { $bw.Write($e[1]) }
$bw.Flush()

$iconPath = Join-Path $PSScriptRoot "icon.ico"
[System.IO.File]::WriteAllBytes($iconPath, $out.ToArray())
$out.Dispose()
Write-Output "OK -> $iconPath"
