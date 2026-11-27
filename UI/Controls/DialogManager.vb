Option Strict On
Option Explicit On

Imports System.Windows
Imports HidaChat.UI.Icons

Namespace UI.Controls

    ''' <summary>
    ''' Centralized helper for displaying themed Fluent modal confirmation, info, warning, and error dialogs.
    ''' </summary>
    Public Module DialogManager

        ''' <summary>
        ''' Displays a themed confirmation dialog and returns True if the user confirmed.
        ''' </summary>
        Public Function ShowConfirm(owner As Window,
                                    title As String,
                                    message As String,
                                    Optional confirmText As String = "Confirm",
                                    Optional cancelText As String = "Cancel",
                                    Optional isDestructive As Boolean = False,
                                    Optional iconKey As IconKey = IconKey.Info) As Boolean
            Dim dlg As New ConfirmDialog()
            If owner IsNot Nothing AndAlso owner.IsLoaded Then
                dlg.Owner = owner
                dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner
            Else
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen
            End If

            dlg.Configure(title, message, confirmText, cancelText, isDestructive, showCancel:=True, iconKey:=iconKey)
            Dim result = dlg.ShowDialog()
            Return result.HasValue AndAlso result.Value
        End Function

        ''' <summary>
        ''' Displays a themed informational alert dialog.
        ''' </summary>
        Public Sub ShowInfo(owner As Window, title As String, message As String, Optional okText As String = "OK")
            ShowAlertInternal(owner, title, message, okText, isDestructive:=False, iconKey:=IconKey.Info)
        End Sub

        ''' <summary>
        ''' Displays a themed warning alert dialog.
        ''' </summary>
        Public Sub ShowWarning(owner As Window, title As String, message As String, Optional okText As String = "OK")
            ShowAlertInternal(owner, title, message, okText, isDestructive:=False, iconKey:=IconKey.Warning)
        End Sub

        ''' <summary>
        ''' Displays a themed error alert dialog.
        ''' </summary>
        Public Sub ShowError(owner As Window, title As String, message As String, Optional okText As String = "OK")
            ShowAlertInternal(owner, title, message, okText, isDestructive:=True, iconKey:=IconKey.Error)
        End Sub

        Private Sub ShowAlertInternal(owner As Window,
                                      title As String,
                                      message As String,
                                      okText As String,
                                      isDestructive As Boolean,
                                      iconKey As IconKey)
            Dim dlg As New ConfirmDialog()
            If owner IsNot Nothing AndAlso owner.IsLoaded Then
                dlg.Owner = owner
                dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner
            Else
                dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen
            End If

            dlg.Configure(title, message, okText, String.Empty, isDestructive, showCancel:=False, iconKey:=iconKey)
            dlg.ShowDialog()
        End Sub

    End Module

End Namespace
