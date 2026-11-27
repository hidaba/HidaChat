Option Strict On
Option Explicit On

Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Threading
Imports HidaChat.UI.Icons

Namespace UI.Controls

    ''' <summary>
    ''' Non-intrusive inline or floating toast notification banner with auto-dismiss timer and semantic styling.
    ''' </summary>
    <TemplatePart(Name:="PART_CloseButton", Type:=GetType(Button))>
    Public Class ToastNotification
        Inherits Control

        Private _closeButton As Button
        Private _dismissTimer As DispatcherTimer

        Public Shared ReadOnly TitleProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Title), GetType(String), GetType(ToastNotification),
                                        New PropertyMetadata(String.Empty))

        Public Shared ReadOnly MessageProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Message), GetType(String), GetType(ToastNotification),
                                        New PropertyMetadata(String.Empty))

        Public Shared ReadOnly SeverityProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Severity), GetType(SemanticSeverity), GetType(ToastNotification),
                                        New PropertyMetadata(SemanticSeverity.Info, AddressOf OnSeverityChanged))

        Public Shared ReadOnly IsOpenProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(IsOpen), GetType(Boolean), GetType(ToastNotification),
                                        New PropertyMetadata(False))

        Public Shared ReadOnly IconGeometryProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(IconGeometry), GetType(Geometry), GetType(ToastNotification),
                                        New PropertyMetadata(Nothing))

        Public Property Title As String
            Get
                Return CStr(GetValue(TitleProperty))
            End Get
            Set(value As String)
                SetValue(TitleProperty, value)
            End Set
        End Property

        Public Property Message As String
            Get
                Return CStr(GetValue(MessageProperty))
            End Get
            Set(value As String)
                SetValue(MessageProperty, value)
            End Set
        End Property

        Public Property Severity As SemanticSeverity
            Get
                Return CType(GetValue(SeverityProperty), SemanticSeverity)
            End Get
            Set(value As SemanticSeverity)
                SetValue(SeverityProperty, value)
            End Set
        End Property

        Public Property IsOpen As Boolean
            Get
                Return CBool(GetValue(IsOpenProperty))
            End Get
            Set(value As Boolean)
                SetValue(IsOpenProperty, value)
            End Set
        End Property

        Public Property IconGeometry As Geometry
            Get
                Return TryCast(GetValue(IconGeometryProperty), Geometry)
            End Get
            Set(value As Geometry)
                SetValue(IconGeometryProperty, value)
            End Set
        End Property

        Public Sub New()
            MyBase.New()
            UpdateIconForSeverity(SemanticSeverity.Info)
        End Sub

        Public Overrides Sub OnApplyTemplate()
            MyBase.OnApplyTemplate()

            If _closeButton IsNot Nothing Then
                RemoveHandler _closeButton.Click, AddressOf OnCloseClick
            End If

            _closeButton = TryCast(GetTemplateChild("PART_CloseButton"), Button)
            If _closeButton IsNot Nothing Then
                AddHandler _closeButton.Click, AddressOf OnCloseClick
            End If
        End Sub

        ''' <summary>
        ''' Displays the toast notification with optional auto-dismiss timeout.
        ''' </summary>
        Public Sub Show(message As String,
                        Optional severity As SemanticSeverity = SemanticSeverity.Info,
                        Optional title As String = "",
                        Optional durationMs As Integer = 3500)
            Me.Title = title
            Me.Message = message
            Me.Severity = severity
            Me.IsOpen = True

            If _dismissTimer IsNot Nothing Then
                _dismissTimer.Stop()
            End If

            If durationMs > 0 Then
                If _dismissTimer Is Nothing Then
                    _dismissTimer = New DispatcherTimer()
                    AddHandler _dismissTimer.Tick, AddressOf OnDismissTick
                End If
                _dismissTimer.Interval = TimeSpan.FromMilliseconds(durationMs)
                _dismissTimer.Start()
            End If
        End Sub

        ''' <summary>
        ''' Hides the toast notification immediately and stops any active dismiss timer.
        ''' </summary>
        Public Sub Hide()
            If _dismissTimer IsNot Nothing Then
                _dismissTimer.Stop()
            End If
            Me.IsOpen = False
        End Sub

        Private Sub OnDismissTick(sender As Object, e As EventArgs)
            Hide()
        End Sub

        Private Sub OnCloseClick(sender As Object, e As RoutedEventArgs)
            Hide()
        End Sub

        Private Shared Sub OnSeverityChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
            Dim toast = TryCast(d, ToastNotification)
            If toast Is Nothing Then Return
            toast.UpdateIconForSeverity(CType(e.NewValue, SemanticSeverity))
        End Sub

        Private Sub UpdateIconForSeverity(sev As SemanticSeverity)
            Select Case sev
                Case SemanticSeverity.Success
                    IconGeometry = IconManager.GetGeometry(IconKey.Check)
                Case SemanticSeverity.Warning
                    IconGeometry = IconManager.GetGeometry(IconKey.Warning)
                Case SemanticSeverity.Error
                    IconGeometry = IconManager.GetGeometry(IconKey.Error)
                Case Else
                    IconGeometry = IconManager.GetGeometry(IconKey.Info)
            End Select
        End Sub
    End Class

End Namespace
