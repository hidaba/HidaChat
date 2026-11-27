# Reusable UI Component Catalog (`HidaChat.UI.Controls`)

All controls live in `WhatsAppVB/UI/Controls/` and are styled via `UI/Themes/ModernControls.xaml`.

## 1. `ModernButton`
Supports `Variant` (`Primary`, `Secondary`, `Ghost`, `Danger`), `IconKey` (`HidaChat.UI.Icons.IconKey`), `CornerRadius`, and `IsLoading`.
```xml
<ui:ModernButton Content="Save Changes" Variant="Primary" IconKey="Save" Click="BtnSave_Click"/>
<ui:ModernButton Content="Delete Account" Variant="Danger" IconKey="Trash" Click="BtnDelete_Click"/>
```

## 2. `ModernTextBox`
TextBox with integrated `Label`, `Placeholder`, and inline `ErrorMessage` validation display.
```xml
<ui:ModernTextBox x:Name="TxtServerUrl"
                  Label="Gateway URL"
                  Placeholder="https://node.ts.net"
                  ErrorMessage=""/>
```
```vb
TxtServerUrl.ErrorMessage = "Please enter a valid HTTPS URL."
```

## 3. `ModernComboBox`
Theme-aware Fluent dropdown with rounded popup and accessible item hover/selection states.
```xml
<ui:ModernComboBox SelectedIndex="0">
    <ComboBoxItem Content="System"/>
    <ComboBoxItem Content="Light"/>
    <ComboBoxItem Content="Dark"/>
</ui:ModernComboBox>
```

## 4. `ModernDataGrid`
Pre-configured `DataGrid` with row/column virtualization (`VirtualizationMode.Recycling`), sortable columns, and clean horizontal gridlines.
```xml
<ui:ModernDataGrid x:Name="AccountsGrid" Height="300">
    <DataGrid.Columns>
        <DataGridTextColumn Header="Name" Binding="{Binding Name}" Width="*"/>
        <DataGridTextColumn Header="Platform" Binding="{Binding Platform}" Width="*"/>
    </DataGrid.Columns>
</ui:ModernDataGrid>
```

## 5. `ModernCard`
Surface container with optional `Header`, `Subtitle`, and `HeaderAction` slot.
```xml
<ui:ModernCard Header="Network &amp; Tailscale" Subtitle="Embedded portable tsnet configuration">
    <ui:ModernCard.HeaderAction>
        <ui:StatusBadge Text="Connected" Severity="Success"/>
    </ui:ModernCard.HeaderAction>
    <StackPanel>
        <!-- Card body -->
    </StackPanel>
</ui:ModernCard>
```

## 6. `NavigationSidebar`
Vertical navigation rail bound to `ObservableCollection(Of NavigationItem)` with active accent pill and vector icons.
```vb
Dim items As New ObservableCollection(Of NavigationItem) From {
    New NavigationItem With {.Key = "general", .Title = "General", .IconKey = IconKey.Settings},
    New NavigationItem With {.Key = "accounts", .Title = "Accounts", .IconKey = IconKey.Users}
}
Sidebar.ItemsSource = items
Sidebar.SelectByKey("general")
```

## 7. `SearchBox`
Debounced search input with magnifying glass icon, clear button, and `SearchChanged` event.
```xml
<ui:SearchBox Placeholder="Search contacts..." DebounceMilliseconds="180" SearchChanged="OnSearchChanged"/>
```

## 8. `StatusBadge`
Semantic status pill (`Neutral`, `Info`, `Success`, `Warning`, `Error`).
```xml
<ui:StatusBadge Text="Online" Severity="Success"/>
```

## 9. `ToastNotification`
Non-intrusive notification banner with semantic accent bar, icon, close button, and auto-dismiss timer.
```vb
ToastHost.Show("Settings saved.", SemanticSeverity.Success, "Saved", durationMs:=3000)
```

## 10. `LoadingOverlay`
Translucent backdrop with indeterminate progress bar and status message for async operations.
```vb
LoadingHost.IsLoading = True
Try
    Await Task.Run(AddressOf HeavyWork)
Finally
    LoadingHost.IsLoading = False
End Try
```

## 11. `EmptyState`
Centered empty placeholder with vector icon, title, description, and optional primary CTA button.
```xml
<ui:EmptyState IconKey="Search"
               Title="No results found"
               Description="Try a different search keyword."
               ActionText="Reset Filter"
               ActionClicked="OnResetFilter"/>
```

## 12. `ConfirmDialog` & `DialogManager`
Standardized modal confirmation and alert dialogs with `Enter`/`Escape` keyboard support and destructive styling.
```vb
Dim confirmed = DialogManager.ShowConfirm(
    owner:=Me,
    title:="Clear Cache",
    message:="This will clear temporary Chromium cache files while keeping active logins intact.",
    confirmText:="Clear Cache",
    cancelText:="Cancel",
    isDestructive:=False)
```

## 13. `IconManager`
Provides 30 resolution-independent 24x24 vector SVG `Geometry` icons cached and frozen for zero-overhead rendering.
```vb
Dim geom = IconManager.GetGeometry(IconKey.Shield)
```
