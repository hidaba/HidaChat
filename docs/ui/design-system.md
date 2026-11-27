# HidaChat Fluent Design System

## Overview
HidaChat's modern UI architecture is a lightweight, zero-bloat **Windows 11 Fluent Design System** built natively for **VB.NET WPF (.NET 9)**. It provides a single source of truth for typography, colors, spacing, corner radii, vector icons, and reusable controls while preserving 100% application portability and Visual Studio XAML Designer compatibility.

## Architecture
```text
WhatsAppVB/
├── UI/
│   ├── Theme/
│   │   ├── DesignTokens.vb        # Palette definitions, typography, spacing, WCAG contrast engine
│   │   └── ThemeManager.vb        # Light/Dark/System engine, custom accent, portable data/ui_theme.json
│   ├── Icons/
│   │   └── IconManager.vb         # 30 cached frozen 24x24 vector SVG Path geometries
│   ├── Themes/
│   │   ├── FluentLight.xaml       # Light semantic brush dictionary
│   │   ├── FluentDark.xaml        # Dark semantic brush dictionary
│   │   └── ModernControls.xaml    # ControlTemplates for all HidaChat.UI.Controls
│   ├── Controls/                  # 12 standardized VB.NET WPF controls + DialogManager
│   └── Showcase/
│       ├── UiShowcaseWindow.xaml  # Interactive reference window (--ui-showcase)
│       └── UiShowcaseWindow.xaml.vb
```

## Typography Scale (`Segoe UI`)
| Token (`DesignTokens`) | Size | Weight | Usage |
|---|---|---|---|
| `FontSizeDisplay` | `22 px` | Bold | Hero headings, KPI values |
| `FontSizeTitle` | `18 px` | SemiBold | Page and window headers |
| `FontSizeSubtitle` | `16 px` | SemiBold | Card headers, modal titles |
| `FontSizeBodyLarge` | `15 px` | Regular/SemiBold | Emphasized body content |
| `FontSizeBody` | `14 px` (~10.5 pt) | Regular | Primary text, inputs, labels |
| `FontSizeCaption` | `12 px` (~9 pt) | Regular/SemiBold | Hints, metadata, badges |

Monospace font (`FontFamilyMonospace`): `Cascadia Code, Consolas, Courier New` (used for custom CSS editors, logs, and technical IDs).

## Spacing & Corner Radius Tokens
- **Spacing**: `SpaceXs = 4`, `SpaceSm = 8`, `SpaceMd = 12`, `SpaceLg = 16`, `SpaceXl = 24`, `Space2Xl = 32`.
- **Corner Radius**: `RadiusSm = 6` (buttons, inputs, nav items), `RadiusMd = 8` (cards, dialogs, toasts), `RadiusLg = 12` (outer windows), `RadiusPill = 999` (status badges).

## Integrating a Window with the Design System

### 1. XAML Resource Dictionary Merge
```xml
<Window.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="/HidaChat;component/UI/Themes/FluentDark.xaml"/>
            <ResourceDictionary Source="/HidaChat;component/UI/Themes/ModernControls.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Window.Resources>
```

### 2. VB.NET Live Theme Registration
```vb
Imports HidaChat.UI.Theme

Public Class MyModernWindow
    Inherits Window

    Public Sub New()
        InitializeComponent()
        ThemeManager.Instance.RegisterWindow(Me)
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        ThemeManager.Instance.UnregisterWindow(Me)
        MyBase.OnClosed(e)
    End Sub
End Class
```

### 3. Launching the Interactive UI Showcase
Run the compiled application with the `--ui-showcase` flag:
```powershell
.\bin\Debug\net9.0-windows10.0.19041.0\HidaChat.exe --ui-showcase
```
