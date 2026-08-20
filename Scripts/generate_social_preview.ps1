Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase

$width = 1280
$height = 640

$grid = New-Object System.Windows.Controls.Grid
$grid.Width = $width
$grid.Height = $height

# Background Brush
$bg = New-Object System.Windows.Media.LinearGradientBrush
$bg.StartPoint = New-Object System.Windows.Point(0, 0)
$bg.EndPoint = New-Object System.Windows.Point(1, 1)
$bg.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.ColorConverter]::ConvertFromString("#0b1218"), 0.0)))
$bg.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.ColorConverter]::ConvertFromString("#131e27"), 0.5)))
$bg.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.ColorConverter]::ConvertFromString("#091016"), 1.0)))
$grid.Background = $bg

# Ambient Glow Circles
$glow1 = New-Object System.Windows.Shapes.Ellipse
$glow1.Width = 500
$glow1.Height = 500
$g1Brush = New-Object System.Windows.Media.RadialGradientBrush
$g1Brush.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.ColorConverter]::ConvertFromString("#2025d366"), 0.0)))
$g1Brush.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.ColorConverter]::ConvertFromString("#0025d366"), 1.0)))
$glow1.Fill = $g1Brush
$glow1.Margin = New-Object System.Windows.Thickness(-150, -100, 0, 0)
$glow1.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Left
$glow1.VerticalAlignment = [System.Windows.VerticalAlignment]::Top
[void]$grid.Children.Add($glow1)

$glow2 = New-Object System.Windows.Shapes.Ellipse
$glow2.Width = 600
$glow2.Height = 600
$g2Brush = New-Object System.Windows.Media.RadialGradientBrush
$g2Brush.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.ColorConverter]::ConvertFromString("#2524a1de"), 0.0)))
$g2Brush.GradientStops.Add((New-Object System.Windows.Media.GradientStop([System.Windows.Media.ColorConverter]::ConvertFromString("#0024a1de"), 1.0)))
$glow2.Fill = $g2Brush
$glow2.Margin = New-Object System.Windows.Thickness(0, 0, -100, -150)
$glow2.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
$glow2.VerticalAlignment = [System.Windows.VerticalAlignment]::Bottom
[void]$grid.Children.Add($glow2)

# Content Container (2 Columns: Left info, Right UI card)
$mainCols = New-Object System.Windows.Controls.Grid
$col1 = New-Object System.Windows.Controls.ColumnDefinition
$col1.Width = New-Object System.Windows.GridLength(640)
$col2 = New-Object System.Windows.Controls.ColumnDefinition
$col2.Width = New-Object System.Windows.GridLength(1, [System.Windows.GridUnitType]::Star)
[void]$mainCols.ColumnDefinitions.Add($col1)
[void]$mainCols.ColumnDefinitions.Add($col2)
$mainCols.Margin = New-Object System.Windows.Thickness(60, 40, 60, 40)
[void]$grid.Children.Add($mainCols)

# Left StackPanel
$leftStack = New-Object System.Windows.Controls.StackPanel
$leftStack.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
[System.Windows.Controls.Grid]::SetColumn($leftStack, 0)
[void]$mainCols.Children.Add($leftStack)

# Top Brand / Icon Row
$brandRow = New-Object System.Windows.Controls.StackPanel
$brandRow.Orientation = [System.Windows.Controls.Orientation]::Horizontal
$brandRow.Margin = New-Object System.Windows.Thickness(0, 0, 0, 16)

# App Icon container
$iconBorder = New-Object System.Windows.Controls.Border
$iconBorder.Width = 68
$iconBorder.Height = 68
$iconBorder.CornerRadius = New-Object System.Windows.CornerRadius(18)
$iconBorder.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#1f2c34")))
$iconBorder.BorderBrush = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#2a3e49")))
$iconBorder.BorderThickness = New-Object System.Windows.Thickness(1.5)
$iconShadow = New-Object System.Windows.Media.Effects.DropShadowEffect
$iconShadow.BlurRadius = 20
$iconShadow.ShadowDepth = 4
$iconShadow.Opacity = 0.5
$iconShadow.Color = [System.Windows.Media.ColorConverter]::ConvertFromString("#00a884")
$iconBorder.Effect = $iconShadow

