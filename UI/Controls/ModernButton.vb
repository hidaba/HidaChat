Option Strict On
Option Explicit On

Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Media
Imports HidaChat.UI.Icons

Namespace UI.Controls

    ''' <summary>
    ''' Visual style variants for ModernButton.
    ''' </summary>
    Public Enum ButtonVariant
        Primary = 0
        Secondary = 1
        Ghost = 2
        Danger = 3
    End Enum

    ''' <summary>
    ''' Fluent Design button supporting semantic variants, vector icons, and loading state.
    ''' </summary>
    Public Class ModernButton
        Inherits Button

        Public Shared ReadOnly VariantProperty As DependencyProperty =
            DependencyProperty.Register(NameOf([Variant]), GetType(ButtonVariant), GetType(ModernButton),
                                        New PropertyMetadata(ButtonVariant.Secondary))

        Public Shared ReadOnly IconKeyProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(IconKey), GetType(IconKey), GetType(ModernButton),
                                        New PropertyMetadata(IconKey.None, AddressOf OnIconKeyChanged))

        Public Shared ReadOnly IconGeometryProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(IconGeometry), GetType(Geometry), GetType(ModernButton),
                                        New PropertyMetadata(Nothing))

        Public Shared ReadOnly CornerRadiusProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(CornerRadius), GetType(CornerRadius), GetType(ModernButton),
                                        New PropertyMetadata(New CornerRadius(6)))

        Public Shared ReadOnly IsLoadingProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(IsLoading), GetType(Boolean), GetType(ModernButton),
                                        New PropertyMetadata(False))

        Public Property [Variant] As ButtonVariant
            Get
                Return CType(GetValue(VariantProperty), ButtonVariant)
            End Get
            Set(value As ButtonVariant)
                SetValue(VariantProperty, value)
            End Set
        End Property

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

        Public Property CornerRadius As CornerRadius
            Get
                Return CType(GetValue(CornerRadiusProperty), CornerRadius)
            End Get
            Set(value As CornerRadius)
                SetValue(CornerRadiusProperty, value)
            End Set
        End Property

        Public Property IsLoading As Boolean
            Get
                Return CBool(GetValue(IsLoadingProperty))
            End Get
            Set(value As Boolean)
                SetValue(IsLoadingProperty, value)
            End Set
        End Property

        Private Shared Sub OnIconKeyChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
            Dim btn = TryCast(d, ModernButton)
            If btn Is Nothing Then Return
            Dim key = CType(e.NewValue, IconKey)
            btn.IconGeometry = If(key = IconKey.None, Nothing, IconManager.GetGeometry(key))
        End Sub
    End Class

End Namespace
