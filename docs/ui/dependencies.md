# UI Dependencies, Library Evaluation & Licenses

## 1. Current Project Dependencies (`HidaChat.vbproj`)
Target framework: **`net9.0-windows10.0.19041.0`** (WPF + minimal WinForms for `NotifyIcon`).

| Package | Version | License | Purpose |
|---|---|---|---|
| `ExcelDataReader` | `3.9.0` | MIT | Bulk sender Excel workbook parsing |
| `ExcelDataReader.DataSet` | `3.9.0` | MIT | DataSet adapter for Excel contacts |
| `Microsoft.Toolkit.Uwp.Notifications` | `7.1.3` | MIT | Native Windows toast notifications |
| `Microsoft.Web.WebView2` | `1.0.4078.44` | BSD-3-Clause / Microsoft Software License | Embedded Chromium web sessions |

## 2. WPF UI Library Evaluation

| Candidate | License | Pros | Cons | Decision |
|---|---|---|---|---|
| **Native WPF + HidaChat `UI/` Design System** | Apache-2.0 (in-repo) | Zero DLL bloat in portable ZIP; 100% Visual Studio Designer stability; exact Fluent tokens; zero regression risk on existing 10 windows | Custom templates maintained in `UI/Themes/ModernControls.xaml` | **Selected Primary Foundation** |
| **WPF-UI (`WPF-UI` 4.x by Lepo.co)** | MIT | Authentic Win11 Mica/Acrylic backdrop, `FluentWindow`, `NavigationView`, `SymbolIcon`, `ContentDialog` | ~2.5 MB extra assemblies; global dictionary merge requires careful isolation from legacy windows | **Approved Optional Extension** for Phase 2/3 when Win11 Mica chrome is desired |
| **MahApps.Metro (2.4.x)** | MIT | Mature, well-documented WPF control suite | Metro/Win10 visual language rather than Win11 Fluent; heavier footprint (~4 MB) | **Not Selected** |

## 3. Charting & Automated UI Testing Evaluation

| Category | Candidate | License | Verdict |
|---|---|---|---|
| **Lightweight KPI Charts** | Native WPF Vector Bars (in `UiShowcaseWindow`) | Apache-2.0 | **Selected Default** (0 KB overhead) |
| **High-Volume Telemetry Charts** | `ScottPlot.WPF` (5.x) | MIT | **Recommended** if diagnostic graphs (>10k points) are added |
| **Interactive Business Charts** | `LiveChartsCore.SkiaSharpView.WPF` (2.x) | MIT | **Recommended** if animated pie/line/gauge dashboards are added |
| **Automated Desktop UI Tests** | `FlaUI.UIA3` (4.x) | MIT | **Recommended** for end-to-end WPF UIA3 automation in a dedicated `Tests/HidaChat.UiTests` project (never bundled into the portable release ZIP) |

## Governance Rule
Commercial or proprietary UI component suites (e.g., Telerik, DevExpress, Syncfusion, Infragistics) MUST NOT be added without explicit written authorization.
