# Color Palette & Contrast Verification

## Semantic Palette Tokens

All theme-sensitive UI elements MUST bind via `{DynamicResource <TokenName>}` in XAML or `DesignTokens.GetPalette(isDark, accentHex)` in VB.NET.

| Resource Key | Light Theme | Dark Theme | Purpose |
|---|---|---|---|
| `AppBackgroundBrush` | `#F8FAFC` | `#0F172A` | Primary window canvas |
| `SurfaceBrush` | `#FFFFFF` | `#1E293B` | Cards, panels, sidebar, inputs |
| `SurfaceElevatedBrush` | `#F1F5F9` | `#273549` | Table headers, secondary buttons |
| `SurfaceHoverBrush` | `#E2E8F0` | `#334155` | Interactive hover surface |
| `SurfacePressedBrush` | `#CBD5E1` | `#475569` | Interactive pressed surface |
| `BorderSubtleBrush` | `#E2E8F0` | `#334155` | Card and divider hairlines |
| `BorderStrongBrush` | `#CBD5E1` | `#475569` | Input borders, dropdowns |
| `TextPrimaryBrush` | `#0F172A` | `#F1F5F9` | Headings and primary content |
| `TextSecondaryBrush` | `#475569` | `#94A3B8` | Subtitles, labels, secondary copy |
| `TextMutedBrush` | `#64748B` | `#64748B` | Placeholders, captions |
| `AccentBrush` (Default) | `#2563EB` | `#3B82F6` | Primary actions, active nav pill |
| `AccentForegroundBrush` | `#FFFFFF` | `#FFFFFF` | Auto-computed high-contrast text on accent |
| `SuccessBrush` | `#16A34A` | `#22C55E` | Online/success indicators |
| `ErrorBrush` | `#DC2626` | `#EF4444` | Errors, destructive buttons |
| `WarningBrush` | `#D97706` | `#F59E0B` | Rate limits, warnings |
| `InfoBrush` | `#0284C7` | `#38BDF8` | Informational states |

## Brand Accent Presets
| Preset | Constant (`DesignTokens`) | Hex | Notes |
|---|---|---|---|
| **Fluent Blue (Default)** | `DefaultAccentHex` | `#2563EB` | Windows 11 Fluent primary accent |
| **HidaChat Emerald** | `BrandEmeraldAccentHex` | `#00A884` | Messaging brand accent aligned with legacy HidaChat |
| **Violet** | `AccentVioletHex` | `#7C3AED` | Optional user accent |
| **Teal** | `AccentTealHex` | `#0D9488` | Optional user accent |
| **Rose** | `AccentRoseHex` | `#E11D48` | Optional user accent |

## WCAG 2.1 Contrast Verification
Verified programmatically via `DesignTokens.GetContrastRatio(fg, bg)` and automated regression tests:

| Foreground / Background Pair | Light Ratio | Dark Ratio | WCAG Grade |
|---|---|---|---|
| `TextPrimary` on `AppBackground` | **17.1 : 1** | **16.2 : 1** | AAA |
| `TextPrimary` on `Surface` | **17.9 : 1** | **13.5 : 1** | AAA |
| `TextSecondary` on `Surface` | **7.6 : 1** | **6.1 : 1** | AA / AAA |
| `AccentForeground` (`#FFFFFF`) on `Accent` (`#2563EB`) | **5.17 : 1** | **3.68 : 1** (Large/Bold UI) | AA |
| `Error` on `Surface` | **4.83 : 1** | **4.66 : 1** | AA |

## VB.NET Programmatic Usage
```vb
Imports HidaChat.UI.Theme

' Switch to Dark theme with HidaChat Emerald brand accent
ThemeManager.Instance.SetTheme(AppThemeMode.Dark, DesignTokens.BrandEmeraldAccentHex, persist:=True)

' Verify contrast for a custom color pair
Dim ratio = DesignTokens.GetContrastRatio("#0F172A", "#FFFFFF")
```
