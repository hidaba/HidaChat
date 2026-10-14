Imports System.Windows
Imports System.Windows.Threading

''' <summary>
''' Finestra grafica modale compatta e localizzata che mostra l'avanzamento durante la fase di salvataggio
''' e chiusura controllata di HidaChat (TODO #74).
''' </summary>
Public Class ShutdownProgressDialog

    Public Sub New(Optional loc As AppLocalizations = Nothing)
        InitializeComponent()
        ApplyLocalization(loc)
    End Sub

    ''' <summary>
    ''' Applica le etichette localizzate nelle 5 lingue supportate.
    ''' </summary>
    Public Sub ApplyLocalization(loc As AppLocalizations)
        If loc IsNot Nothing Then
            TxtTitle.Text = loc.Get("shutdown_dialog_title")
            TxtHeader.Text = loc.Get("shutdown_dialog_header")
            TxtDetail.Text = loc.Get("shutdown_stage_settings")
        Else
            TxtTitle.Text = "HidaChat"
            TxtHeader.Text = "Salvataggio dati e chiusura in corso..."
            TxtDetail.Text = "Salvataggio impostazioni e preferenze account..."
        End If
    End Sub

    ''' <summary>
    ''' Aggiorna il testo descrittivo dello step di chiusura corrente.
    ''' </summary>
    Public Sub UpdateStatus(statusText As String)
        If String.IsNullOrWhiteSpace(statusText) Then Return

        If Not Dispatcher.CheckAccess() Then
            Dispatcher.InvokeAsync(Sub() UpdateStatus(statusText))
            Return
        End If

        TxtDetail.Text = statusText
        ' Forza il refresh della UI WPF
        Dispatcher.Invoke(DispatcherPriority.Render, Sub()
                                                     End Sub)
    End Sub

End Class
