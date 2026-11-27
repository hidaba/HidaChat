Option Strict On
Option Explicit On

Imports System.Windows
Imports System.Windows.Controls

Namespace UI.Controls

    ''' <summary>
    ''' Fluent Design surface card container with optional header, subtitle, and right-aligned header action slot.
    ''' </summary>
    Public Class ModernCard
        Inherits ContentControl

        Public Shared ReadOnly HeaderProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Header), GetType(String), GetType(ModernCard),
                                        New PropertyMetadata(String.Empty))

        Public Shared ReadOnly SubtitleProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Subtitle), GetType(String), GetType(ModernCard),
                                        New PropertyMetadata(String.Empty))

        Public Shared ReadOnly HeaderActionProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(HeaderAction), GetType(Object), GetType(ModernCard),
                                        New PropertyMetadata(Nothing))

        Public Shared ReadOnly CornerRadiusProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(CornerRadius), GetType(CornerRadius), GetType(ModernCard),
                                        New PropertyMetadata(New CornerRadius(8)))

        Public Property Header As String
            Get
                Return CStr(GetValue(HeaderProperty))
            End Get
            Set(value As String)
                SetValue(HeaderProperty, value)
            End Set
        End Property

        Public Property Subtitle As String
            Get
                Return CStr(GetValue(SubtitleProperty))
            End Get
            Set(value As String)
                SetValue(SubtitleProperty, value)
            End Set
        End Property

        Public Property HeaderAction As Object
            Get
                Return GetValue(HeaderActionProperty)
            End Get
            Set(value As Object)
                SetValue(HeaderActionProperty, value)
            End Set
        End Property

        Public Property CornerRadius As CornerRadius
            Get
                Return CType(GetValue(CornerRadiusProperty), CornerRadius)
            End Get
            Set(value As CornerRadius)
                SetValue(CornerRadiusProperty, value)
            End Set
        End Property
    End Class

End Namespace