$iconImg = New-Object System.Windows.Controls.Image
$iconImg.Source = New-Object System.Windows.Media.Imaging.BitmapImage(New-Object System.Uri((Resolve-Path "images\icon.png").Path))
$iconImg.Margin = New-Object System.Windows.Thickness(8)
$iconBorder.Child = $iconImg
[void]$brandRow.Children.Add($iconBorder)

$titleText = New-Object System.Windows.Controls.TextBlock
$titleText.Text = "HidaChat"
$titleText.FontSize = 52
$titleText.FontWeight = [System.Windows.FontWeights]::Bold
$titleText.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#ffffff")))
$titleText.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
$titleText.Margin = New-Object System.Windows.Thickness(18, 0, 0, 0)
[void]$brandRow.Children.Add($titleText)

# Version badge
$vBadge = New-Object System.Windows.Controls.Border
$vBadge.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#1b382b")))
$vBadge.BorderBrush = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#25d366")))
$vBadge.BorderThickness = New-Object System.Windows.Thickness(1)
$vBadge.CornerRadius = New-Object System.Windows.CornerRadius(12)
$vBadge.Padding = New-Object System.Windows.Thickness(10, 4, 10, 4)
$vBadge.Margin = New-Object System.Windows.Thickness(14, 0, 0, 0)
$vBadge.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
$vText = New-Object System.Windows.Controls.TextBlock
$vText.Text = "v0.5.1"
$vText.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#25d366")))
$vText.FontSize = 14
$vText.FontWeight = [System.Windows.FontWeights]::SemiBold
$vBadge.Child = $vText
[void]$brandRow.Children.Add($vBadge)

[void]$leftStack.Children.Add($brandRow)

# Headline
$headline = New-Object System.Windows.Controls.TextBlock
$headline.Text = "Portable Multi-Account Client for WhatsApp & Telegram"
$headline.FontSize = 23
$headline.FontWeight = [System.Windows.FontWeights]::SemiBold
$headline.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#e2e8f0")))
$headline.TextWrapping = [System.Windows.TextWrapping]::Wrap
$headline.Margin = New-Object System.Windows.Thickness(0, 0, 20, 14)
[void]$leftStack.Children.Add($headline)

$subDesc = New-Object System.Windows.Controls.TextBlock
$subDesc.Text = "High-performance .NET 9 & WebView2 wrapper with instant preloading, zero installation, and built-in message translation."
$subDesc.FontSize = 14.5
$subDesc.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#94a3b8")))
$subDesc.TextWrapping = [System.Windows.TextWrapping]::Wrap
$subDesc.Margin = New-Object System.Windows.Thickness(0, 0, 30, 20)
[void]$leftStack.Children.Add($subDesc)

# Feature Badges
$badges = New-Object System.Windows.Controls.WrapPanel
$badges.Margin = New-Object System.Windows.Thickness(0, 0, 0, 0)

$featureList = @(
    @("WhatsApp & Telegram", "#25d366", "#143324"),
    @("100% Portable (USB)", "#38bdf8", "#0f2d3d"),
    @("Instant Translation", "#a78bfa", "#2a1b4d"),
    @("Zero Reload Lag", "#fbbf24", "#3d2d0f"),
    @("Open Source", "#34d399", "#113324")
)

foreach ($f in $featureList) {
    $b = New-Object System.Windows.Controls.Border
    $b.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString($f[2])))
    $b.BorderBrush = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString($f[1])))
    $b.BorderThickness = New-Object System.Windows.Thickness(1)
    $b.CornerRadius = New-Object System.Windows.CornerRadius(14)
    $b.Padding = New-Object System.Windows.Thickness(12, 6, 12, 6)
    $b.Margin = New-Object System.Windows.Thickness(0, 0, 10, 10)

    $t = New-Object System.Windows.Controls.TextBlock
    $t.Text = $f[0]
    $t.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString($f[1])))
    $t.FontSize = 13
    $t.FontWeight = [System.Windows.FontWeights]::SemiBold
    $b.Child = $t
    [void]$badges.Children.Add($b)
}
[void]$leftStack.Children.Add($badges)

