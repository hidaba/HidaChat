# UI Development & Review Checklist

Use this checklist before completing any UI change in HidaChat:

## Design System & Theming
- [ ] Consulted relevant skills in `.agents/skills/vbnet-*`.
- [ ] Zero hardcoded `#RRGGBB` colors in new XAML or VB.NET code-behind.
- [ ] All theme brushes use `{DynamicResource ...}` (never `{StaticResource ...}`).
- [ ] Verified live switching between **Light**, **Dark**, and **System** themes plus custom accents.
- [ ] Verified WCAG contrast (>= 4.5:1 body text, >= 3.0:1 UI elements).

## Controls & Layout
- [ ] Reused standardized controls from `HidaChat.UI.Controls` and icons from `IconManager`.
- [ ] Root `Window` sets `UseLayoutRounding="True"`, `SnapsToDevicePixels="True"`, and sensible `MinWidth`/`MinHeight`.
- [ ] No fixed pixel widths on localized text buttons; text wraps or trims gracefully.
- [ ] Verified at 100%, 125%, 150%, 175%, and 200% DPI scaling without overlap or clipping.
- [ ] Verified Visual Studio Designer compatibility (`DesignerProperties.GetIsInDesignMode` guards).

## UX, Accessibility & Performance
- [ ] Keyboard navigation (`Tab`, `Enter`, `Escape`) works across all controls (no `Focusable="False"` on buttons).
- [ ] Destructive actions require `DialogManager.ShowConfirm(..., isDestructive:=True)`.
- [ ] Long operations (> 250ms) run asynchronously off the UI thread with `LoadingOverlay`.
- [ ] Static event handlers (`ThemeManager.Instance.ThemeChanged`) unsubscribed in `OnClosed`.
- [ ] All user-facing strings localized across all 5 languages (`en`, `it`, `fr`, `es`, `de`) in `Localization.vb`.

## Verification
- [ ] `dotnet build HidaChat.vbproj` succeeds with 0 warnings, 0 errors.
- [ ] `dotnet run --project Tests/HidaChat.SessionTests.vbproj` passes all assertions.
- [ ] Ran `.\scripts\ui\Test-UiBuildAndRegression.ps1`.
