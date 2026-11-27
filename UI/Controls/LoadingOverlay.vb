Option Strict On
Option Explicit On

Imports System.Windows
Imports System.Windows.Controls

Namespace UI.Controls

    ''' <summary>
    ''' Translucent loading overlay with indeterminate progress indicator and status message.
    ''' </summary>
    Public Class LoadingOverlay
        Inherits Control

        Public Shared ReadOnly IsLoadingProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(IsLoading), GetType(Boolean), GetType(LoadingOverlay),
                                        New PropertyMetadata(False))

        Public Shared ReadOnly MessageProperty As DependencyProperty =
            DependencyProperty.Register(NameOf(Message), GetType(String), GetType(LoadingOverlay),
                                        New PropertyMetadata("Loading..."))

        Public Property IsLoading As Boolean
            Get
                Return CBool(GetValue(IsLoadingProperty))
            End Get
            Set(value As Boolean)
                SetValue(IsLoadingProperty, value)
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
    End Class

End Namespace
