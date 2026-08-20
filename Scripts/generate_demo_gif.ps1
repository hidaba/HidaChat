Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Drawing

Add-Type -TypeDefinition @"
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;

public class GifMaker {
    public static void SaveGif(Bitmap[] frames, int[] delaysMs, string outPath) {
        using (var fs = new FileStream(outPath, FileMode.Create))
        using (var bw = new BinaryWriter(fs)) {
            for (int i = 0; i < frames.Length; i++) {
                using (var ms = new MemoryStream()) {
                    frames[i].Save(ms, ImageFormat.Gif);
                    byte[] bytes = ms.ToArray();
                    if (i == 0) {
                        // Header & Logical Screen Descriptor (13 bytes)
                        bw.Write(bytes, 0, 13);
                        // Global Color Table
                        bool hasGct = (bytes[10] & 0x80) != 0;
                        if (hasGct) {
                            int gctSize = 3 * (1 << ((bytes[10] & 0x07) + 1));
                            bw.Write(bytes, 13, gctSize);
                        }
                        // Netscape 2.0 Loop Extension
                        bw.Write(new byte[] { 0x21, 0xFF, 0x0B });
                        bw.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                        bw.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });
                    }
                    
                    int delayCenti = delaysMs[i] / 10;
                    byte delayLo = (byte)(delayCenti & 0xFF);
                    byte delayHi = (byte)((delayCenti >> 8) & 0xFF);
                    
                    bool gceWritten = false;
                    for (int j = 13; j < bytes.Length - 4; j++) {
                        if (bytes[j] == 0x21 && bytes[j+1] == 0xF9 && bytes[j+2] == 0x04) {
                            bytes[j+4] = delayLo;
                            bytes[j+5] = delayHi;
                            int len = bytes.Length - j;
                            if (bytes[bytes.Length - 1] == 0x3B) len--;
                            bw.Write(bytes, j, len);
                            gceWritten = true;
                            break;
                        }
                    }
                    if (!gceWritten) {
                        bw.Write(new byte[] { 0x21, 0xF9, 0x04, 0x00, delayLo, delayHi, 0x00, 0x00 });
                        for (int j = 13; j < bytes.Length; j++) {
                            if (bytes[j] == 0x2C) {
                                int len = bytes.Length - j;
                                if (bytes[bytes.Length - 1] == 0x3B) len--;
                                bw.Write(bytes, j, len);
                                break;
                            }
                        }
                    }
                }
            }
            bw.Write((byte)0x3B);
        }
    }
}
"@ -ReferencedAssemblies "System.Drawing"

$width = 960
$height = 540

