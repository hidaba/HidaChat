# Accessibility & Keyboard Navigation Guide

## 1. Keyboard Accessibility
- **Never disable focus on interactive controls**: Avoid `Focusable="False"` on buttons, tabs, and navigation items. Every interactive control must be reachable via `Tab` / `Shift+Tab` and activatable with `Enter` or `Space`.
- **Visible Focus Ring**: All `HidaChat.UI.Controls` render a 2px `{DynamicResource FocusBrush}` border when `IsKeyboardFocused="True"`.
- **Dialog Shortcuts**: All modal dialogs (`ConfirmDialog`) wire `IsDefault="True"` on the primary confirm button (`Enter`) and `IsCancel="True"` on the cancel button (`Escape`).

## 2. Color Contrast (WCAG 2.1 AA / AAA)
- Body text (`14px`): minimum **4.5:1** contrast ratio against its surface.
- Large headings (`>= 18px` or `>= 14px` bold) and UI component borders/icons: minimum **3.0:1**.
- Programmatic verification: use `DesignTokens.GetContrastRatio(fgHex, bgHex)` and `DesignTokens.GetAccessibleForegroundHex(bgHex)` whenever computing dynamic colors.

## 3. Screen Readers & UI Automation (FlaUI)
- Provide `AutomationProperties.Name` and `ToolTip` on every icon-only button (e.g. Close, Clear Search, Accent Swatches).
- Pair every form input with a visible `Label` (`ModernTextBox.Label`) rather than relying solely on placeholder text.
- Never convey status by color alone — pair color with explicit text (`StatusBadge.Text`) and semantic icons (`ToastNotification`).
