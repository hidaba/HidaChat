Option Strict On
Option Explicit On

Imports System.Globalization
Imports System.Text.RegularExpressions
#If Not NO_WPF Then
Imports System.Windows
Imports System.Windows.Media
#End If

Namespace UI.Theme

    ''' <summary>
    ''' Supported application theme modes.
    ''' </summary>
    Public Enum AppThemeMode
        System = 0
        Light = 1
        Dark = 2
    End Enum

    ''' <summary>
    ''' Immutable color palette representing all semantic Fluent Design tokens for a theme.
    ''' </summary>
    Public NotInheritable Class ThemePalette
        Public Property IsDark As Boolean
        Public Property AppBackground As String
        Public Property Surface As String
        Public Property SurfaceElevated As String
        Public Property SurfaceHover As String
        Public Property SurfacePressed As String
        Public Property BorderSubtle As String
        Public Property BorderStrong As String
        Public Property TextPrimary As String
        Public Property TextSecondary As String
        Public Property TextMuted As String
        Public Property Accent As String
        Public Property AccentHover As String
        Public Property AccentPressed As String
        Public Property AccentForeground As String
        Public Property AccentSubtle As String
        Public Property Success As String
        Public Property SuccessSubtle As String
        Public Property ErrorColor As String
        Public Property ErrorSubtle As String
        Public Property Warning As String
        Public Property WarningSubtle As String
        Public Property Info As String
        Public Property InfoSubtle As String
        Public Property FocusRing As String
    End Class

    ''' <summary>
    ''' Theme-aware, WCAG AA-compliant background, foreground, and border colors for semantic status badges.
    ''' </summary>
    Public NotInheritable Class StatusBadgePalette
        Public Property BackgroundHex As String
        Public Property ForegroundHex As String
        Public Property BorderHex As String
    End Class

    ''' <summary>
    ''' Centralized Fluent Design tokens (typography, spacing, radii, color palettes, and WCAG contrast utilities).
    ''' </summary>
    Public Module DesignTokens

        ' Typography tokens (Segoe UI Fluent scale)
        Public Const FontFamilyPrimary As String = "Segoe UI"
        Public Const FontFamilyMonospace As String = "Cascadia Code, Consolas, Courier New"

        Public Const FontSizeCaption As Double = 12.0
        Public Const FontSizeBody As Double = 14.0          ' ~10.5 pt
        Public Const FontSizeBodyLarge As Double = 15.0     ' ~11.25 pt
        Public Const FontSizeSubtitle As Double = 16.0
        Public Const FontSizeTitle As Double = 18.0
        Public Const FontSizeDisplay As Double = 22.0

        ' Spacing scale (multiples of 4 and 8)
        Public Const SpaceXs As Double = 4.0
        Public Const SpaceSm As Double = 8.0
        Public Const SpaceMd As Double = 12.0
        Public Const SpaceLg As Double = 16.0
        Public Const SpaceXl As Double = 24.0
        Public Const Space2Xl As Double = 32.0

        ' Corner radius tokens (6-8 px Fluent standard)
        Public Const RadiusSm As Double = 6.0
        Public Const RadiusMd As Double = 8.0
        Public Const RadiusLg As Double = 12.0
        Public Const RadiusPill As Double = 999.0

        ' Reference Light theme colors
        Public Const LightBackgroundHex As String = "#F8FAFC"
        Public Const LightSurfaceHex As String = "#FFFFFF"
        Public Const LightSurfaceElevatedHex As String = "#F1F5F9"
        Public Const LightSurfaceHoverHex As String = "#E2E8F0"
        Public Const LightSurfacePressedHex As String = "#CBD5E1"
        Public Const LightBorderSubtleHex As String = "#E2E8F0"
        Public Const LightBorderStrongHex As String = "#CBD5E1"
        Public Const LightTextPrimaryHex As String = "#0F172A"
        Public Const LightTextSecondaryHex As String = "#475569"
        Public Const LightTextMutedHex As String = "#64748B"

        ' Reference Dark theme colors
        Public Const DarkBackgroundHex As String = "#0F172A"
        Public Const DarkSurfaceHex As String = "#1E293B"
        Public Const DarkSurfaceElevatedHex As String = "#273549"
        Public Const DarkSurfaceHoverHex As String = "#334155"
        Public Const DarkSurfacePressedHex As String = "#475569"
        Public Const DarkBorderSubtleHex As String = "#334155"
        Public Const DarkBorderStrongHex As String = "#475569"
        Public Const DarkTextPrimaryHex As String = "#F1F5F9"
        Public Const DarkTextSecondaryHex As String = "#94A3B8"
        Public Const DarkTextMutedHex As String = "#64748B"

        ' Semantic & brand accent colors
        Public Const DefaultAccentHex As String = "#2563EB"       ' Fluent Blue (default)
        Public Const BrandEmeraldAccentHex As String = "#00A884"  ' HidaChat / Messaging brand accent
        Public Const AccentVioletHex As String = "#7C3AED"
        Public Const AccentTealHex As String = "#0D9488"
        Public Const AccentRoseHex As String = "#E11D48"

        Public Const SuccessLightHex As String = "#16A34A"
        Public Const SuccessDarkHex As String = "#22C55E"
        Public Const ErrorLightHex As String = "#DC2626"
        Public Const ErrorDarkHex As String = "#EF4444"
        Public Const WarningLightHex As String = "#D97706"
        Public Const WarningDarkHex As String = "#F59E0B"
        Public Const InfoLightHex As String = "#0284C7"
        Public Const InfoDarkHex As String = "#38BDF8"

        Private ReadOnly HexColorRegex As New Regex("^#([0-9A-Fa-f]{6})$", RegexOptions.Compiled)

        ''' <summary>
        ''' Validates whether a string is a 6-digit hex RGB color (e.g. "#2563EB").
        ''' </summary>
        Public Function IsValidHexColor(hex As String) As Boolean
            If String.IsNullOrWhiteSpace(hex) Then Return False
            Return HexColorRegex.IsMatch(hex.Trim())
        End Function

        ''' <summary>
        ''' Normalizes a hex color string or returns the fallback default accent color.
        ''' </summary>
        Public Function NormalizeAccentHex(hex As String) As String
            If IsValidHexColor(hex) Then
                Return hex.Trim().ToUpperInvariant()
            End If
            Return DefaultAccentHex
        End Function

        ''' <summary>
        ''' Builds the complete semantic palette for the requested theme polarity and accent color.
        ''' </summary>
        Public Function GetPalette(isDark As Boolean, Optional accentHex As String = DefaultAccentHex) As ThemePalette
            Dim normalizedAccent = NormalizeAccentHex(accentHex)
            Dim effectiveAccent = If(isDark AndAlso normalizedAccent.Equals(DefaultAccentHex, StringComparison.OrdinalIgnoreCase),
                                     "#3B82F6",
                                     normalizedAccent)
            Dim accentHover = AdjustBrightness(effectiveAccent, If(isDark, 0.12, -0.1))
            Dim accentPressed = AdjustBrightness(effectiveAccent, If(isDark, -0.12, -0.2))
            Dim accentForeground = GetAccessibleForegroundHex(effectiveAccent)
            Dim accentSubtle = BlendHex(effectiveAccent, If(isDark, DarkSurfaceHex, LightSurfaceHex), If(isDark, 0.22, 0.12))

            If isDark Then
                Return New ThemePalette With {
                    .IsDark = True,
                    .AppBackground = DarkBackgroundHex,
                    .Surface = DarkSurfaceHex,
                    .SurfaceElevated = DarkSurfaceElevatedHex,
                    .SurfaceHover = DarkSurfaceHoverHex,
                    .SurfacePressed = DarkSurfacePressedHex,
                    .BorderSubtle = DarkBorderSubtleHex,
                    .BorderStrong = DarkBorderStrongHex,
                    .TextPrimary = DarkTextPrimaryHex,
                    .TextSecondary = DarkTextSecondaryHex,
                    .TextMuted = DarkTextMutedHex,
                    .Accent = effectiveAccent,
                    .AccentHover = accentHover,
                    .AccentPressed = accentPressed,
                    .AccentForeground = accentForeground,
                    .AccentSubtle = accentSubtle,
                    .Success = SuccessDarkHex,
                    .SuccessSubtle = BlendHex(SuccessDarkHex, DarkSurfaceHex, 0.2),
                    .ErrorColor = ErrorDarkHex,
                    .ErrorSubtle = BlendHex(ErrorDarkHex, DarkSurfaceHex, 0.2),
                    .Warning = WarningDarkHex,
                    .WarningSubtle = BlendHex(WarningDarkHex, DarkSurfaceHex, 0.2),
                    .Info = InfoDarkHex,
                    .InfoSubtle = BlendHex(InfoDarkHex, DarkSurfaceHex, 0.2),
                    .FocusRing = effectiveAccent
                }
            Else
                Return New ThemePalette With {
                    .IsDark = False,
                    .AppBackground = LightBackgroundHex,
                    .Surface = LightSurfaceHex,
                    .SurfaceElevated = LightSurfaceElevatedHex,
                    .SurfaceHover = LightSurfaceHoverHex,
                    .SurfacePressed = LightSurfacePressedHex,
                    .BorderSubtle = LightBorderSubtleHex,
                    .BorderStrong = LightBorderStrongHex,
                    .TextPrimary = LightTextPrimaryHex,
                    .TextSecondary = LightTextSecondaryHex,
                    .TextMuted = LightTextMutedHex,
                    .Accent = effectiveAccent,
                    .AccentHover = accentHover,
                    .AccentPressed = accentPressed,
                    .AccentForeground = accentForeground,
                    .AccentSubtle = accentSubtle,
                    .Success = SuccessLightHex,
                    .SuccessSubtle = BlendHex(SuccessLightHex, LightSurfaceHex, 0.14),
                    .ErrorColor = ErrorLightHex,
                    .ErrorSubtle = BlendHex(ErrorLightHex, LightSurfaceHex, 0.14),
                    .Warning = WarningLightHex,
                    .WarningSubtle = BlendHex(WarningLightHex, LightSurfaceHex, 0.14),
                    .Info = InfoLightHex,
                    .InfoSubtle = BlendHex(InfoLightHex, LightSurfaceHex, 0.14),
                    .FocusRing = effectiveAccent
                }
            End If
        End Function

        ''' <summary>
        ''' Parses a 6-digit hex color into RGB byte components.
        ''' </summary>
        Public Sub ParseHexRgb(hex As String, ByRef r As Byte, ByRef g As Byte, ByRef b As Byte)
            Dim normalized = NormalizeAccentHex(hex)
            r = Byte.Parse(normalized.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            g = Byte.Parse(normalized.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            b = Byte.Parse(normalized.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
        End Sub

        ''' <summary>
        ''' Computes WCAG 2.1 relative luminance in the range [0.0, 1.0].
        ''' </summary>
        Public Function GetRelativeLuminance(hex As String) As Double
            Dim r As Byte, g As Byte, b As Byte
            ParseHexRgb(hex, r, g, b)

            Dim rLin = LinearizeChannel(r / 255.0)
            Dim gLin = LinearizeChannel(g / 255.0)
            Dim bLin = LinearizeChannel(b / 255.0)

            Return (0.2126 * rLin) + (0.7152 * gLin) + (0.0722 * bLin)
        End Function

        ''' <summary>
        ''' Computes the WCAG 2.1 contrast ratio between two hex colors (range 1.0 to 21.0).
        ''' </summary>
        Public Function GetContrastRatio(foregroundHex As String, backgroundHex As String) As Double
            Dim l1 = GetRelativeLuminance(foregroundHex)
            Dim l2 = GetRelativeLuminance(backgroundHex)
            Dim lighter = Math.Max(l1, l2)
            Dim darker = Math.Min(l1, l2)
            Return Math.Round((lighter + 0.05) / (darker + 0.05), 2)
        End Function

        ''' <summary>
        ''' Selects either pure white (#FFFFFF) or dark slate (#0F172A) for maximum WCAG legibility on a given background.
        ''' </summary>
        Public Function GetAccessibleForegroundHex(backgroundHex As String) As String
            Dim whiteContrast = GetContrastRatio("#FFFFFF", backgroundHex)
            Dim darkContrast = GetContrastRatio("#0F172A", backgroundHex)
            Return If(whiteContrast >= darkContrast, "#FFFFFF", "#0F172A")
        End Function

        ''' <summary>
        ''' Returns high-contrast, theme-aware background, foreground, and border hex colors for a semantic status badge.
        ''' Supports "success", "warning", "error", "info", and "neutral" (default).
        ''' </summary>
        Public Function GetStatusBadgePalette(severity As String, isDark As Boolean) As StatusBadgePalette
            Dim key = If(severity, String.Empty).Trim().ToLowerInvariant()
            Select Case key
                Case "success", "running", "online"
                    If isDark Then
                        Return New StatusBadgePalette With {.BackgroundHex = "#143326", .ForegroundHex = "#4ADE80", .BorderHex = "#1F513B"}
                    Else
                        Return New StatusBadgePalette With {.BackgroundHex = "#DCFCE7", .ForegroundHex = "#166534", .BorderHex = "#86EFAC"}
                    End If
                Case "warning", "needslogin"
                    If isDark Then
                        Return New StatusBadgePalette With {.BackgroundHex = "#3B2D10", .ForegroundHex = "#FCD34D", .BorderHex = "#664D19"}
                    Else
                        Return New StatusBadgePalette With {.BackgroundHex = "#FEF3C7", .ForegroundHex = "#92400E", .BorderHex = "#FCD34D"}
                    End If
                Case "error", "failed"
                    If isDark Then
                        Return New StatusBadgePalette With {.BackgroundHex = "#3F171A", .ForegroundHex = "#FCA5A5", .BorderHex = "#7F1D1D"}
                    Else
                        Return New StatusBadgePalette With {.BackgroundHex = "#FEE2E2", .ForegroundHex = "#991B1B", .BorderHex = "#FCA5A5"}
                    End If
                Case "info", "starting"
                    If isDark Then
                        Return New StatusBadgePalette With {.BackgroundHex = "#172E44", .ForegroundHex = "#7DD3FC", .BorderHex = "#1E4B6E"}
                    Else
                        Return New StatusBadgePalette With {.BackgroundHex = "#E0F2FE", .ForegroundHex = "#075985", .BorderHex = "#7DD3FC"}
                    End If
                Case Else
                    If isDark Then
                        Return New StatusBadgePalette With {.BackgroundHex = "#2A3942", .ForegroundHex = "#CBD5E1", .BorderHex = "#3B4D59"}
                    Else
                        Return New StatusBadgePalette With {.BackgroundHex = "#E2E8F0", .ForegroundHex = "#334155", .BorderHex = "#CBD5E1"}
                    End If
            End Select
        End Function

        ''' <summary>
        ''' Lightens (positive factor) or darkens (negative factor) a hex color safely.
        ''' </summary>
        Public Function AdjustBrightness(hex As String, factor As Double) As String
            Dim r As Byte, g As Byte, b As Byte
            ParseHexRgb(hex, r, g, b)

            Dim nr = ClampByte(If(factor >= 0, r + (255 - r) * factor, r * (1.0 + factor)))
            Dim ng = ClampByte(If(factor >= 0, g + (255 - g) * factor, g * (1.0 + factor)))
            Dim nb = ClampByte(If(factor >= 0, b + (255 - b) * factor, b * (1.0 + factor)))

            Return $"#{nr:X2}{ng:X2}{nb:X2}"
        End Function

        ''' <summary>
        ''' Alpha-blends a foreground hex color over a background hex color using a ratio in [0.0, 1.0].
        ''' </summary>
        Public Function BlendHex(foregroundHex As String, backgroundHex As String, foregroundWeight As Double) As String
            Dim w = Math.Max(0.0, Math.Min(1.0, foregroundWeight))
            Dim fr As Byte, fg As Byte, fb As Byte
            Dim br As Byte, bg As Byte, bb As Byte
            ParseHexRgb(foregroundHex, fr, fg, fb)
            ParseHexRgb(backgroundHex, br, bg, bb)

            Dim r = ClampByte((fr * w) + (br * (1.0 - w)))
            Dim g = ClampByte((fg * w) + (bg * (1.0 - w)))
            Dim b = ClampByte((fb * w) + (bb * (1.0 - w)))

            Return $"#{r:X2}{g:X2}{b:X2}"
        End Function

        Private Function LinearizeChannel(channel As Double) As Double
            If channel <= 0.03928 Then
                Return channel / 12.92
            End If
            Return Math.Pow((channel + 0.055) / 1.055, 2.4)
        End Function

        Private Function ClampByte(value As Double) As Byte
            If value < 0 Then Return 0
            If value > 255 Then Return 255
            Return CByte(Math.Round(value))
        End Function

#If Not NO_WPF Then
        ''' <summary>
        ''' Creates a frozen WPF SolidColorBrush from a hex color string using the shared BrushCache.
        ''' </summary>
        Public Function CreateFrozenBrush(hex As String) As SolidColorBrush
            Return BrushCache.GetBrush(hex)
        End Function

        ''' <summary>
        ''' Populates or updates all semantic brush keys in a WPF ResourceDictionary from a ThemePalette.
        ''' </summary>
        Public Sub ApplyPaletteToResources(resources As ResourceDictionary, palette As ThemePalette)
            If resources Is Nothing OrElse palette Is Nothing Then Return

            resources("AppBackgroundBrush") = CreateFrozenBrush(palette.AppBackground)
            resources("SurfaceBrush") = CreateFrozenBrush(palette.Surface)
            resources("SurfaceElevatedBrush") = CreateFrozenBrush(palette.SurfaceElevated)
            resources("SurfaceHoverBrush") = CreateFrozenBrush(palette.SurfaceHover)
            resources("SurfacePressedBrush") = CreateFrozenBrush(palette.SurfacePressed)
            resources("BorderSubtleBrush") = CreateFrozenBrush(palette.BorderSubtle)
            resources("BorderStrongBrush") = CreateFrozenBrush(palette.BorderStrong)
            resources("TextPrimaryBrush") = CreateFrozenBrush(palette.TextPrimary)
            resources("TextSecondaryBrush") = CreateFrozenBrush(palette.TextSecondary)
            resources("TextMutedBrush") = CreateFrozenBrush(palette.TextMuted)
            resources("AccentBrush") = CreateFrozenBrush(palette.Accent)
            resources("AccentHoverBrush") = CreateFrozenBrush(palette.AccentHover)
            resources("AccentPressedBrush") = CreateFrozenBrush(palette.AccentPressed)
            resources("AccentForegroundBrush") = CreateFrozenBrush(palette.AccentForeground)
            resources("AccentSubtleBrush") = CreateFrozenBrush(palette.AccentSubtle)
            resources("SuccessBrush") = CreateFrozenBrush(palette.Success)
            resources("SuccessSubtleBrush") = CreateFrozenBrush(palette.SuccessSubtle)
            resources("ErrorBrush") = CreateFrozenBrush(palette.ErrorColor)
            resources("ErrorSubtleBrush") = CreateFrozenBrush(palette.ErrorSubtle)
            resources("WarningBrush") = CreateFrozenBrush(palette.Warning)
            resources("WarningSubtleBrush") = CreateFrozenBrush(palette.WarningSubtle)
            resources("InfoBrush") = CreateFrozenBrush(palette.Info)
            resources("InfoSubtleBrush") = CreateFrozenBrush(palette.InfoSubtle)
            resources("FocusBrush") = CreateFrozenBrush(palette.FocusRing)
        End Sub
#End If

    End Module

End Namespace
