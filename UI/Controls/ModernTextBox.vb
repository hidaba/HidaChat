Option Strict On
Option Explicit On

Imports System.Windows
Imports System.Windows.Controls

Namespace UI.Controls

    ''' <summary>
    ''' Fluent Design TextBox with placeholder watermark, label, and inline validation error message.
    ''' </summary>
    Public Class ModernTextBox
        Inherits TextBox

        Public Shared ReadOnly PlaceholderProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Placeholder), GetType(String), GetType(ModernTextBox),
                                        New PropertyMetadata(String.Empty))

        Public Shared ReadOnly LabelProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Label), GetType(String), GetType(ModernTextBox),
                                        New PropertyMetadata(String.Empty))

        Public Shared ReadOnly ErrorMessageProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(ErrorMessage), GetType(String), GetType(ModernTextBox),
                                        New PropertyMetadata(String.Empty, AddressOf OnErrorMessageChanged))

        Public Shared ReadOnly HasErrorProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(HasError), GetType(Boolean), GetType(ModernTextBox),
                                        New PropertyMetadata(False))

        Public Shared ReadOnly CornerRadiusProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(CornerRadius), GetType(CornerRadius), GetType(ModernTextBox),
                                        New PropertyMetadata(New CornerRadius(6)))

        Public Property Placeholder As String
            Get
                Return CStr(GetValue(PlaceholderProperty))
            End Get
            Set(value As String)
                SetValue(PlaceholderProperty, value)
            End Set
        End Property

        Public Property Label As String
            Get
                Return CStr(GetValue(LabelProperty))
            End Get
            Set(value As String)
                SetValue(LabelProperty, value)
            End Set
        End Property

        Public Property ErrorMessage As String
            Get
                Return CStr(GetValue(ErrorMessageProperty))
            End Get
            Set(value As String)
                SetValue(ErrorMessageProperty, value)
            End Set
        End Property

        Public Property HasError As Boolean
            Get
                Return CBool(GetValue(HasErrorProperty))
            End Get
            Set(value As Boolean)
                SetValue(HasErrorProperty, value)
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

        Private Shared Sub OnErrorMessageChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
            Dim tb = TryCast(d, ModernTextBox)
            If tb Is Nothing Then Return
            Dim msg = TryCast(e.NewValue, String)
            tb.HasError = Not String.IsNullOrWhiteSpace(msg)
        End Sub
    End Class

End Namespace
