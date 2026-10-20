Imports System.Collections.Generic
Imports System.Windows

''' <summary>
''' Finestra modale informativa di privacy per l'opt-in obbligatorio prima dell'invio dei testi
''' dei messaggi di chat al fornitore di traduzione selezionato (TODO #69).
''' </summary>
Public Class TranslationPrivacyConsentDialog

    Public Property ConsentGranted As Boolean = False
    Public Property OpenSettingsRequested As Boolean = False

    Private ReadOnly _loc As AppLocalizations
    Private ReadOnly _providerDisplayName As String

    Public Sub New(loc As AppLocalizations, Optional providerDisplayName As String = "Google Translate")
        InitializeComponent()
        _loc = loc
        _providerDisplayName = If(Not String.IsNullOrWhiteSpace(providerDisplayName), providerDisplayName, "Google Translate")
        ApplyLocalization()
    End Sub

    Private Sub ApplyLocalization()
        If _loc IsNot Nothing Then
            TxtTitle.Text = "HidaChat — " & _loc.Get("translation_privacy_title")
            TxtHeader.Text = _loc.Get("translation_privacy_header")

            Dim bodyArgs As New Dictionary(Of String, String) From {
                {"provider", _providerDisplayName}
            }
            TxtBody.Text = _loc.Get("translation_privacy_body", bodyArgs)

            BtnAllow.Content = _loc.Get("translation_privacy_allow")
            BtnSettings.Content = _loc.Get("translation_privacy_settings")
            BtnCancel.Content = _loc.Get("cancel")
        End If
    End Sub

    Private Sub BtnAllow_Click(sender As Object, e As RoutedEventArgs)
        ConsentGranted = True
        DialogResult = True
        Close()
    End Sub

    Private Sub BtnSettings_Click(sender As Object, e As RoutedEventArgs)
        ConsentGranted = False
        OpenSettingsRequested = True
        DialogResult = False
        Close()
    End Sub

    Private Sub BtnCancel_Click(sender As Object, e As RoutedEventArgs)
        ConsentGranted = False
        DialogResult = False
        Close()
    End Sub

End Class
