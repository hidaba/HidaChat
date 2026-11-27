Option Strict On
Option Explicit On

Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Threading.Tasks
Imports System.Windows
Imports System.Windows.Data
Imports System.Windows.Media
Imports HidaChat.UI.Controls
Imports HidaChat.UI.Icons
Imports HidaChat.UI.Theme

Namespace UI.Showcase

    ''' <summary>
    ''' Sample data row for the UI Showcase DataGrid demonstration.
    ''' </summary>
    Public NotInheritable Class ShowcaseAccountRow
        Public Property Name As String = String.Empty
        Public Property Platform As String = String.Empty
        Public Property Endpoint As String = String.Empty
        Public Property Status As String = String.Empty
        Public Property Latency As String = String.Empty
    End Class

    ''' <summary>
    ''' Sample bar item for the lightweight Fluent bar chart in the UI Showcase.
    ''' </summary>
    Public NotInheritable Class ShowcaseChartBar
        Public Property Label As String = String.Empty
        Public Property Value As Integer
        Public Property BarHeight As Double
        Public Property TooltipText As String = String.Empty
    End Class

    ''' <summary>
    ''' View item for the IconManager gallery in the UI Showcase.
    ''' </summary>
    Public NotInheritable Class ShowcaseIconItem
        Public Property Name As String = String.Empty
        Public Property Geometry As Geometry
    End Class

    ''' <summary>
    ''' Interactive reference window showcasing HidaChat's Fluent Design System,
    ''' reusable VB.NET WPF components, live Light/Dark/System themes, and accent colors.
    ''' </summary>
    Public Class UiShowcaseWindow
        Inherits Window

        Private ReadOnly _navItems As New ObservableCollection(Of NavigationItem)()
        Private ReadOnly _sampleRows As New ObservableCollection(Of ShowcaseAccountRow)()
        Private _rowsView As ICollectionView

        Public Sub New()
            InitializeComponent()
            ThemeManager.Instance.RegisterWindow(Me)
            AddHandler ThemeManager.Instance.ThemeChanged, AddressOf OnThemeChanged

            InitializeNavigation()
            InitializeSampleData()
            InitializeIconGallery()
            UpdateContrastMetric()
        End Sub

        Protected Overrides Sub OnClosed(e As EventArgs)
            RemoveHandler ThemeManager.Instance.ThemeChanged, AddressOf OnThemeChanged
            ThemeManager.Instance.UnregisterWindow(Me)
            MyBase.OnClosed(e)
        End Sub

        Private Sub InitializeNavigation()
            _navItems.Add(New NavigationItem With {.Key = "overview", .Title = "Overview & Dashboard", .IconKey = IconKey.Dashboard})
            _navItems.Add(New NavigationItem With {.Key = "controls", .Title = "Buttons & Inputs", .IconKey = IconKey.Edit})
            _navItems.Add(New NavigationItem With {.Key = "table", .Title = "Data Grid & Search", .IconKey = IconKey.Filter, .BadgeText = "6"})
            _navItems.Add(New NavigationItem With {.Key = "icons", .Title = "Vector Icons", .IconKey = IconKey.Palette, .BadgeText = IconManager.GetAllIconKeys().Count.ToString()})
            _navItems.Add(New NavigationItem With {.Key = "feedback", .Title = "Feedback & Dialogs", .IconKey = IconKey.Bell})

            Sidebar.ItemsSource = _navItems
            Sidebar.SelectByKey("overview")
        End Sub

        Private Sub InitializeSampleData()
            _sampleRows.Add(New ShowcaseAccountRow With {.Name = "Work WhatsApp", .Platform = "WhatsApp", .Endpoint = "https://web.whatsapp.com", .Status = "Online", .Latency = "42 ms"})
            _sampleRows.Add(New ShowcaseAccountRow With {.Name = "Personal Telegram", .Platform = "Telegram", .Endpoint = "https://web.telegram.org/a/", .Status = "Online", .Latency = "31 ms"})
            _sampleRows.Add(New ShowcaseAccountRow With {.Name = "Community Discord", .Platform = "Discord", .Endpoint = "https://discord.com/app", .Status = "Online", .Latency = "58 ms"})
            _sampleRows.Add(New ShowcaseAccountRow With {.Name = "Local OpenClaw", .Platform = "OpenClaw", .Endpoint = "http://127.0.0.1:18800", .Status = "Online", .Latency = "4 ms"})
            _sampleRows.Add(New ShowcaseAccountRow With {.Name = "Studio Hermes", .Platform = "Hermes Agent", .Endpoint = "https://hermes.studio.ts.net", .Status = "Tailscale", .Latency = "19 ms"})
            _sampleRows.Add(New ShowcaseAccountRow With {.Name = "Archive Account", .Platform = "WhatsApp", .Endpoint = "https://web.whatsapp.com", .Status = "Hibernated", .Latency = "—"})

            ShowcaseGrid.ItemsSource = _sampleRows
            _rowsView = CollectionViewSource.GetDefaultView(_sampleRows)

            Dim weeklyValues = New(String, Integer)() {
                ("Mon", 140), ("Tue", 210), ("Wed", 185), ("Thu", 260), ("Fri", 295), ("Sat", 110), ("Sun", 84)
            }
            Dim maxVal = 300.0
            Dim bars As New ObservableCollection(Of ShowcaseChartBar)()
            For Each entry In weeklyValues
                bars.Add(New ShowcaseChartBar With {
                    .Label = entry.Item1,
                    .Value = entry.Item2,
                    .BarHeight = Math.Max(14.0, (entry.Item2 / maxVal) * 120.0),
                    .TooltipText = $"{entry.Item1}: {entry.Item2} messages"
                })
            Next
            ChartItems.ItemsSource = bars
        End Sub

        Private Sub InitializeIconGallery()
            Dim items As New ObservableCollection(Of ShowcaseIconItem)()
            For Each key In IconManager.GetAllIconKeys()
                items.Add(New ShowcaseIconItem With {
                    .Name = key.ToString(),
                    .Geometry = IconManager.GetGeometry(key)
                })
            Next
            IconGallery.ItemsSource = items
        End Sub

        Private Sub Sidebar_NavigationSelected(sender As Object, e As NavigationSelectionChangedEventArgs)
            SectionOverview.Visibility = Visibility.Collapsed
            SectionControls.Visibility = Visibility.Collapsed
            SectionTable.Visibility = Visibility.Collapsed
            SectionIcons.Visibility = Visibility.Collapsed
            SectionFeedback.Visibility = Visibility.Collapsed

            Select Case e.SelectedItem.Key
                Case "overview"
                    SectionOverview.Visibility = Visibility.Visible
                    TxtPageTitle.Text = "Overview & Dashboard"
                    TxtPageSubtitle.Text = "KPI cards, lightweight vector chart, typography scale, and WCAG contrast verification"
                Case "controls"
                    SectionControls.Visibility = Visibility.Visible
                    TxtPageTitle.Text = "Buttons & Form Controls"
                    TxtPageSubtitle.Text = "Interactive button variants, ModernTextBox validation, ModernComboBox, and StatusBadges"
                Case "table"
                    SectionTable.Visibility = Visibility.Visible
                    TxtPageTitle.Text = "Data Grid & Search"
                    TxtPageSubtitle.Text = "Virtualized ModernDataGrid with column sorting, instant SearchBox filtering, and EmptyState"
                Case "icons"
                    SectionIcons.Visibility = Visibility.Visible
                    TxtPageTitle.Text = "Vector Icon Library"
                    TxtPageSubtitle.Text = "Frozen WPF Geometry paths from IconManager — crisp at 100% to 200% DPI"
                Case "feedback"
                    SectionFeedback.Visibility = Visibility.Visible
                    TxtPageTitle.Text = "Feedback, Dialogs & States"
                    TxtPageSubtitle.Text = "Non-intrusive ToastNotification, modal ConfirmDialog, LoadingOverlay, and EmptyState"
            End Select
        End Sub

        Private Sub OnThemeChanged(sender As Object, e As ThemeChangedEventArgs)
            UpdateContrastMetric()
        End Sub

        Private Sub UpdateContrastMetric()
            Dim palette = ThemeManager.Instance.CurrentPalette
            Dim ratio = DesignTokens.GetContrastRatio(palette.TextPrimary, palette.Surface)
            TxtContrastMetric.Text = $"{ratio:0.0} : 1"
        End Sub

        Private Sub BtnThemeLight_Click(sender As Object, e As RoutedEventArgs)
            ThemeManager.Instance.SetTheme(AppThemeMode.Light, persist:=True)
            ShowcaseToast.Show("Switched to Light theme.", SemanticSeverity.Info, "Theme Updated")
        End Sub

        Private Sub BtnThemeDark_Click(sender As Object, e As RoutedEventArgs)
            ThemeManager.Instance.SetTheme(AppThemeMode.Dark, persist:=True)
            ShowcaseToast.Show("Switched to Dark theme.", SemanticSeverity.Info, "Theme Updated")
        End Sub

        Private Sub BtnThemeSystem_Click(sender As Object, e As RoutedEventArgs)
            ThemeManager.Instance.SetTheme(AppThemeMode.System, persist:=True)
            ShowcaseToast.Show("Synchronized with Windows system theme.", SemanticSeverity.Info, "Theme Updated")
        End Sub

        Private Sub BtnAccentBlue_Click(sender As Object, e As RoutedEventArgs)
            ThemeManager.Instance.SetAccentColor(DesignTokens.DefaultAccentHex, persist:=True)
        End Sub

        Private Sub BtnAccentEmerald_Click(sender As Object, e As RoutedEventArgs)
            ThemeManager.Instance.SetAccentColor(DesignTokens.BrandEmeraldAccentHex, persist:=True)
        End Sub

        Private Sub BtnAccentViolet_Click(sender As Object, e As RoutedEventArgs)
            ThemeManager.Instance.SetAccentColor(DesignTokens.AccentVioletHex, persist:=True)
        End Sub

        Private Sub BtnAccentRose_Click(sender As Object, e As RoutedEventArgs)
            ThemeManager.Instance.SetAccentColor(DesignTokens.AccentRoseHex, persist:=True)
        End Sub

        Private Sub BtnValidateInputs_Click(sender As Object, e As RoutedEventArgs)
            Dim hasErrors As Boolean = False

            If String.IsNullOrWhiteSpace(TxtSampleName.Text) Then
                TxtSampleName.ErrorMessage = "Display name is required."
                hasErrors = True
            Else
                TxtSampleName.ErrorMessage = String.Empty
            End If

            Dim url = TxtSampleEmail.Text.Trim()
            If Not url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) Then
                TxtSampleEmail.ErrorMessage = "Please enter a valid HTTPS URL starting with https://."
                hasErrors = True
            Else
                TxtSampleEmail.ErrorMessage = String.Empty
            End If

            If hasErrors Then
                ShowcaseToast.Show("Please correct the highlighted fields.", SemanticSeverity.Warning, "Validation")
            Else
                ShowcaseToast.Show("All form inputs are valid.", SemanticSeverity.Success, "Validation Passed")
            End If
        End Sub

        Private Sub TableSearchBox_SearchChanged(sender As Object, query As String)
            If _rowsView Is Nothing Then Return
            Dim q = If(query, String.Empty).Trim()
            _rowsView.Filter = Function(itemObj)
                                   If String.IsNullOrEmpty(q) Then Return True
                                   Dim row = TryCast(itemObj, ShowcaseAccountRow)
                                   If row Is Nothing Then Return False
                                   Return row.Name.Contains(q, StringComparison.OrdinalIgnoreCase) OrElse
                                          row.Platform.Contains(q, StringComparison.OrdinalIgnoreCase) OrElse
                                          row.Status.Contains(q, StringComparison.OrdinalIgnoreCase)
                               End Function
            _rowsView.Refresh()

            Dim isEmpty = _rowsView.IsEmpty
            ShowcaseGrid.Visibility = If(isEmpty, Visibility.Collapsed, Visibility.Visible)
            TableEmptyState.Visibility = If(isEmpty, Visibility.Visible, Visibility.Collapsed)
        End Sub

        Private Sub TableEmptyState_ActionClicked(sender As Object, e As RoutedEventArgs)
            TableSearchBox.Text = String.Empty
        End Sub

        Private Sub BtnSampleToastInfo_Click(sender As Object, e As RoutedEventArgs)
            ShowcaseToast.Show("Configuration synchronized with portable storage.", SemanticSeverity.Info, "Information")
        End Sub

        Private Sub BtnSampleToastSuccess_Click(sender As Object, e As RoutedEventArgs)
            ShowcaseToast.Show("Changes saved successfully.", SemanticSeverity.Success, "Success")
        End Sub

        Private Sub BtnSampleToastWarning_Click(sender As Object, e As RoutedEventArgs)
            ShowcaseToast.Show("Rate limit approaching; switching to backup endpoint.", SemanticSeverity.Warning, "Warning")
        End Sub

        Private Sub BtnSampleToastError_Click(sender As Object, e As RoutedEventArgs)
            ShowcaseToast.Show("Unable to reach remote gateway. Check Tailscale status.", SemanticSeverity.Error, "Connection Error")
        End Sub

        Private Sub BtnConfirmStandard_Click(sender As Object, e As RoutedEventArgs)
            Dim ok = DialogManager.ShowConfirm(Me,
                                               "Apply Theme Preset",
                                               "Apply the selected Fluent Design tokens to all registered windows?",
                                               "Apply",
                                               "Cancel",
                                               isDestructive:=False)
            If ok Then
                ShowcaseToast.Show("Confirmation accepted.", SemanticSeverity.Success)
            End If
        End Sub

        Private Sub BtnConfirmDestructive_Click(sender As Object, e As RoutedEventArgs)
            Dim ok = DialogManager.ShowConfirm(Me,
                                               "Delete Account Profile",
                                               "This will permanently remove the selected profile and local cache. This operation cannot be undone.",
                                               "Delete Profile",
                                               "Cancel",
                                               isDestructive:=True)
            If ok Then
                ShowcaseToast.Show("Destructive action confirmed (demo only).", SemanticSeverity.Warning)
            End If
        End Sub

        Private Async Sub BtnSimulateLoading_Click(sender As Object, e As RoutedEventArgs)
            ShowcaseLoading.IsLoading = True
            Try
                Await Task.Delay(1500)
            Finally
                ShowcaseLoading.IsLoading = False
            End Try
            ShowcaseToast.Show("Async background task completed without blocking the UI thread.", SemanticSeverity.Success, "Done")
        End Sub
    End Class

End Namespace
