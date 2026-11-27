Option Strict On
Option Explicit On

Imports System.Windows
Imports HidaChat.UI.Icons
Imports HidaChat.UI.Theme

Namespace UI.Controls

    ''' <summary>
    ''' Standardized Fluent Design modal confirmation and alert dialog with keyboard Enter/Escape support.
    ''' </summary>
    Public Class ConfirmDialog
        Inherits Window

        Public Sub New()
            InitializeComponent()
            ThemeManager.Instance.RegisterWindow(Me)
        End Sub

        Public Sub Configure(title As String,
                             message As String,
                             confirmText As String,
                             cancelText As String,
                             isDestructive As Boolean,
                             Optional showCancel As Boolean = True,
                             Optional iconKey As IconKey = IconKey.Info)
            Me.Title = title
            TxtTitle.Text = title
            TxtMessage.Text = message
            BtnConfirm.Content = confirmText
            BtnCancel.Content = cancelText
            BtnCancel.Visibility = If(showCancel, Visibility.Visible, Visibility.Collapsed)
            BtnConfirm.Variant = If(isDestructive, ButtonVariant.Danger, ButtonVariant.Primary)

            Dim effectiveIcon = If(isDestructive AndAlso iconKey = IconKey.Info, IconKey.Warning, iconKey)
            IconPath.Data = IconManager.GetGeometry(effectiveIcon)

            If isDestructive Then
                IconBadge.SetResourceReference(Border.BackgroundProperty, "ErrorSubtleBrush")
                IconPath.SetResourceReference(Shapes.Path.FillProperty, "ErrorBrush")
            ElseIf effectiveIcon = IconKey.Warning Then
                IconBadge.SetResourceReference(Border.BackgroundProperty, "WarningSubtleBrush")
                IconPath.SetResourceReference(Shapes.Path.FillProperty, "WarningBrush")
            Else
                IconBadge.SetResourceReference(Border.BackgroundProperty, "AccentSubtleBrush")
                IconPath.SetResourceReference(Shapes.Path.FillProperty, "AccentBrush")
            End If
        End Sub

        Protected Overrides Sub OnClosed(e As EventArgs)
            ThemeManager.Instance.UnregisterWindow(Me)
            MyBase.OnClosed(e)
        End Sub

        Private Sub BtnConfirm_Click(sender As Object, e As RoutedEventArgs)
            Me.DialogResult = True
            Me.Close()
        End Sub

        Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
            Me.DialogResult = False
            Me.Close()
        End Sub
    End Class

End Namespace