function Render-Scene {
    param(
        [string]$ActiveTab = "WhatsApp",
        [bool]$ShowHoverBtn = $false,
        [bool]$ShowTranslation = $false,
        [int]$CursorX = -100,
        [int]$CursorY = -100,
        [bool]$ShowCursor = $false
    )

    $root = New-Object System.Windows.Controls.Grid
    $root.Width = $width
    $root.Height = $height
    $root.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#0b141a")))

    # Window Chrome
    $rowTop = New-Object System.Windows.Controls.RowDefinition; $rowTop.Height = New-Object System.Windows.GridLength(44)
    $rowBody = New-Object System.Windows.Controls.RowDefinition; $rowBody.Height = New-Object System.Windows.GridLength(1, [System.Windows.GridUnitType]::Star)
    [void]$root.RowDefinitions.Add($rowTop)
    [void]$root.RowDefinitions.Add($rowBody)

    # 1. Top Header & Tabs Bar
    $header = New-Object System.Windows.Controls.Border
    $header.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#1f2c34")))
    [System.Windows.Controls.Grid]::SetRow($header, 0)
    [void]$root.Children.Add($header)

    $hdrGrid = New-Object System.Windows.Controls.Grid
    $header.Child = $hdrGrid

    $tabsStack = New-Object System.Windows.Controls.StackPanel
    $tabsStack.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    $tabsStack.Margin = New-Object System.Windows.Thickness(12, 0, 0, 0)
    $tabsStack.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
    [void]$hdrGrid.Children.Add($tabsStack)

    # App Branding Icon
    $bIcon = New-Object System.Windows.Controls.Border
    $bIcon.Width = 26; $bIcon.Height = 26; $bIcon.CornerRadius = New-Object System.Windows.CornerRadius(6)
    $bIcon.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#00a884")))
    $bIcon.Margin = New-Object System.Windows.Thickness(0, 0, 10, 0)
    $bText = New-Object System.Windows.Controls.TextBlock; $bText.Text = "H"; $bText.Foreground = [System.Windows.Media.Brushes]::White; $bText.FontWeight = [System.Windows.FontWeights]::Bold; $bText.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center; $bText.VerticalAlignment = [System.Windows.VerticalAlignment]::Center; $bText.FontSize = 14
    $bIcon.Child = $bText
    [void]$tabsStack.Children.Add($bIcon)

    # Tab 1: WhatsApp
    $t1 = New-Object System.Windows.Controls.Border
    $isT1Active = ($ActiveTab -eq "WhatsApp")
    if ($isT1Active) {
        $t1.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#111b21")))
        $t1.BorderThickness = New-Object System.Windows.Thickness(0, 0, 0, 3)
    } else {
        $t1.Background = [System.Windows.Media.Brushes]::Transparent
        $t1.BorderThickness = New-Object System.Windows.Thickness(0, 0, 0, 0)
    }
    $t1.BorderBrush = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#25d366")))
    $t1.Padding = New-Object System.Windows.Thickness(14, 8, 14, 6)
    $t1.Margin = New-Object System.Windows.Thickness(0, 0, 6, 0)
    $t1Text = New-Object System.Windows.Controls.TextBlock
    $t1Text.Text = "WhatsApp (Work)"
    if ($isT1Active) {
        $t1Text.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#25d366")))
        $t1Text.FontWeight = [System.Windows.FontWeights]::SemiBold
    } else {
        $t1Text.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#8696a0")))
        $t1Text.FontWeight = [System.Windows.FontWeights]::Normal
    }
    $t1Text.FontSize = 13
    $t1.Child = $t1Text
    [void]$tabsStack.Children.Add($t1)

    # Tab 2: Telegram
    $t2 = New-Object System.Windows.Controls.Border
    $isT2Active = ($ActiveTab -eq "Telegram")
    if ($isT2Active) {
        $t2.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#17212b")))
        $t2.BorderThickness = New-Object System.Windows.Thickness(0, 0, 0, 3)
    } else {
        $t2.Background = [System.Windows.Media.Brushes]::Transparent
        $t2.BorderThickness = New-Object System.Windows.Thickness(0, 0, 0, 0)
    }
    $t2.BorderBrush = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#24a1de")))
    $t2.Padding = New-Object System.Windows.Thickness(14, 8, 14, 6)
    $t2.Margin = New-Object System.Windows.Thickness(0, 0, 6, 0)
    $t2Text = New-Object System.Windows.Controls.TextBlock
    $t2Text.Text = "Telegram Web"
    if ($isT2Active) {
        $t2Text.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#24a1de")))
        $t2Text.FontWeight = [System.Windows.FontWeights]::SemiBold
    } else {
        $t2Text.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#8696a0")))
        $t2Text.FontWeight = [System.Windows.FontWeights]::Normal
    }
    $t2Text.FontSize = 13
    $t2.Child = $t2Text
    [void]$tabsStack.Children.Add($t2)

    # Add Tab Button (+)
    $tAdd = New-Object System.Windows.Controls.Border
    $tAdd.Width = 28; $tAdd.Height = 28; $tAdd.CornerRadius = New-Object System.Windows.CornerRadius(14)
    $tAdd.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#2a3942")))
    $tAdd.Margin = New-Object System.Windows.Thickness(4, 0, 0, 0)
    $tAddText = New-Object System.Windows.Controls.TextBlock; $tAddText.Text = "+"; $tAddText.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#e9edef"))); $tAddText.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center; $tAddText.VerticalAlignment = [System.Windows.VerticalAlignment]::Center; $tAddText.FontSize = 16
    $tAdd.Child = $tAddText
    [void]$tabsStack.Children.Add($tAdd)

    # Right TitleBar Actions
    $actStack = New-Object System.Windows.Controls.StackPanel
    $actStack.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    $actStack.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
    $actStack.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
    $actStack.Margin = New-Object System.Windows.Thickness(0, 0, 16, 0)

    $lblBrand = New-Object System.Windows.Controls.TextBlock
    $lblBrand.Text = "HidaChat v0.5.1"
    $lblBrand.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#8696a0")))
    $lblBrand.FontSize = 12
    $lblBrand.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
    $lblBrand.Margin = New-Object System.Windows.Thickness(0, 0, 16, 0)
    [void]$actStack.Children.Add($lblBrand)
    [void]$hdrGrid.Children.Add($actStack)

    # 2. Main Content Grid (Sidebar + Chat Area)
    $bodyGrid = New-Object System.Windows.Controls.Grid
    [System.Windows.Controls.Grid]::SetRow($bodyGrid, 1)
    [void]$root.Children.Add($bodyGrid)

    $colSide = New-Object System.Windows.Controls.ColumnDefinition; $colSide.Width = New-Object System.Windows.GridLength(300)
    $colChat = New-Object System.Windows.Controls.ColumnDefinition; $colChat.Width = New-Object System.Windows.GridLength(1, [System.Windows.GridUnitType]::Star)
    [void]$bodyGrid.ColumnDefinitions.Add($colSide)
    [void]$bodyGrid.ColumnDefinitions.Add($colChat)

    # SIDEBAR
    $sideBorder = New-Object System.Windows.Controls.Border
    $sideBorder.Background = if ($ActiveTab -eq "WhatsApp") { (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#111b21"))) } else { (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#17212b"))) }
    $sideBorder.BorderBrush = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#202c33")))
    $sideBorder.BorderThickness = New-Object System.Windows.Thickness(0, 0, 1, 0)
    [System.Windows.Controls.Grid]::SetColumn($sideBorder, 0)
    [void]$bodyGrid.Children.Add($sideBorder)

    $sideStack = New-Object System.Windows.Controls.StackPanel
    $sideBorder.Child = $sideStack

    # Sidebar Search
    $searchBorder = New-Object System.Windows.Controls.Border
    $searchBorder.Height = 36; $searchBorder.CornerRadius = New-Object System.Windows.CornerRadius(8)
    $searchBorder.Background = if ($ActiveTab -eq "WhatsApp") { (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#202c33"))) } else { (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#242f3d"))) }
    $searchBorder.Margin = New-Object System.Windows.Thickness(12, 12, 12, 10)
    $sText = New-Object System.Windows.Controls.TextBlock; $sText.Text = "Search or start new chat"; $sText.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#8696a0"))); $sText.VerticalAlignment = [System.Windows.VerticalAlignment]::Center; $sText.Margin = New-Object System.Windows.Thickness(10, 0, 0, 0); $sText.FontSize = 12
    $searchBorder.Child = $sText
    [void]$sideStack.Children.Add($searchBorder)

    # Chat Items in Sidebar
    $chats = if ($ActiveTab -eq "WhatsApp") {
        @(
            @("Alexander (Berlin)", "Guten Morgen! Hast du...", "09:42", $true, "#00a884"),
            @("Dev Team Global", "PR #420520 passed winget CI", "09:15", $false, "#53bdeb"),
            @("Marco Bianchi", "Ci vediamo per il meeting?", "Ieri", $false, "#a78bfa")
        )
    } else {
        @(
            @("HidaChat Community", "v0.5.1 is now live on winget!", "10:04", $true, "#24a1de"),
            @("Telegram News", "Major updates released today", "08:30", $false, "#24a1de"),
            @("GitHub Notifications", "Merged PR #420520", "08:12", $false, "#34d399")
        )
    }

    foreach ($c in $chats) {
        $cItem = New-Object System.Windows.Controls.Border
        if ($c[3]) {
            if ($ActiveTab -eq "WhatsApp") {
                $cItem.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#2a3942")))
            } else {
                $cItem.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#2b5278")))
            }
        } else {
            $cItem.Background = [System.Windows.Media.Brushes]::Transparent
        }
        $cItem.Padding = New-Object System.Windows.Thickness(12, 10, 12, 10)

        $cGrid = New-Object System.Windows.Controls.Grid
        $cgCol1 = New-Object System.Windows.Controls.ColumnDefinition; $cgCol1.Width = New-Object System.Windows.GridLength(44)
        $cgCol2 = New-Object System.Windows.Controls.ColumnDefinition; $cgCol2.Width = New-Object System.Windows.GridLength(1, [System.Windows.GridUnitType]::Star)
        $cgCol3 = New-Object System.Windows.Controls.ColumnDefinition; $cgCol3.Width = New-Object System.Windows.GridLength(44)
        [void]$cGrid.ColumnDefinitions.Add($cgCol1)
        [void]$cGrid.ColumnDefinitions.Add($cgCol2)
        [void]$cGrid.ColumnDefinitions.Add($cgCol3)

        # Avatar
        $av = New-Object System.Windows.Controls.Border
        $av.Width = 38; $av.Height = 38; $av.CornerRadius = New-Object System.Windows.CornerRadius(19)
        $av.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString($c[4])))
        $avText = New-Object System.Windows.Controls.TextBlock; $avText.Text = $c[0].Substring(0, 1); $avText.Foreground = [System.Windows.Media.Brushes]::White; $avText.FontWeight = [System.Windows.FontWeights]::Bold; $avText.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Center; $avText.VerticalAlignment = [System.Windows.VerticalAlignment]::Center; $avText.FontSize = 15
        $av.Child = $avText
        [System.Windows.Controls.Grid]::SetColumn($av, 0)
        [void]$cGrid.Children.Add($av)

        # Name & Last Message
        $ciStack = New-Object System.Windows.Controls.StackPanel
        $ciStack.Margin = New-Object System.Windows.Thickness(8, 0, 0, 0)
        $ciStack.VerticalAlignment = [System.Windows.VerticalAlignment]::Center
        $ciName = New-Object System.Windows.Controls.TextBlock; $ciName.Text = $c[0]; $ciName.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#e9edef"))); $ciName.FontWeight = [System.Windows.FontWeights]::SemiBold; $ciName.FontSize = 13
        $ciMsg = New-Object System.Windows.Controls.TextBlock; $ciMsg.Text = $c[1]; $ciMsg.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#8696a0"))); $ciMsg.FontSize = 11.5; $ciMsg.TextTrimming = [System.Windows.TextTrimming]::CharacterEllipsis
        [void]$ciStack.Children.Add($ciName)
        [void]$ciStack.Children.Add($ciMsg)
        [System.Windows.Controls.Grid]::SetColumn($ciStack, 1)
        [void]$cGrid.Children.Add($ciStack)

        # Time
        $ciTime = New-Object System.Windows.Controls.TextBlock; $ciTime.Text = $c[2]; $ciTime.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#8696a0"))); $ciTime.FontSize = 11; $ciTime.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
        [System.Windows.Controls.Grid]::SetColumn($ciTime, 2)
        [void]$cGrid.Children.Add($ciTime)

        $cItem.Child = $cGrid
        [void]$sideStack.Children.Add($cItem)
    }

    # CHAT AREA
    $chatBorder = New-Object System.Windows.Controls.Border
    $chatBorder.Background = if ($ActiveTab -eq "WhatsApp") { (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#0b141a"))) } else { (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#0e1621"))) }
    [System.Windows.Controls.Grid]::SetColumn($chatBorder, 1)
    [void]$bodyGrid.Children.Add($chatBorder)

    $chatGrid = New-Object System.Windows.Controls.Grid
    $cgHdr = New-Object System.Windows.Controls.RowDefinition; $cgHdr.Height = New-Object System.Windows.GridLength(48)
    $cgMsgs = New-Object System.Windows.Controls.RowDefinition; $cgMsgs.Height = New-Object System.Windows.GridLength(1, [System.Windows.GridUnitType]::Star)
    $cgInput = New-Object System.Windows.Controls.RowDefinition; $cgInput.Height = New-Object System.Windows.GridLength(52)
    [void]$chatGrid.RowDefinitions.Add($cgHdr)
    [void]$chatGrid.RowDefinitions.Add($cgMsgs)
    [void]$chatGrid.RowDefinitions.Add($cgInput)
    $chatBorder.Child = $chatGrid

    # Chat Header
    $chHdrBorder = New-Object System.Windows.Controls.Border
    $chHdrBorder.Background = if ($ActiveTab -eq "WhatsApp") { (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#202c33"))) } else { (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#17212b"))) }
    [System.Windows.Controls.Grid]::SetRow($chHdrBorder, 0)
    [void]$chatGrid.Children.Add($chHdrBorder)

    $chHdrStack = New-Object System.Windows.Controls.StackPanel
    $chHdrStack.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    $chHdrStack.Margin = New-Object System.Windows.Thickness(16, 0, 0, 0)
    $chHdrStack.VerticalAlignment = [System.Windows.VerticalAlignment]::Center

    $chTitle = New-Object System.Windows.Controls.TextBlock
    $chTitle.Text = if ($ActiveTab -eq "WhatsApp") { "Alexander (Berlin)" } else { "HidaChat Community" }
    $chTitle.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#e9edef")))
    $chTitle.FontWeight = [System.Windows.FontWeights]::SemiBold
    $chTitle.FontSize = 14
    [void]$chHdrStack.Children.Add($chTitle)

    $chStatus = New-Object System.Windows.Controls.TextBlock
    $chStatus.Text = if ($ActiveTab -eq "WhatsApp") { " • online" } else { " • 1,280 members" }
    $chStatus.Foreground = if ($ActiveTab -eq "WhatsApp") { (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#25d366"))) } else { (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#24a1de"))) }
    $chStatus.FontSize = 12
    [void]$chHdrStack.Children.Add($chStatus)
    $chHdrBorder.Child = $chHdrStack

    # Chat Messages Stack
    $msgsStack = New-Object System.Windows.Controls.StackPanel
    $msgsStack.Margin = New-Object System.Windows.Thickness(24, 20, 24, 10)
    $msgsStack.VerticalAlignment = [System.Windows.VerticalAlignment]::Top
    [System.Windows.Controls.Grid]::SetRow($msgsStack, 1)
    [void]$chatGrid.Children.Add($msgsStack)

    if ($ActiveTab -eq "WhatsApp") {
        # Message 1 Container (with hover button)
        $m1Container = New-Object System.Windows.Controls.Grid
        $m1Container.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Left
        $m1Container.Margin = New-Object System.Windows.Thickness(0, 0, 0, 6)

        $m1 = New-Object System.Windows.Controls.Border
        $m1.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#202c33")))
        $m1.CornerRadius = New-Object System.Windows.CornerRadius(8)
        $m1.Padding = New-Object System.Windows.Thickness(14, 10, 14, 10)
        $m1.MaxWidth = 440

        $m1Body = New-Object System.Windows.Controls.TextBlock
        $m1Body.Text = "Guten Morgen! Hast du den neuen Release-Bericht erhalten?"
        $m1Body.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#e9edef")))
        $m1Body.FontSize = 13.5
        $m1Body.TextWrapping = [System.Windows.TextWrapping]::Wrap
        $m1.Child = $m1Body
        [void]$m1Container.Children.Add($m1)

        # Hover Translation Button on WhatsApp message
        if ($ShowHoverBtn) {
            $hBtn = New-Object System.Windows.Controls.Border
            $hBtn.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#00a884")))
            $hBtn.CornerRadius = New-Object System.Windows.CornerRadius(14)
            $hBtn.Padding = New-Object System.Windows.Thickness(10, 4, 10, 4)
            $hBtn.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
            $hBtn.VerticalAlignment = [System.Windows.VerticalAlignment]::Top
            $hBtn.Margin = New-Object System.Windows.Thickness(0, -10, -10, 0)
            
            $hBtnShadow = New-Object System.Windows.Media.Effects.DropShadowEffect
            $hBtnShadow.BlurRadius = 12; $hBtnShadow.ShadowDepth = 2; $hBtnShadow.Color = [System.Windows.Media.ColorConverter]::ConvertFromString("#000000"); $hBtnShadow.Opacity = 0.5
            $hBtn.Effect = $hBtnShadow

            $hText = New-Object System.Windows.Controls.TextBlock
            $hText.Text = "Translate"
            $hText.Foreground = [System.Windows.Media.Brushes]::White
            $hText.FontSize = 11.5
            $hText.FontWeight = [System.Windows.FontWeights]::Bold
            $hBtn.Child = $hText
            [void]$m1Container.Children.Add($hBtn)
        }

        [void]$msgsStack.Children.Add($m1Container)

        # Instant Translated Bubble
        if ($ShowTranslation) {
            $tCard = New-Object System.Windows.Controls.Border
            $tCard.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#132938")))
            $tCard.BorderBrush = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#00a884")))
            $tCard.BorderThickness = New-Object System.Windows.Thickness(3, 0, 0, 0)
            $tCard.CornerRadius = New-Object System.Windows.CornerRadius(0, 8, 8, 0)
            $tCard.Padding = New-Object System.Windows.Thickness(12, 8, 12, 8)
            $tCard.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Left
            $tCard.MaxWidth = 440
            $tCard.Margin = New-Object System.Windows.Thickness(12, 0, 0, 16)

            $tCardText = New-Object System.Windows.Controls.TextBlock
            $tCardText.Text = "[IT]: Buongiorno! Hai ricevuto il nuovo report di rilascio?"
            $tCardText.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#25d366")))
            $tCardText.FontSize = 13
            $tCardText.FontWeight = [System.Windows.FontWeights]::SemiBold
            $tCardText.TextWrapping = [System.Windows.TextWrapping]::Wrap
            $tCard.Child = $tCardText
            [void]$msgsStack.Children.Add($tCard)
        }

        # Message 2 Outgoing
        $m2 = New-Object System.Windows.Controls.Border
        $m2.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#005c4b")))
        $m2.CornerRadius = New-Object System.Windows.CornerRadius(8)
        $m2.Padding = New-Object System.Windows.Thickness(14, 10, 14, 10)
        $m2.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
        $m2.MaxWidth = 440
        $m2.Margin = New-Object System.Windows.Thickness(0, 8, 0, 0)

        $m2Body = New-Object System.Windows.Controls.TextBlock
        $m2Body.Text = "Si! HidaChat v0.5.1 e online su winget con traduzione istantanea."
        $m2Body.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#e9edef")))
        $m2Body.FontSize = 13.5
        $m2Body.TextWrapping = [System.Windows.TextWrapping]::Wrap
        $m2.Child = $m2Body
        [void]$msgsStack.Children.Add($m2)

    } else {
        # Telegram Messages
        $tm1 = New-Object System.Windows.Controls.Border
        $tm1.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#182533")))
        $tm1.CornerRadius = New-Object System.Windows.CornerRadius(10)
        $tm1.Padding = New-Object System.Windows.Thickness(14, 10, 14, 10)
        $tm1.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Left
        $tm1.MaxWidth = 460
        $tm1.Margin = New-Object System.Windows.Thickness(0, 0, 0, 12)

        $tm1Stack = New-Object System.Windows.Controls.StackPanel
        $tm1Author = New-Object System.Windows.Controls.TextBlock; $tm1Author.Text = "Telegram Community"; $tm1Author.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#24a1de"))); $tm1Author.FontWeight = [System.Windows.FontWeights]::Bold; $tm1Author.FontSize = 12
        $tm1Text = New-Object System.Windows.Controls.TextBlock; $tm1Text.Text = "Welcome to HidaChat on Telegram! Instant tab preloading is active."; $tm1Text.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#e9edef"))); $tm1Text.FontSize = 13.5; $tm1Text.TextWrapping = [System.Windows.TextWrapping]::Wrap; $tm1Text.Margin = New-Object System.Windows.Thickness(0, 3, 0, 0)
        [void]$tm1Stack.Children.Add($tm1Author)
        [void]$tm1Stack.Children.Add($tm1Text)
        $tm1.Child = $tm1Stack
        [void]$msgsStack.Children.Add($tm1)

        $tm2 = New-Object System.Windows.Controls.Border
        $tm2.Background = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#2b5278")))
        $tm2.CornerRadius = New-Object System.Windows.CornerRadius(10)
        $tm2.Padding = New-Object System.Windows.Thickness(14, 10, 14, 10)
        $tm2.HorizontalAlignment = [System.Windows.HorizontalAlignment]::Right
        $tm2.MaxWidth = 460
        $tm2.Margin = New-Object System.Windows.Thickness(0, 0, 0, 12)

        $tm2Text = New-Object System.Windows.Controls.TextBlock; $tm2Text.Text = "Passaggio istantaneo tra schede WhatsApp e Telegram completato!"; $tm2Text.Foreground = [System.Windows.Media.Brushes]::White; $tm2Text.FontSize = 13.5; $tm2Text.TextWrapping = [System.Windows.TextWrapping]::Wrap
        $tm2.Child = $tm2Text
        [void]$msgsStack.Children.Add($tm2)
    }

    # Input Bar
    $inputBorder = New-Object System.Windows.Controls.Border
    $inputBorder.Background = if ($ActiveTab -eq "WhatsApp") { (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#202c33"))) } else { (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#17212b"))) }
    [System.Windows.Controls.Grid]::SetRow($inputBorder, 2)
    [void]$chatGrid.Children.Add($inputBorder)

    $inpStack = New-Object System.Windows.Controls.StackPanel
    $inpStack.Orientation = [System.Windows.Controls.Orientation]::Horizontal
    $inpStack.Margin = New-Object System.Windows.Thickness(16, 0, 16, 0)
    $inpStack.VerticalAlignment = [System.Windows.VerticalAlignment]::Center

    $inpText = New-Object System.Windows.Controls.TextBlock; $inpText.Text = "Type a message..."; $inpText.Foreground = (New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.ColorConverter]::ConvertFromString("#8696a0"))); $inpText.FontSize = 13
    [void]$inpStack.Children.Add($inpText)
    $inputBorder.Child = $inpStack

    # Animated Mouse Cursor
    if ($ShowCursor) {
        $curCanvas = New-Object System.Windows.Controls.Canvas
        $curCanvas.IsHitTestVisible = $false
        [System.Windows.Controls.Grid]::SetRowSpan($curCanvas, 2)
        [void]$root.Children.Add($curCanvas)

        $curPath = New-Object System.Windows.Shapes.Path
        $curPath.Data = [System.Windows.Media.Geometry]::Parse("M0,0 L0,18 L4.5,14 L8.5,22 L11.5,20.5 L7.5,13 L13,13 Z")
        $curPath.Fill = [System.Windows.Media.Brushes]::White
        $curPath.Stroke = [System.Windows.Media.Brushes]::Black
        $curPath.StrokeThickness = 1.5
        $cShadow = New-Object System.Windows.Media.Effects.DropShadowEffect
        $cShadow.BlurRadius = 8; $cShadow.ShadowDepth = 2; $cShadow.Color = [System.Windows.Media.ColorConverter]::ConvertFromString("#000000"); $cShadow.Opacity = 0.6
        $curPath.Effect = $cShadow

        [System.Windows.Controls.Canvas]::SetLeft($curPath, $CursorX)
        [System.Windows.Controls.Canvas]::SetTop($curPath, $CursorY)
        [void]$curCanvas.Children.Add($curPath)
    }

    # Render Visual to Bitmap
    $size = New-Object System.Windows.Size($width, $height)
    $root.Measure($size)
    $rect = New-Object System.Windows.Rect(0, 0, $width, $height)
    $root.Arrange($rect)
    $root.UpdateLayout()

    $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($width, $height, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $rtb.Render($root)

    # Convert to System.Drawing.Bitmap
    $ms = New-Object System.IO.MemoryStream
    $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
    $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
    $enc.Save($ms)
    $bmp = New-Object System.Drawing.Bitmap($ms)
    $ms.Close()

    return $bmp
}

Write-Host "Rendering frames for animated demo GIF..."

$frames = New-Object System.Collections.Generic.List[System.Drawing.Bitmap]
$delays = New-Object System.Collections.Generic.List[int]

# 1. WhatsApp View (Idle - 1200ms)
$f = Render-Scene -ActiveTab "WhatsApp" -ShowHoverBtn $false -ShowTranslation $false -ShowCursor $true -CursorX 480 -CursorY 320
$frames.Add($f); $delays.Add(1200)

# 2. Cursor moves towards message (Hover effect - 4 frames interpolation)
$pts = @(@(480, 260), @(500, 180), @(520, 130), @(530, 115))
foreach ($pt in $pts) {
    $f = Render-Scene -ActiveTab "WhatsApp" -ShowHoverBtn $true -ShowTranslation $false -ShowCursor $true -CursorX $pt[0] -CursorY $pt[1]
    $frames.Add($f); $delays.Add(180)
}

# 3. Hover button active & click (800ms)
$f = Render-Scene -ActiveTab "WhatsApp" -ShowHoverBtn $true -ShowTranslation $false -ShowCursor $true -CursorX 535 -CursorY 110
$frames.Add($f); $delays.Add(800)

# 4. Instant Translation Revealed! (2200ms)
$f = Render-Scene -ActiveTab "WhatsApp" -ShowHoverBtn $false -ShowTranslation $true -ShowCursor $true -CursorX 535 -CursorY 110
$frames.Add($f); $delays.Add(2200)

# 5. Cursor moves up to Telegram tab (3 frames)
$pts2 = @(@(400, 70), @(280, 30), @(220, 22))
foreach ($pt in $pts2) {
    $f = Render-Scene -ActiveTab "WhatsApp" -ShowHoverBtn $false -ShowTranslation $true -ShowCursor $true -CursorX $pt[0] -CursorY $pt[1]
    $frames.Add($f); $delays.Add(200)
}

# 6. Click on Telegram tab! (400ms)
$f = Render-Scene -ActiveTab "WhatsApp" -ShowHoverBtn $false -ShowTranslation $true -ShowCursor $true -CursorX 220 -CursorY 22
$frames.Add($f); $delays.Add(400)

# 7. Instant Tab Switch to Telegram! (0ms reload, hot session - 2400ms)
$f = Render-Scene -ActiveTab "Telegram" -ShowHoverBtn $false -ShowTranslation $false -ShowCursor $true -CursorX 220 -CursorY 22
$frames.Add($f); $delays.Add(2400)

# 8. Cursor moves back towards center (2 frames)
$f = Render-Scene -ActiveTab "Telegram" -ShowHoverBtn $false -ShowTranslation $false -ShowCursor $true -CursorX 340 -CursorY 120
$frames.Add($f); $delays.Add(250)

$f = Render-Scene -ActiveTab "Telegram" -ShowHoverBtn $false -ShowTranslation $false -ShowCursor $true -CursorX 480 -CursorY 240
$frames.Add($f); $delays.Add(350)

# Encode into Animated GIF
$outGif = Join-Path (Get-Location) "images\demo.gif"
Write-Host "Saving animated GIF with $($frames.Count) frames to: $outGif"

[GifMaker]::SaveGif($frames.ToArray(), $delays.ToArray(), $outGif)

foreach ($frm in $frames) { $frm.Dispose() }

Write-Host "Animated demo GIF created successfully at: $outGif (Size: $((Get-Item $outGif).Length) bytes)"