# Right Floating UI Preview Card
$rightCard = New-Object System.Windows.Controls.Border
[System.Windows.Controls.Grid]::SetColumn($rightCard, 1)
$rightCard.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#111b21")))
$rightCard.BorderBrush = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#202c33")))
$rightCard.BorderThickness = New-Object System.Windows.Thickness(1.5)
$rightCard.CornerRadius = New-Object System.Windows.CornerRadius(16)
$rightCard.Height = 440
$rightCard.VerticalAlignment = [System.Windows.VerticalAlignment]::Center

$rcShadow = New-Object System.Windows.Media.Effects.DropShadowEffect
$rcShadow.BlurRadius = 35
$rcShadow.ShadowDepth = 8
$rcShadow.Opacity = 0.6
$rcShadow.Color = [System.Windows.Media.ColorConverter]::ConvertFromString("#000000")
$rightCard.Effect = $rcShadow

# Card Inner Layout
$cardGrid = New-Object System.Windows.Controls.Grid
$cardRow1 = New-Object System.Windows.Controls.RowDefinition
$cardRow1.Height = New-Object System.Windows.GridLength(44)
$cardRow2 = New-Object System.Windows.Controls.RowDefinition
$cardRow2.Height = New-Object System.Windows.GridLength(1, [System.Windows.GridUnitType]::Star)
[void]$cardGrid.RowDefinitions.Add($cardRow1)
[void]$cardGrid.RowDefinitions.Add($cardRow2)
$rightCard.Child = $cardGrid

# Window Header
$cardHeader = New-Object System.Windows.Controls.Border
$cardHeader.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#1f2c34")))
$cardHeader.CornerRadius = New-Object System.Windows.CornerRadius(16, 16, 0, 0)
[System.Windows.Controls.Grid]::SetRow($cardHeader, 0)
[void]$cardGrid.Children.Add($cardHeader)

$hdrStack = New-Object System.Windows.Controls.StackPanel
$hdrStack.Orientation = [System.Windows.Controls.Orientation]::Horizontal
$hdrStack.Margin = New-Object System.Windows.Thickness(12, 0, 12, 0)
$cardHeader.Child = $hdrStack

# Tab 1: WhatsApp (Active)
$tab1 = New-Object System.Windows.Controls.Border
$tab1.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#111b21")))
$tab1.BorderBrush = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#25d366")))
$tab1.BorderThickness = New-Object System.Windows.Thickness(0, 0, 0, 2.5)
$tab1.Padding = New-Object System.Windows.Thickness(12, 8, 12, 6)
$tab1.Margin = New-Object System.Windows.Thickness(0, 4, 6, 0)
$t1Text = New-Object System.Windows.Controls.TextBlock
$t1Text.Text = "WhatsApp (Work)"
$t1Text.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#25d366")))
$t1Text.FontSize = 12
$t1Text.FontWeight = [System.Windows.FontWeights]::SemiBold
$tab1.Child = $t1Text
[void]$hdrStack.Children.Add($tab1)

# Tab 2: Telegram
$tab2 = New-Object System.Windows.Controls.Border
$tab2.Background = [System.Windows.Media.Brushes]::Transparent
$tab2.Padding = New-Object System.Windows.Thickness(12, 8, 12, 6)
$tab2.Margin = New-Object System.Windows.Thickness(0, 4, 6, 0)
$t2Text = New-Object System.Windows.Controls.TextBlock
$t2Text.Text = "Telegram"
$t2Text.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#8696a0")))
$t2Text.FontSize = 12
$tab2.Child = $t2Text
[void]$hdrStack.Children.Add($tab2)

# Tab 3: WhatsApp Personal
$tab3 = New-Object System.Windows.Controls.Border
$tab3.Background = [System.Windows.Media.Brushes]::Transparent
$tab3.Padding = New-Object System.Windows.Thickness(12, 8, 12, 6)
$tab3.Margin = New-Object System.Windows.Thickness(0, 4, 6, 0)
$t3Text = New-Object System.Windows.Controls.TextBlock
$t3Text.Text = "WhatsApp 2"
$t3Text.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#8696a0")))
$t3Text.FontSize = 12
$tab3.Child = $t3Text
[void]$hdrStack.Children.Add($tab3)

