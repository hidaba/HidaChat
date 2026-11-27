Option Strict On
Option Explicit On

Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports HidaChat.UI.Icons

Namespace UI.Controls

    ''' <summary>
    ''' Represents a single item in NavigationSidebar.
    ''' </summary>
    Public Class NavigationItem
        Implements INotifyPropertyChanged

        Private _isSelected As Boolean

        Public Property Key As String = String.Empty
        Public Property Title As String = String.Empty
        Public Property IconKey As IconKey = IconKey.None
        Public Property BadgeText As String = String.Empty

        Public ReadOnly Property IconGeometry As Geometry
            Get
                Return If(IconKey = IconKey.None, Nothing, IconManager.GetGeometry(IconKey))
            End Get
        End Property

        Public Property IsSelected As Boolean
            Get
                Return _isSelected
            End Get
            Set(value As Boolean)
                If _isSelected <> value Then
                    _isSelected = value
                    RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(IsSelected)))
                End If
            End Set
        End Property

        Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged
    End Class

    ''' <summary>
    ''' Event payload raised when a user selects a navigation item in NavigationSidebar.
    ''' </summary>
    Public NotInheritable Class NavigationSelectionChangedEventArgs
        Inherits EventArgs

        Public ReadOnly Property SelectedItem As NavigationItem

        Public Sub New(item As NavigationItem)
            Me.SelectedItem = item
        End Sub
    End Class

    ''' <summary>
    ''' Fluent Design vertical navigation sidebar with active accent indicator pill and vector icons.
    ''' </summary>
    Public Class NavigationSidebar
        Inherits ListBox

        Public Shared ReadOnly HeaderTitleProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(HeaderTitle), GetType(String), GetType(NavigationSidebar),
                                        New PropertyMetadata(String.Empty))

        Public Shared ReadOnly HeaderSubtitleProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(HeaderSubtitle), GetType(String), GetType(NavigationSidebar),
                                        New PropertyMetadata(String.Empty))

        Public Property HeaderTitle As String
            Get
                Return CStr(GetValue(HeaderTitleProperty))
            End Get
            Set(value As String)
                SetValue(HeaderTitleProperty, value)
            End Set
        End Property

        Public Property HeaderSubtitle As String
            Get
                Return CStr(GetValue(HeaderSubtitleProperty))
            End Get
            Set(value As String)
                SetValue(HeaderSubtitleProperty, value)
            End Set
        End Property

        Public Event NavigationSelected As EventHandler(Of NavigationSelectionChangedEventArgs)

        Public Sub New()
            MyBase.New()
            BorderThickness = New Thickness(0)
            SelectionMode = SelectionMode.Single
        End Sub

        Protected Overrides Sub OnSelectionChanged(e As SelectionChangedEventArgs)
            MyBase.OnSelectionChanged(e)
            Dim selectedNav = TryCast(Me.SelectedItem, NavigationItem)
            If ItemsSource IsNot Nothing Then
                For Each obj In ItemsSource
                    Dim nav = TryCast(obj, NavigationItem)
                    If nav IsNot Nothing Then
                        nav.IsSelected = Object.ReferenceEquals(nav, selectedNav)
                    End If
                Next
            End If
            If selectedNav IsNot Nothing Then
                RaiseEvent NavigationSelected(Me, New NavigationSelectionChangedEventArgs(selectedNav))
            End If
        End Sub

        ''' <summary>
        ''' Programmatically selects a navigation item by its unique key.
        ''' </summary>
        Public Sub SelectByKey(key As String)
            If String.IsNullOrWhiteSpace(key) OrElse ItemsSource Is Nothing Then Return
            For Each obj In ItemsSource
                Dim nav = TryCast(obj, NavigationItem)
                If nav IsNot Nothing AndAlso nav.Key.Equals(key, StringComparison.OrdinalIgnoreCase) Then
                    Me.SelectedItem = nav
                    Return
                End If
            Next
        End Sub
    End Class

End Namespace
