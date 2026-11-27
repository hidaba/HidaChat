Option Strict On
Option Explicit On

Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Threading

Namespace UI.Controls

    ''' <summary>
    ''' Fluent Design search input with leading search icon, clear button, and optional debounce timer.
    ''' </summary>
    <TemplatePart(Name:="PART_ClearButton", Type:=GetType(Button))>
    Public Class SearchBox
        Inherits TextBox

        Private _clearButton As Button
        Private _debounceTimer As DispatcherTimer

        Public Shared ReadOnly PlaceholderProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Placeholder), GetType(String), GetType(SearchBox),
                                        New PropertyMetadata("Search..."))

        Public Shared ReadOnly DebounceMillisecondsProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(DebounceMilliseconds), GetType(Integer), GetType(SearchBox),
                                        New PropertyMetadata(180))

        Public Property Placeholder As String
            Get
                Return CStr(GetValue(PlaceholderProperty))
            End Get
            Set(value As String)
                SetValue(PlaceholderProperty, value)
            End Set
        End Property

        Public Property DebounceMilliseconds As Integer
            Get
                Return CInt(GetValue(DebounceMillisecondsProperty))
            End Get
            Set(value As Integer)
                SetValue(DebounceMillisecondsProperty, value)
            End Set
        End Property

        Public Event SearchChanged As EventHandler(Of String)

        Public Overrides Sub OnApplyTemplate()
            MyBase.OnApplyTemplate()

            If _clearButton IsNot Nothing Then
                RemoveHandler _clearButton.Click, AddressOf OnClearButtonClick
            End If

            _clearButton = TryCast(GetTemplateChild("PART_ClearButton"), Button)
            If _clearButton IsNot Nothing Then
                AddHandler _clearButton.Click, AddressOf OnClearButtonClick
            End If
        End Sub

        Protected Overrides Sub OnTextChanged(e As TextChangedEventArgs)
            MyBase.OnTextChanged(e)

            If DebounceMilliseconds <= 0 Then
                RaiseEvent SearchChanged(Me, Me.Text)
                Return
            End If

            If _debounceTimer Is Nothing Then
                _debounceTimer = New DispatcherTimer()
                AddHandler _debounceTimer.Tick, AddressOf OnDebounceTick
            End If

            _debounceTimer.Stop()
            _debounceTimer.Interval = TimeSpan.FromMilliseconds(DebounceMilliseconds)
            _debounceTimer.Start()
        End Sub

        Private Sub OnDebounceTick(sender As Object, e As EventArgs)
            If _debounceTimer IsNot Nothing Then
                _debounceTimer.Stop()
            End If
            RaiseEvent SearchChanged(Me, Me.Text)
        End Sub

        Private Sub OnClearButtonClick(sender As Object, e As RoutedEventArgs)
            Me.Text = String.Empty
            Me.Focus()
            RaiseEvent SearchChanged(Me, String.Empty)
        End Sub
    End Class

End Namespace