# Card Body (Chat Mockup)
$cardBody = New-Object System.Windows.Controls.StackPanel
$cardBody.Margin = New-Object System.Windows.Thickness(18, 18, 18, 18)
[System.Windows.Controls.Grid]::SetRow($cardBody, 1)
[void]$cardGrid.Children.Add($cardBody)

# Chat Message 1 (Incoming)
$msg1 = New-Object System.Windows.Controls.Border
$msg1.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#202c33")))
$msg1.CornerRadius = New-Object System.Windows.CornerRadius(8)
$msg1.Padding = New-Object System.Windows.Thickness(12, 10, 12, 10)
$msg1.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Left
$msg1.MaxWidth = 360
$msg1.Margin = New-Object System.Windows.Thickness(0, 0, 0, 12)

$m1Stack = New-Object System.Windows.Controls.StackPanel
$m1Sender = New-Object System.Windows.Controls.TextBlock
$m1Sender.Text = "Alexander (Berlin)"
$m1Sender.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#53bdeb")))
$m1Sender.FontSize = 11
$m1Sender.FontWeight = [System.Windows.FontWeights]::SemiBold
$m1Body = New-Object System.Windows.Controls.TextBlock
$m1Body.Text = "Guten Morgen! Hast du den neuen Release-Bericht erhalten?"
$m1Body.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#e9edef")))
$m1Body.FontSize = 12.5
$m1Body.TextWrapping = [System.Windows.TextWrapping]::Wrap
$m1Body.Margin = New-Object System.Windows.Thickness(0, 3, 0, 0)
[void]$m1Stack.Children.Add($m1Sender)
[void]$m1Stack.Children.Add($m1Body)
$msg1.Child = $m1Stack
[void]$cardBody.Children.Add($msg1)

# Translation Box
$transBox = New-Object System.Windows.Controls.Border
$transBox.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#172a3a")))
$transBox.BorderBrush = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#00a884")))
$transBox.BorderThickness = New-Object System.Windows.Thickness(2, 0, 0, 0)
$transBox.Padding = New-Object System.Windows.Thickness(10, 6, 10, 6)
$transBox.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Left
$transBox.MaxWidth = 360
$transBox.Margin = New-Object System.Windows.Thickness(12, -6, 0, 16)

$tbText = New-Object System.Windows.Controls.TextBlock
$tbText.Text = "Translate: Buongiorno! Hai ricevuto il report?"
$tbText.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#25d366")))
$tbText.FontSize = 11.5
$tbText.TextWrapping = [System.Windows.TextWrapping]::Wrap
$transBox.Child = $tbText
[void]$cardBody.Children.Add($transBox)

# Chat Message 2 (Outgoing)
$msg2 = New-Object System.Windows.Controls.Border
$msg2.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#005c4b")))
$msg2.CornerRadius = New-Object System.Windows.CornerRadius(8)
$msg2.Padding = New-Object System.Windows.Thickness(12, 10, 12, 10)
$msg2.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
$msg2.MaxWidth = 360
$msg2.Margin = New-Object System.Windows.Thickness(0, 0, 0, 12)

$m2Body = New-Object System.Windows.Controls.TextBlock
$m2Body.Text = "Yes! HidaChat v0.5.1 is now live on winget"
$m2Body.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#e9edef")))
$m2Body.FontSize = 12.5
$m2Body.TextWrapping = [System.Windows.TextWrapping]::Wrap
$msg2.Child = $m2Body
[void]$cardBody.Children.Add($msg2)

[void]$mainCols.Children.Add($rightCard)

# Measure and Arrange
$size = New-Object System.Windows.Size($width, $height)
$grid.Measure($size)
$rect = New-Object System.Windows.Rect(0, 0, $width, $height)
$grid.Arrange($rect)
$grid.UpdateLayout()

# Render to PNG
$renderTarget = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($width, $height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
$renderTarget.Render($grid)

$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($renderTarget))

$outPath = Join-Path (Get-Location) "images\social_preview.png"
$fileStream = New-Object System.IO.FileStream($outPath, [System.IO.FileMode]::Create)
$encoder.Save($fileStream)
$fileStream.Close()

Write-Host "Social preview banner generated successfully: $outPath"
