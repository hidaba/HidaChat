Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Windows
Imports HidaChat.UI.Theme

''' <summary>
''' Fluent Design modal privacy consent dialog shown before sending uncached chat text
''' to the selected translation provider.
''' </summary>
Public Class TranslationPrivacyConsentDialog

    Public Property ConsentGranted As Boolean = False
    Public Property OpenSettingsRequested As Boolean = False

    Private ReadOnly _loc As AppLocalizations
    Private ReadOnly _providerDisplayName As String

    ''' <summary>
    ''' Parameterless constructor preserved for Visual Studio XAML Designer compatibility.
    ''' </summary>
    Public Sub New()
        InitializeComponent()
    End Sub

    Public Sub New(loc As AppLocalizations, Optional providerDisplayName As String = "Google Translate")
        InitializeComponent()
        _loc = loc
        _providerDisplayName = If(Not String.IsNullOrWhiteSpace(providerDisplayName), providerDisplayName, "Google Translate")
        If Not DesignerProperties.GetIsInDesignMode(Me) Then
            ThemeManager.Instance.RegisterWindow(Me)
        End If
        ApplyLocalization()
    End Sub

    Protected Overrides Sub OnClosed(e As EventArgs)
        ThemeManager.Instance.UnregisterWindow(Me)
        MyBase.OnClosed(e)
    End Sub

    Private Sub ApplyLocalization()
        If _loc IsNot Nothing Then
            Dim titleText = "HidaChat — " & _loc.Get("translation_privacy_title")
            Me.Title = titleText
            TxtTitle.Text = titleText
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
