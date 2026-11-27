Option Strict On
Option Explicit On

Imports System.IO
Imports System.Text.Json
Imports System.Windows
Imports Microsoft.Win32

Namespace UI.Theme

    ''' <summary>
    ''' Event payload raised whenever the effective theme or accent color changes.
    ''' </summary>
    Public NotInheritable Class ThemeChangedEventArgs
        Inherits EventArgs

        Public ReadOnly Property Mode As AppThemeMode
        Public ReadOnly Property IsDark As Boolean
        Public ReadOnly Property AccentHex As String
        Public ReadOnly Property Palette As ThemePalette

        Public Sub New(mode As AppThemeMode, isDark As Boolean, accentHex As String, palette As ThemePalette)
            Me.Mode = mode
            Me.IsDark = isDark
            Me.AccentHex = accentHex
            Me.Palette = palette
        End Sub
    End Class

    ''' <summary>
    ''' Serializable portable theme preference stored in data/ui_theme.json.
    ''' </summary>
    Public NotInheritable Class ThemePreferenceData
        Public Property Mode As String = "System"
        Public Property AccentHex As String = DesignTokens.DefaultAccentHex
    End Class

    ''' <summary>
    ''' Centralized WPF Theme Manager supporting Light, Dark, and System-auto modes,
    ''' customizable accent colors, portable JSON persistence, and live DynamicResource updates.
    ''' </summary>
    Public NotInheritable Class ThemeManager
        Private Shared ReadOnly _lazyInstance As New Lazy(Of ThemeManager)(Function() New ThemeManager())

        Public Shared ReadOnly Property Instance As ThemeManager
            Get
                Return _lazyInstance.Value
            End Get
        End Property

        Private ReadOnly _registeredWindows As New List(Of WeakReference(Of Window))()
        Private ReadOnly _syncLock As New Object()

        Private _mode As AppThemeMode = AppThemeMode.System
        Private _accentHex As String = DesignTokens.DefaultAccentHex
        Private _currentPalette As ThemePalette

        Public Event ThemeChanged As EventHandler(Of ThemeChangedEventArgs)

        Public ReadOnly Property CurrentMode As AppThemeMode
            Get
                Return _mode
            End Get
        End Property

        Public ReadOnly Property CurrentAccentHex As String
            Get
                Return _accentHex
            End Get
        End Property

        Public ReadOnly Property IsDarkEffective As Boolean
            Get
                Select Case _mode
                    Case AppThemeMode.Dark
                        Return True
                    Case AppThemeMode.Light
                        Return False
                    Case Else
                        Return IsWindowsDarkModeEnabled()
                End Select
            End Get
        End Property

        Public ReadOnly Property CurrentPalette As ThemePalette
            Get
                If _currentPalette Is Nothing Then
                    _currentPalette = DesignTokens.GetPalette(IsDarkEffective, _accentHex)
                End If
                Return _currentPalette
            End Get
        End Property

        ''' <summary>
        ''' Portable configuration file path strictly inside the application base directory.
        ''' </summary>
        Public Shared ReadOnly Property PortableThemeConfigPath As String
            Get
                Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "ui_theme.json")
            End Get
        End Property

        Private Sub New()
            LoadPreferences()
            _currentPalette = DesignTokens.GetPalette(IsDarkEffective, _accentHex)
            Try
                AddHandler SystemEvents.UserPreferenceChanged, AddressOf OnSystemUserPreferenceChanged
            Catch
                ' Non-fatal in restricted or design-time environments
            End Try
        End Sub

        ''' <summary>
        ''' Detects whether Windows AppsUseLightTheme is disabled (Dark Mode active).
        ''' </summary>
        Public Shared Function IsWindowsDarkModeEnabled() As Boolean
            Try
                Using key = Registry.CurrentUser.OpenSubKey("Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")
                    If key IsNot Nothing Then
                        Dim value = key.GetValue("AppsUseLightTheme")
                        If value IsNot Nothing Then
                            Return Convert.ToInt32(value) = 0
                        End If
                    End If
                End Using
            Catch
            End Try
            Return False
        End Function

        ''' <summary>
        ''' Registers a WPF Window for automatic live theme updates and applies the current palette immediately.
        ''' </summary>
        Public Sub RegisterWindow(window As Window)
            If window Is Nothing Then Return

            SyncLock _syncLock
                PruneDeadWindowsNoLock()
                Dim alreadyRegistered = _registeredWindows.Any(Function(wr)
                                                                   Dim target As Window = Nothing
                                                                   Return wr.TryGetTarget(target) AndAlso Object.ReferenceEquals(target, window)
                                                               End Function)
                If Not alreadyRegistered Then
                    _registeredWindows.Add(New WeakReference(Of Window)(window))
                End If
            End SyncLock

            ApplyTheme(window)
        End Sub

        ''' <summary>
        ''' Unregisters a WPF Window (recommended in Window.OnClosed to avoid unnecessary references).
        ''' </summary>
        Public Sub UnregisterWindow(window As Window)
            If window Is Nothing Then Return

            SyncLock _syncLock
                _registeredWindows.RemoveAll(Function(wr)
                                                 Dim target As Window = Nothing
                                                 Return Not wr.TryGetTarget(target) OrElse Object.ReferenceEquals(target, window)
                                             End Function)
            End SyncLock
        End Sub

        ''' <summary>
        ''' Updates the active theme mode and optional accent color, refreshing all registered windows live.
        ''' </summary>
        Public Sub SetTheme(mode As AppThemeMode, Optional accentHex As String = Nothing, Optional persist As Boolean = True)
            _mode = mode
            If Not String.IsNullOrWhiteSpace(accentHex) Then
                _accentHex = DesignTokens.NormalizeAccentHex(accentHex)
            End If

            _currentPalette = DesignTokens.GetPalette(IsDarkEffective, _accentHex)

            If persist Then
                SavePreferences()
            End If

            RefreshAllWindows()
            RaiseEvent ThemeChanged(Me, New ThemeChangedEventArgs(_mode, _currentPalette.IsDark, _accentHex, _currentPalette))
        End Sub

        ''' <summary>
        ''' Updates only the accent color while keeping the current Light/Dark/System mode.
        ''' </summary>
        Public Sub SetAccentColor(accentHex As String, Optional persist As Boolean = True)
            SetTheme(_mode, accentHex, persist)
        End Sub

        ''' <summary>
        ''' Applies the current semantic palette brushes to a specific window's ResourceDictionary.
        ''' </summary>
        Public Sub ApplyTheme(window As Window)
            If window Is Nothing Then Return
            DesignTokens.ApplyPaletteToResources(window.Resources, CurrentPalette)
        End Sub

        ''' <summary>
        ''' Refreshes all registered windows on their UI dispatchers.
        ''' </summary>
        Public Sub RefreshAllWindows()
            Dim targets As New List(Of Window)()
            SyncLock _syncLock
                PruneDeadWindowsNoLock()
                For Each wr In _registeredWindows
                    Dim win As Window = Nothing
                    If wr.TryGetTarget(win) Then
                        targets.Add(win)
                    End If
                Next
            End SyncLock

            For Each win In targets
                If win.Dispatcher.CheckAccess() Then
                    ApplyTheme(win)
                Else
                    win.Dispatcher.BeginInvoke(Sub() ApplyTheme(win))
                End If
            Next
        End Sub

        ''' <summary>
        ''' Loads persisted theme preferences from data/ui_theme.json if present.
        ''' </summary>
        Public Sub LoadPreferences()
            Try
                Dim path = PortableThemeConfigPath
                If Not File.Exists(path) Then Return

                Dim json = File.ReadAllText(path)
                Dim data = JsonSerializer.Deserialize(Of ThemePreferenceData)(json)
                If data Is Nothing Then Return

                Dim parsedMode As AppThemeMode
                If [Enum].TryParse(Of AppThemeMode)(data.Mode, ignoreCase:=True, result:=parsedMode) Then
                    _mode = parsedMode
                End If
                If DesignTokens.IsValidHexColor(data.AccentHex) Then
                    _accentHex = DesignTokens.NormalizeAccentHex(data.AccentHex)
                End If
            Catch
                ' Fallback safely to defaults if config is corrupted
            End Try
        End Sub

        ''' <summary>
        ''' Persists current theme preferences portably inside data/ui_theme.json.
        ''' </summary>
        Public Sub SavePreferences()
            Try
                Dim path = PortableThemeConfigPath
                Dim dir = IO.Path.GetDirectoryName(path)
                If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then
                    Directory.CreateDirectory(dir)
                End If

                Dim data As New ThemePreferenceData With {
                    .Mode = _mode.ToString(),
                    .AccentHex = _accentHex
                }
                Dim json = JsonSerializer.Serialize(data, New JsonSerializerOptions With {.WriteIndented = True})
                File.WriteAllText(path, json)
            Catch
                ' Non-fatal on read-only media
            End Try
        End Sub

        Private Sub OnSystemUserPreferenceChanged(sender As Object, e As UserPreferenceChangedEventArgs)
            If _mode <> AppThemeMode.System Then Return
            If e.Category = UserPreferenceCategory.General OrElse e.Category = UserPreferenceCategory.Color Then
                _currentPalette = DesignTokens.GetPalette(IsDarkEffective, _accentHex)
                RefreshAllWindows()
                RaiseEvent ThemeChanged(Me, New ThemeChangedEventArgs(_mode, _currentPalette.IsDark, _accentHex, _currentPalette))
            End If
        End Sub

        Private Sub PruneDeadWindowsNoLock()
            _registeredWindows.RemoveAll(Function(wr)
                                             Dim target As Window = Nothing
                                             Return Not wr.TryGetTarget(target)
                                         End Function)
        End Sub
    End Class

End Namespace
