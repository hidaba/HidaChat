using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace IconGen;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Console.WriteLine("Starting HidaChat Icon Generator (Refined)...");

        string rootDir = Directory.GetCurrentDirectory();
        string imagesDir = System.IO.Path.Combine(rootDir, "images");
        if (!Directory.Exists(imagesDir))
        {
            imagesDir = System.IO.Path.Combine(rootDir, "..", "images");
        }
        if (!Directory.Exists(imagesDir))
        {
            Directory.CreateDirectory(imagesDir);
        }

        // 1. Generate 512x512 icon.png
        byte[] png512 = RenderIcon(512, hasNotification: false);
        string pngPath = System.IO.Path.Combine(imagesDir, "icon.png");
        File.WriteAllBytes(pngPath, png512);
        Console.WriteLine($"Saved {pngPath} (512x512, 32-bit RGBA Transparent)");

        // 2. Generate multi-resolution icon.ico
        int[] icoSizes = [256, 128, 64, 48, 32, 24, 16];
        var mainPngList = new List<byte[]>();
        foreach (int sz in icoSizes)
        {
            mainPngList.Add(RenderIcon(sz, hasNotification: false));
        }
        string icoPath = System.IO.Path.Combine(imagesDir, "icon.ico");
        BuildIcoFile(icoPath, icoSizes, mainPngList);
        Console.WriteLine($"Saved {icoPath} ({string.Join(", ", icoSizes)})");

        // 3. Generate multi-resolution icon_notification.ico
        var notifPngList = new List<byte[]>();
        foreach (int sz in icoSizes)
        {
            notifPngList.Add(RenderIcon(sz, hasNotification: true));
        }
        string notifIcoPath = System.IO.Path.Combine(imagesDir, "icon_notification.ico");
        BuildIcoFile(notifIcoPath, icoSizes, notifPngList);
        Console.WriteLine($"Saved {notifIcoPath} ({string.Join(", ", icoSizes)})");

        // 4. Sync to build output folders if they exist
        string[] targetFolders = [
            System.IO.Path.Combine(rootDir, "bin", "Debug", "net9.0-windows10.0.19041.0", "images"),
            System.IO.Path.Combine(rootDir, "bin", "Release", "net9.0-windows10.0.19041.0", "images"),
            System.IO.Path.Combine(rootDir, "bin", "Release", "staging", "images"),
            System.IO.Path.Combine(rootDir, "publish", "images")
        ];

        foreach (string target in targetFolders)
        {
            if (Directory.Exists(target))
            {
                File.Copy(pngPath, System.IO.Path.Combine(target, "icon.png"), true);
                File.Copy(icoPath, System.IO.Path.Combine(target, "icon.ico"), true);
                File.Copy(notifIcoPath, System.IO.Path.Combine(target, "icon_notification.ico"), true);
                Console.WriteLine($"Synchronized icons to {target}");
            }
        }

        Console.WriteLine("Icon generation completed successfully!");
    }

    static byte[] RenderIcon(int size, bool hasNotification)
    {
        double scale = size / 512.0;

        Canvas rootCanvas = new Canvas
        {
            Width = size,
            Height = size,
            Background = System.Windows.Media.Brushes.Transparent
        };

        // 1. Squircle App Icon Container (Centered with clean transparent background outside)
        double cardSize = 452.0 * scale;
        Border card = new Border
        {
            Width = cardSize,
            Height = cardSize,
            CornerRadius = new CornerRadius(108.0 * scale),
            ClipToBounds = true
        };

        // Dark modern background gradient (#142028 -> #0e171e -> #080e12)
        LinearGradientBrush bgBrush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        bgBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#15222b"), 0.0));
        bgBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0e171e"), 0.5));
        bgBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#080f14"), 1.0));
        card.Background = bgBrush;

        // Glowing border rim: Neon Cyan / Emerald to Vibrant Purple / Magenta
        LinearGradientBrush borderBrush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        borderBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00f5d4"), 0.0));
        borderBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10b981"), 0.35));
        borderBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#8b5cf6"), 0.75));
        borderBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ec4899"), 1.0));
        card.BorderBrush = borderBrush;
        card.BorderThickness = new Thickness(4.5 * scale);

        Canvas.SetLeft(card, (512.0 - 452.0) / 2.0 * scale);
        Canvas.SetTop(card, (512.0 - 452.0) / 2.0 * scale);
        rootCanvas.Children.Add(card);

        // Inner artwork container
        Canvas artCanvas = new Canvas
        {
            Width = cardSize,
            Height = cardSize
        };
        card.Child = artCanvas;

        // Top subtle glossy highlight curve
        PathGeometry glossGeo = new PathGeometry();
        PathFigure glossFig = new PathFigure
        {
            StartPoint = new System.Windows.Point(0, 0),
            IsClosed = true
        };
        glossFig.Segments.Add(new LineSegment(new System.Windows.Point(cardSize, 0), true));
        glossFig.Segments.Add(new LineSegment(new System.Windows.Point(cardSize, 160.0 * scale), true));
        glossFig.Segments.Add(new BezierSegment(
            new System.Windows.Point(cardSize * 0.7, 190.0 * scale),
            new System.Windows.Point(cardSize * 0.3, 140.0 * scale),
            new System.Windows.Point(0, 160.0 * scale),
            true));
        glossGeo.Figures.Add(glossFig);

        LinearGradientBrush glossBrush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(0, 1)
        };
        glossBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1affffff"), 0.0));
        glossBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00ffffff"), 1.0));

        System.Windows.Shapes.Path glossPath = new System.Windows.Shapes.Path
        {
            Data = glossGeo,
            Fill = glossBrush
        };
        artCanvas.Children.Add(glossPath);

        // Top-left ambient cyan glow
        Ellipse cyanGlow = new Ellipse
        {
            Width = 300.0 * scale,
            Height = 300.0 * scale
        };
        RadialGradientBrush cgBrush = new RadialGradientBrush();
        cgBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4500f5d4"), 0.0));
        cgBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0000f5d4"), 1.0));
        cyanGlow.Fill = cgBrush;
        Canvas.SetLeft(cyanGlow, -20.0 * scale);
        Canvas.SetTop(cyanGlow, -20.0 * scale);
        artCanvas.Children.Add(cyanGlow);

        // Bottom-right ambient magenta glow
        Ellipse magentaGlow = new Ellipse
        {
            Width = 300.0 * scale,
            Height = 300.0 * scale
        };
        RadialGradientBrush mgBrush = new RadialGradientBrush();
        mgBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#45ec4899"), 0.0));
        mgBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00ec4899"), 1.0));
        magentaGlow.Fill = mgBrush;
        Canvas.SetLeft(magentaGlow, (cardSize - 280.0 * scale));
        Canvas.SetTop(magentaGlow, (cardSize - 280.0 * scale));
        artCanvas.Children.Add(magentaGlow);

        // 2. Chat Speech Bubble
        LinearGradientBrush bubbleStroke = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        bubbleStroke.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00f5d4"), 0.0));
        bubbleStroke.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#25d366"), 0.28));
        bubbleStroke.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#a855f7"), 0.72));
        bubbleStroke.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#f43f5e"), 1.0));

        LinearGradientBrush bubbleFill = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1)
        };
        bubbleFill.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4000a884"), 0.0));
        bubbleFill.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#281f2c34"), 0.5));
        bubbleFill.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#407c3aed"), 1.0));

        PathGeometry bubbleGeo = new PathGeometry();
        PathFigure bubbleFig = new PathFigure
        {
            StartPoint = new System.Windows.Point(146.0 * scale, 320.0 * scale),
            IsClosed = true
        };

        // Tail bottom-left
        bubbleFig.Segments.Add(new BezierSegment(
            new System.Windows.Point(126.0 * scale, 340.0 * scale),
            new System.Windows.Point(86.0 * scale, 370.0 * scale),
            new System.Windows.Point(80.0 * scale, 372.0 * scale),
            true));

        bubbleFig.Segments.Add(new BezierSegment(
            new System.Windows.Point(92.0 * scale, 350.0 * scale),
            new System.Windows.Point(100.0 * scale, 308.0 * scale),
            new System.Windows.Point(106.0 * scale, 282.0 * scale),
            true));

        // Left arc
        bubbleFig.Segments.Add(new BezierSegment(
            new System.Windows.Point(76.0 * scale, 226.0 * scale),
            new System.Windows.Point(96.0 * scale, 138.0 * scale),
            new System.Windows.Point(166.0 * scale, 92.0 * scale),
            true));

        // Top arc
        bubbleFig.Segments.Add(new BezierSegment(
            new System.Windows.Point(226.0 * scale, 50.0 * scale),
            new System.Windows.Point(310.0 * scale, 50.0 * scale),
            new System.Windows.Point(362.0 * scale, 94.0 * scale),
            true));

        // Right arc
        bubbleFig.Segments.Add(new BezierSegment(
            new System.Windows.Point(412.0 * scale, 142.0 * scale),
            new System.Windows.Point(412.0 * scale, 244.0 * scale),
            new System.Windows.Point(362.0 * scale, 294.0 * scale),
            true));

        // Bottom arc
        bubbleFig.Segments.Add(new BezierSegment(
            new System.Windows.Point(310.0 * scale, 340.0 * scale),
            new System.Windows.Point(222.0 * scale, 344.0 * scale),
            new System.Windows.Point(146.0 * scale, 320.0 * scale),
            true));

        bubbleGeo.Figures.Add(bubbleFig);

        // Soft ambient bubble halo
        System.Windows.Shapes.Path bubbleHalo = new System.Windows.Shapes.Path
        {
            Data = bubbleGeo,
            Stroke = bubbleStroke,
            StrokeThickness = 24.0 * scale,
            StrokeLineJoin = PenLineJoin.Round,
            Opacity = 0.35
        };
        artCanvas.Children.Add(bubbleHalo);

        // Crisp bubble body & stroke
        System.Windows.Shapes.Path bubblePath = new System.Windows.Shapes.Path
        {
            Data = bubbleGeo,
            Fill = bubbleFill,
            Stroke = bubbleStroke,
            StrokeThickness = 16.0 * scale,
            StrokeLineJoin = PenLineJoin.Round
        };
        artCanvas.Children.Add(bubblePath);

        // 3. Audio / Waveform pulse line inside the bubble
        LinearGradientBrush waveBrush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 0)
        };
        waveBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00f5d4"), 0.0));
        waveBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#38bdf8"), 0.28));
        waveBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#c084fc"), 0.65));
        waveBrush.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#f43f5e"), 1.0));

        PathGeometry waveGeo = new PathGeometry();
        PathFigure waveFig = new PathFigure
        {
            StartPoint = new System.Windows.Point(138.0 * scale, 202.0 * scale),
            IsClosed = false
        };

        waveFig.Segments.Add(new BezierSegment(
            new System.Windows.Point(158.0 * scale, 202.0 * scale),
            new System.Windows.Point(168.0 * scale, 232.0 * scale),
            new System.Windows.Point(184.0 * scale, 232.0 * scale),
            true));

        waveFig.Segments.Add(new BezierSegment(
            new System.Windows.Point(200.0 * scale, 232.0 * scale),
            new System.Windows.Point(206.0 * scale, 136.0 * scale),
            new System.Windows.Point(226.0 * scale, 136.0 * scale),
            true));

        waveFig.Segments.Add(new BezierSegment(
            new System.Windows.Point(242.0 * scale, 136.0 * scale),
            new System.Windows.Point(252.0 * scale, 268.0 * scale),
            new System.Windows.Point(272.0 * scale, 268.0 * scale),
            true));

        waveFig.Segments.Add(new BezierSegment(
            new System.Windows.Point(288.0 * scale, 268.0 * scale),
            new System.Windows.Point(298.0 * scale, 164.0 * scale),
            new System.Windows.Point(314.0 * scale, 164.0 * scale),
            true));

        waveFig.Segments.Add(new BezierSegment(
            new System.Windows.Point(328.0 * scale, 164.0 * scale),
            new System.Windows.Point(338.0 * scale, 202.0 * scale),
            new System.Windows.Point(358.0 * scale, 202.0 * scale),
            true));

        waveGeo.Figures.Add(waveFig);

        // Wave path glow layer (soft background stroke)
        System.Windows.Shapes.Path waveGlowPath = new System.Windows.Shapes.Path
        {
            Data = waveGeo,
            Stroke = waveBrush,
            StrokeThickness = 24.0 * scale,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Opacity = 0.4
        };
        artCanvas.Children.Add(waveGlowPath);

        // Sharp core wave stroke
        System.Windows.Shapes.Path waveCorePath = new System.Windows.Shapes.Path
        {
            Data = waveGeo,
            Stroke = waveBrush,
            StrokeThickness = 15.0 * scale,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round
        };
        artCanvas.Children.Add(waveCorePath);

        // 4. Notification badge for icon_notification.ico
        if (hasNotification)
        {
            double badgeSize = 130.0 * scale;
            Border badge = new Border
            {
                Width = badgeSize,
                Height = badgeSize,
                CornerRadius = new CornerRadius(badgeSize / 2.0)
            };

            LinearGradientBrush badgeBg = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1)
            };
            badgeBg.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#ff453a"), 0.0));
            badgeBg.GradientStops.Add(new GradientStop((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#e11d48"), 1.0));
            badge.Background = badgeBg;

            badge.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#15222b"));
            badge.BorderThickness = new Thickness(7.0 * scale);

            Ellipse badgeDot = new Ellipse
            {
                Width = 40.0 * scale,
                Height = 40.0 * scale,
                Fill = System.Windows.Media.Brushes.White
            };
            badge.Child = badgeDot;

            Canvas.SetLeft(badge, 364.0 * scale);
            Canvas.SetTop(badge, 16.0 * scale);
            rootCanvas.Children.Add(badge);
        }

        // Layout & Render
        System.Windows.Size renderSize = new System.Windows.Size(size, size);
        rootCanvas.Measure(renderSize);
        rootCanvas.Arrange(new Rect(0, 0, size, size));
        rootCanvas.UpdateLayout();

        RenderTargetBitmap rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(rootCanvas);

        PngBitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using MemoryStream ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    static void BuildIcoFile(string outputPath, int[] sizes, List<byte[]> pngByteArrays)
    {
        using FileStream fs = new FileStream(outputPath, FileMode.Create);
        using BinaryWriter bw = new BinaryWriter(fs);

        // ICONDIR
        bw.Write((ushort)0); // Reserved
        bw.Write((ushort)1); // Type: Icon
        bw.Write((ushort)pngByteArrays.Count); // Count

        int offset = 6 + (16 * pngByteArrays.Count);

        for (int i = 0; i < pngByteArrays.Count; i++)
        {
            byte[] bytes = pngByteArrays[i];
            int sz = sizes[i];
            byte w = (byte)(sz >= 256 ? 0 : sz);
            byte h = (byte)(sz >= 256 ? 0 : sz);

            bw.Write(w);
            bw.Write(h);
            bw.Write((byte)0); // Colors
            bw.Write((byte)0); // Reserved
            bw.Write((ushort)1); // Planes
            bw.Write((ushort)32); // BPP
            bw.Write((uint)bytes.Length); // Size
            bw.Write((uint)offset); // Offset

            offset += bytes.Length;
        }

        foreach (byte[] bytes in pngByteArrays)
        {
            bw.Write(bytes);
        }
    }
}
