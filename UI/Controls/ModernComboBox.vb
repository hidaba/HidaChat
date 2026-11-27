Option Strict On
Option Explicit On

Imports System.Windows
Imports System.Windows.Controls

Namespace UI.Controls

    ''' <summary>
    ''' Fluent Design ComboBox with theme-aware dropdown popup and consistent corner radius.
    ''' </summary>
    Public Class ModernComboBox
        Inherits ComboBox

        Public Shared ReadOnly CornerRadiusProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(CornerRadius), GetType(CornerRadius), GetType(ModernComboBox),
                                        New PropertyMetadata(New CornerRadius(6)))

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
