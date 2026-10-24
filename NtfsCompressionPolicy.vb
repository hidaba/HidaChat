Imports System.Diagnostics
Imports System.IO

''' <summary>
''' Politiche e utilità per la compressione trasparente a livello di filesystem NTFS della cartella dati portabile (TODO #106).
''' Esegue l'utility di sistema compact.exe in background per ridurre del 40-50% lo spazio occupato da cache e LevelDB
''' senza ritardi di avvio né rischi di corruzione in caso di crash o arresto anomalo.
''' </summary>
Public Module NtfsCompressionPolicy

    ''' <summary>
    ''' Costruisce la stringa degli argomenti da passare all'utility nativa compact.exe.
    ''' Parametri:
    '''   /C : abilita l'attributo compresso e comprime i file esistenti.
    '''   /S : applica l'operazione ricorsivamente alle directory e sottocartelle.
    '''   /I : ignora gli errori (consente l'esecuzione silenziosa su filesystem non NTFS come FAT32/exFAT di chiavette USB).
    '''   /Q : modalità silenziosa (riporta solo le informazioni essenziali).
    ''' </summary>
    Public Function BuildCompactArguments(folderPath As String) As String
        Return $"/C /S:""{folderPath}"" /I /Q"
    End Function

    ''' <summary>
    ''' Abilita in modo trasparente e non bloccante la compressione NTFS sulla cartella specificata.
    ''' Se la cartella non esiste, la crea preventivamente.
    ''' </summary>
    ''' <param name="folderPath">Percorso assoluto o relativo della cartella da comprimere.</param>
    ''' <returns>True se l'avvio del processo compact.exe ha avuto successo; False in caso di parametri nulli o eccezioni.</returns>
    Public Function EnableNtfsCompression(folderPath As String) As Boolean
        Try
            If String.IsNullOrWhiteSpace(folderPath) Then Return False
            If Not Directory.Exists(folderPath) Then
                Directory.CreateDirectory(folderPath)
            End If

            Dim psi As New ProcessStartInfo("compact.exe", BuildCompactArguments(folderPath)) With {
                .CreateNoWindow = True,
                .UseShellExecute = False,
                .WindowStyle = ProcessWindowStyle.Hidden
            }
            Using proc = Process.Start(psi)
                ' Esecuzione asincrona in background, non serve attendere o bloccare il thread principale
            End Using
            Return True
        Catch ex As Exception
            Return False
        End Try
    End Function

End Module
