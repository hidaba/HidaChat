Imports System.Text.Json

''' <summary>
''' Gestisce le migrazioni e gli aggiornamenti automatici di configurazione tra diverse versioni dell'applicazione.
''' </summary>
Public Module SettingsMigration
    ''' <summary>
    ''' Esegue la migrazione per la release stabile 1.3.0:
    ''' Se un client ha precedentemente marcato il canale di aggiornamento beta (useBetaChannel = True),
    ''' rimuove la spunta (imposta a False) per riallineare il client al canale stabile
    ''' e contrassegna l'avvenuto reset (betaReset130Done = True).
    ''' Restituisce True se le impostazioni sono state modificate e necessitano di essere salvate.
    ''' </summary>
    Public Function ApplyBetaChannelReset130(settings As Dictionary(Of String, Object), ByRef useBetaChannel As Boolean) As Boolean
        If settings Is Nothing Then Return False

        Dim betaResetDone = False
        If settings.ContainsKey("betaReset130Done") Then
            Dim obj = settings("betaReset130Done")
            If TypeOf obj Is JsonElement Then
                Dim element = CType(obj, JsonElement)
                betaResetDone = (element.ValueKind = JsonValueKind.True)
            Else
                Boolean.TryParse(obj?.ToString(), betaResetDone)
            End If
        End If

        If Not betaResetDone Then
            If useBetaChannel Then
                useBetaChannel = False
                settings("useBetaChannel") = False
            End If
            settings("betaReset130Done") = True
            Return True
        End If

        Return False
    End Function
End Module
