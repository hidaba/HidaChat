Imports System.Windows
Imports System.Windows.Threading

''' <summary>
''' Finestra grafica modale con ProgressBar che mostra l'avanzamento durante la fase di verifica
''' e ripristino dell'integrità dello storage IndexedDB/ServiceWorker corrotto (TODO #76).
''' </summary>
Public Class StorageRecoveryProgressDialog

    Public Sub New(Optional loc As AppLocalizations = Nothing, Optional accountName As String = "", Optional platform As String = "")
        InitializeComponent()
        ApplyLocalization(loc, accountName, platform)
    End Sub

    ''' <summary>
    ''' Applica le etichette localizzate nelle 5 lingue supportate.
    ''' </summary>
    Public Sub ApplyLocalization(loc As AppLocalizations, Optional accountName As String = "", Optional platform As String = "")
        Dim effectiveName = If(Not String.IsNullOrWhiteSpace(accountName), accountName, "Account")
        Dim effectivePlatform = If(Not String.IsNullOrWhiteSpace(platform), platform, "Web")

        If loc IsNot Nothing Then
            TxtTitle.Text = loc.Get("storage_recovery_dialog_title")
            TxtHeader.Text = $"{loc.Get("storage_recovery_dialog_header")} ({effectiveName})"
            Dim args As New Dictionary(Of String, String) From {
                {"account", effectiveName},
                {"platform", effectivePlatform}
            }
            TxtDetail.Text = loc.Get("storage_recovery_stage_checking", args)
        Else
            TxtTitle.Text = "HidaChat — Ripristino Storage"
            TxtHeader.Text = $"Ripristino storage locale in corso ({effectiveName})..."
            TxtDetail.Text = $"Verifica integrità storage per {effectiveName}..."
        End If
    End Sub

    ''' <summary>
    ''' Aggiorna il testo descrittivo dello step di ripristino corrente in modo thread-safe.
    ''' </summary>
    Public Sub UpdateStatus(statusText As String)
        If String.IsNullOrWhiteSpace(statusText) Then Return

        If Not Dispatcher.CheckAccess() Then
            Dispatcher.InvokeAsync(Sub() UpdateStatus(statusText))
            Return
        End If

        TxtDetail.Text = statusText
        ' Forza il refresh immediato del rendering UI
        Dispatcher.Invoke(DispatcherPriority.Render, Sub()
                                                     End Sub)
    End Sub

End Class
