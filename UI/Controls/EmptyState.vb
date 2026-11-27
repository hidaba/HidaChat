Option Strict On
Option Explicit On

Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports HidaChat.UI.Icons

Namespace UI.Controls

    ''' <summary>
    ''' Centered empty-state placeholder with vector icon, title, description, and optional primary CTA button.
    ''' </summary>
    <TemplatePart(Name:="PART_ActionButton", Type:=GetType(Button))>
    Public Class EmptyState
        Inherits Control

        Private _actionButton As Button

        Public Shared ReadOnly IconKeyProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(IconKey), GetType(IconKey), GetType(EmptyState),
                                        New PropertyMetadata(IconKey.Folder, AddressOf OnIconKeyChanged))

        Public Shared ReadOnly IconGeometryProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(IconGeometry), GetType(Geometry), GetType(EmptyState),
                                        New PropertyMetadata(Nothing))

        Public Shared ReadOnly TitleProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Title), GetType(String), GetType(EmptyState),
                                        New PropertyMetadata("Nothing here yet"))

        Public Shared ReadOnly DescriptionProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Description), GetType(String), GetType(EmptyState),
                                        New PropertyMetadata(String.Empty))

        Public Shared ReadOnly ActionTextProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(ActionText), GetType(String), GetType(EmptyState),
                                        New PropertyMetadata(String.Empty))

        Public Property IconKey As IconKey
            Get
                Return CType(GetValue(IconKeyProperty), IconKey)
            End Get
            Set(value As IconKey)
                SetValue(IconKeyProperty, value)
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

        Public Property Title As String
            Get
                Return CStr(GetValue(TitleProperty))
            End Get
            Set(value As String)
                SetValue(TitleProperty, value)
            End Set
        End Property

        Public Property Description As String
            Get
                Return CStr(GetValue(DescriptionProperty))
            End Get
            Set(value As String)
                SetValue(DescriptionProperty, value)
            End Set
        End Property

        Public Property ActionText As String
            Get
                Return CStr(GetValue(ActionTextProperty))
            End Get
            Set(value As String)
                SetValue(ActionTextProperty, value)
            End Set
        End Property

        Public Event ActionClicked As RoutedEventHandler

        Public Sub New()
            MyBase.New()
            IconGeometry = IconManager.GetGeometry(IconKey.Folder)
        End Sub

        Public Overrides Sub OnApplyTemplate()
            MyBase.OnApplyTemplate()

            If _actionButton IsNot Nothing Then
                RemoveHandler _actionButton.Click, AddressOf OnActionButtonClick
            End If

            _actionButton = TryCast(GetTemplateChild("PART_ActionButton"), Button)
            If _actionButton IsNot Nothing Then
                AddHandler _actionButton.Click, AddressOf OnActionButtonClick
            End If
        End Sub

        Private Sub OnActionButtonClick(sender As Object, e As RoutedEventArgs)
            RaiseEvent ActionClicked(Me, e)
        End Sub

        Private Shared Sub OnIconKeyChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
            Dim state = TryCast(d, EmptyState)
            If state Is Nothing Then Return
            Dim key = CType(e.NewValue, IconKey)
            state.IconGeometry = If(key = IconKey.None, Nothing, IconManager.GetGeometry(key))
        End Sub
    End Class

End Namespace
