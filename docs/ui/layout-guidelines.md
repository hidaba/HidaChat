# Responsive Layout & High DPI Guidelines (WPF)

## 1. Adaptive Layout Architecture
- **Use `Grid` with `*` and `Auto` tracks** as the primary layout container. Avoid `Canvas` or fixed pixel coordinates.
- **Reflowing Groups**: Use `WrapPanel` for action button bars, status badge groups, and icon galleries; use `UniformGrid` for symmetric KPI metric rows.
- **Scroll Protection**: Wrap vertically growing forms and settings panels in `<ScrollViewer VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Disabled">`.
- **Never nest `DataGrid` or `ListView` inside a `StackPanel` or `ScrollViewer`**: Place them in a `Grid` row with `Height="*"` so UI virtualization remains active.

## 2. Minimum Sizes & Compact Resolutions
| Window Category | Recommended Default | Minimum Bounds (`MinWidth` x `MinHeight`) |
|---|---|---|
| Main Application Shell | `1080 x 720` | `800 x 560` |
| Settings / Configuration | `820 x 640` | `700 x 520` |
| Bulk Sender / Data Workspace | `1040 x 740` | `840 x 580` |
| UI Showcase | `1120 x 740` | `820 x 560` |
| Modal Dialogs | `440 x Auto` | `380 x Auto` |

## 3. High DPI Scaling (100% – 200%) & Multi-Monitor
- Always set `UseLayoutRounding="True"` and `SnapsToDevicePixels="True"` on every `Window` root to prevent blurry 1px borders at 125%, 150%, and 175% DPI.
- Use vector `Path` icons (`IconManager`) instead of raster PNGs so iconography stays razor-sharp at 200% DPI.
- When restoring window position across sessions, verify that the target bounds intersect `SystemParameters.VirtualScreen*` so windows never spawn off-screen after disconnecting a monitor.

## 4. Multilingual Expansion Safety
HidaChat supports **English, Italian, French, Spanish, and German**.
- Never set fixed `Width` on text buttons — use `MinWidth` + horizontal `Padding="14,8"` so longer German/French labels never clip.
- Enable `TextWrapping="Wrap"` on all descriptions, subtitles, and dialog bodies.
- Use `TextTrimming="CharacterEllipsis"` with a `ToolTip` on single-line sidebar items and tab headers.

## 5. Automated Verification
Run the static layout and DPI checker from `WhatsAppVB\`:
```powershell
.\scripts\ui\Test-UiLayouts.ps1
```
