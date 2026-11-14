Imports System.Text.Json

''' <summary>
''' Gestisce le migrazioni e gli aggiornamenti automatici di configurazione tra diverse versioni dell'applicazione.
''' </summary>
Public Module SettingsMigration
    ''' <summary>
    ''' Migrazione disattivata: la forzatura dello smarcamento del canale beta è stata rimossa su richiesta dell'utente.
    ''' Preserva la preferenza scelta dall'utente (useBetaChannel) senza alterarla.
    ''' </summary>
    Public Function ApplyBetaChannelReset130(settings As Dictionary(Of String, Object), ByRef useBetaChannel As Boolean) As Boolean
        Return False
    End Function

    ''' <summary>
    ''' Migrazione disattivata: la forzatura dello smarcamento del canale beta è stata rimossa su richiesta dell'utente.
    ''' Preserva la preferenza scelta dall'utente (useBetaChannel) senza alterarla.
    ''' </summary>
    Public Function ApplyBetaChannelReset140(settings As Dictionary(Of String, Object), ByRef useBetaChannel As Boolean) As Boolean
        Return False
    End Function
End Module
