Option Strict On
Option Explicit On

Imports System.Windows
Imports System.Windows.Controls

Namespace UI.Controls

    ''' <summary>
    ''' Semantic severity levels shared by StatusBadge and ToastNotification.
    ''' </summary>
    Public Enum SemanticSeverity
        Neutral = 0
        Info = 1
        Success = 2
        Warning = 3
        [Error] = 4
    End Enum

    ''' <summary>
    ''' Compact semantic status pill badge (e.g. Active, Pending, Warning, Failed).
    ''' </summary>
    Public Class StatusBadge
        Inherits Control

        Public Shared ReadOnly TextProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Text), GetType(String), GetType(StatusBadge),
                                        New PropertyMetadata(String.Empty))

        Public Shared ReadOnly SeverityProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Severity), GetType(SemanticSeverity), GetType(StatusBadge),
                                        New PropertyMetadata(SemanticSeverity.Neutral))

        Public Property Text As String
            Get
                Return CStr(GetValue(TextProperty))
            End Get
            Set(value As String)
                SetValue(TextProperty, value)
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
    End Class

End Namespace
