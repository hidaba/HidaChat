Imports System.Windows
Imports System.Windows.Threading

''' <summary>
''' Finestra grafica modale con ProgressBar che mostra l'avanzamento in tempo reale durante
''' il download, la verifica e l'applicazione degli aggiornamenti OTA di HidaChat (TODO #87).
''' </summary>
Public Class UpdateProgressDialog

    Public Sub New(Optional loc As AppLocalizations = Nothing, Optional version As String = "")
        InitializeComponent()
        ApplyLocalization(loc, version)
    End Sub

    ''' <summary>
    ''' Applica le etichette localizzate nelle 5 lingue supportate.
    ''' </summary>
    Public Sub ApplyLocalization(loc As AppLocalizations, Optional version As String = "")
        Dim verText = If(Not String.IsNullOrWhiteSpace(version), $" (v{version.TrimStart("v"c)})", "")

        If loc IsNot Nothing Then
            TxtTitle.Text = loc.Get("update_dialog_title")
            Dim args As New Dictionary(Of String, String) From {{"version", version.TrimStart("v"c)}}
            TxtHeader.Text = loc.Get("update_stage_downloading", args)
            TxtDetail.Text = loc.Get("update_stage_downloading", args)
        Else
            TxtTitle.Text = "HidaChat"
            TxtHeader.Text = $"Aggiornamento in corso{verText}..."
            TxtDetail.Text = "Download del pacchetto di aggiornamento..."
        End If
    End Sub

    ''' <summary>
    ''' Imposta la modalità indeterminata (fluida) o determinata (a percentuale) della ProgressBar.
    ''' </summary>
    Public Sub SetIndeterminate(isIndeterminate As Boolean)
        If Not Dispatcher.CheckAccess() Then
            Dispatcher.InvokeAsync(Sub() SetIndeterminate(isIndeterminate))
            Return
        End If

        ProgressBarStatus.IsIndeterminate = isIndeterminate
    End Sub

    ''' <summary>
    ''' Aggiorna la percentuale di avanzamento e i testi di stato in modo thread-safe.
    ''' </summary>
    ''' <param name="percentage">Percentuale compresa tra 0 e 100 (-1 per lasciare invariato il valore).</param>
    ''' <param name="detailText">Descrizione della fase corrente.</param>
    ''' <param name="progressText">Informazioni aggiuntive (es. "12.5 MB / 45.0 MB (27%)").</param>
    Public Sub SetProgress(percentage As Double, Optional detailText As String = "", Optional progressText As String = "")
        If Not Dispatcher.CheckAccess() Then
            Dispatcher.InvokeAsync(Sub() SetProgress(percentage, detailText, progressText))
            Return
        End If

        If percentage >= 0 Then
            ProgressBarStatus.IsIndeterminate = False
            ProgressBarStatus.Value = Math.Max(0, Math.Min(100, percentage))
        End If

        If Not String.IsNullOrWhiteSpace(detailText) Then
            TxtDetail.Text = detailText
        End If

        If progressText IsNot Nothing Then
            TxtProgressInfo.Text = progressText
        End If

        ' Forza l'aggiornamento immediato del rendering UI
        Dispatcher.Invoke(DispatcherPriority.Render, Sub()
                                                     End Sub)
    End Sub

    ''' <summary>
    ''' Aggiorna il testo descrittivo della fase corrente in modo thread-safe.
    ''' </summary>
    Public Sub UpdateStatus(statusText As String, Optional detailText As String = "")
        If Not Dispatcher.CheckAccess() Then
            Dispatcher.InvokeAsync(Sub() UpdateStatus(statusText, detailText))
            Return
        End If

        If Not String.IsNullOrWhiteSpace(statusText) Then
            TxtDetail.Text = statusText
        End If

        If detailText IsNot Nothing Then
            TxtProgressInfo.Text = detailText
        End If

        ' Forza l'aggiornamento immediato del rendering UI
        Dispatcher.Invoke(DispatcherPriority.Render, Sub()
                                                     End Sub)
    End Sub

End Class
