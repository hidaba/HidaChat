Imports System.IO
Imports System.Diagnostics
Imports System.Threading.Tasks
Imports System.Windows
Imports System.Text.Json

Public Class UpdateChecker
    Private Shared _hasChecked As Boolean = False

    Private Shared Function GetUpdateFilesPath(settings As SettingsController) As String
        Return If(settings.UseBetaChannel, Constants.UpdateFilesPathBeta, Constants.UpdateFilesPath)
    End Function

    Private Shared Function GetUpdateVersionFile(settings As SettingsController) As String
        Return If(settings.UseBetaChannel, Constants.UpdateVersionFileBeta, Constants.UpdateVersionFile)
    End Function

    Private Shared Function IsNewerVersion(remote As String, current As String) As Boolean
        Dim rParts = remote.Split("."c)
        Dim cParts = current.Split("."c)
        For i As Integer = 0 To Math.Min(rParts.Length, cParts.Length) - 1
            Dim rVal As Integer = 0
            Dim cVal As Integer = 0
            Integer.TryParse(rParts(i), rVal)
            Integer.TryParse(cParts(i), cVal)
            If rVal > cVal Then Return True
            If rVal < cVal Then Return False
        Next
        Return rParts.Length > cParts.Length
    End Function

    ' Controlla se esiste una versione più recente sul repository OTA
    Public Shared Async Function CheckForUpdatesAsync(
        settings As SettingsController,
        accountManager As AccountManager,
        Optional force As Boolean = False
    ) As Task
        If Not force AndAlso _hasChecked Then Return
        _hasChecked = True

        Dim installDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)

        ' Se la versione locale corrisponde già all'app, salta il check (evita loop post-aggiornamento)
        If Not force AndAlso IsLocalVersionCurrent(installDir) Then
            Debug.WriteLine("Local version marker matches, update check skipped.")
            Return
        End If

        If Not force AndAlso Not settings.CheckForUpdates Then
            Debug.WriteLine("Update check on launch is disabled by user.")
            Return
        End If

        ' If running directly from the OTA repository, skip update
        Dim updateFilesPath = GetUpdateFilesPath(settings)
        If installDir.TrimEnd("\"c).Equals(updateFilesPath.TrimEnd("\"c), StringComparison.OrdinalIgnoreCase) Then
            Debug.WriteLine("Running directly from OTA repository, update check skipped.")
            If force Then
                MessageBox.Show(
                    "L'applicazione è in esecuzione direttamente dal repository OTA." & vbCrLf &
                    "Copia l'applicazione in una cartella locale per utilizzare l'aggiornamento automatico.",
                    "Aggiornamento",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                )
            End If
            Return
        End If

        Dim latestVersion = Await ReadVersionFromFileAsync(settings)

        If String.IsNullOrEmpty(latestVersion) Then
            Debug.WriteLine("Update check failed: could not read version file.")
            If force Then
                Dim versionFile = GetUpdateVersionFile(settings)
                MessageBox.Show(
                    "Impossibile leggere il file degli aggiornamenti." & vbCrLf &
                    "Verifica la connettività al percorso di rete: " & versionFile,
                    "Errore connessione",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                )
            End If
            Return
        End If

        Debug.WriteLine($"Current version: {Constants.AppVersion}, Remote version: {latestVersion}")

        If IsNewerVersion(latestVersion, Constants.AppVersion) Then
            Await PerformUpdateAsync(latestVersion, installDir, settings)
        ElseIf latestVersion <> Constants.AppVersion Then
            Debug.WriteLine($"Remote version ({latestVersion}) is older than current ({Constants.AppVersion}), skipping.")
            If force Then
                MessageBox.Show(
                    "La versione remota (" & latestVersion & ") è precedente a quella corrente (" & Constants.AppVersion & ")." & vbCrLf &
                    "Nessun aggiornamento disponibile.",
                    "Aggiornamento",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                )
            End If
        Else
            If force Then
                MessageBox.Show(
                    "Hai già la versione più recente!",
                    "Aggiornato",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                )
            End If
        End If
    End Function

    Private Shared Async Function ReadVersionFromFileAsync(settings As SettingsController) As Task(Of String)
        Try
            Return (Await File.ReadAllTextAsync(GetUpdateVersionFile(settings))).Trim()
        Catch ex As Exception
            Debug.WriteLine($"Error reading version file: {ex.Message}")
            Return Nothing
        End Try
    End Function

    ' Scarica i nuovi file dall'OTA, genera un batch e riavvia l'app
    Private Shared Async Function PerformUpdateAsync(latestVersion As String, installDir As String, settings As SettingsController) As Task
        Dim updateFilesPath = GetUpdateFilesPath(settings)
        Await Task.Run(Sub()
            Try
                ' Verify we can write to the install directory
                Dim testFile = Path.Combine(installDir, ".update_test")
                Try
                    File.WriteAllText(testFile, "test")
                    File.Delete(testFile)
                Catch
                    Application.Current.Dispatcher.Invoke(Sub()
                        MessageBox.Show(
                            "Impossibile aggiornare automaticamente." & vbCrLf &
                            "L'applicazione non ha i permessi di scrittura nella cartella di installazione." & vbCrLf & vbCrLf &
                            "Esegui l'applicazione da una cartella locale (es. C:\Programmi\WhatsAppVB)" & vbCrLf &
                            "oppure copia manualmente i file da:" & vbCrLf &
                            updateFilesPath & vbCrLf & vbCrLf &
                            "Versione disponibile: " & latestVersion,
                            "Aggiornamento non disponibile",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        )
                    End Sub)
                    Return
                End Try

                Dim tempDir = Path.Combine(Path.GetTempPath(), "WhatsAppVB_Update")

                If Directory.Exists(tempDir) Then Directory.Delete(tempDir, True)
                Directory.CreateDirectory(tempDir)

                ' Copia solo i file dell'app, ESCLUDE cartella data\ e file utente (settings, cache traduzioni, version)
                For Each f In Directory.GetFiles(updateFilesPath, "*.*", SearchOption.AllDirectories)
                    Dim relPath = f.Substring(updateFilesPath.TrimEnd("\"c).Length + 1)
                    ' Salta dati utente e metadati
                    If relPath.StartsWith("data\", StringComparison.OrdinalIgnoreCase) Then Continue For
                    If relPath.Equals("settings.json", StringComparison.OrdinalIgnoreCase) Then Continue For
                    If relPath.Equals("translations_cache.json", StringComparison.OrdinalIgnoreCase) Then Continue For
                    If relPath.Equals("version.txt", StringComparison.OrdinalIgnoreCase) Then Continue For
                    Dim destFile = Path.Combine(tempDir, relPath)
                    Dim destDir = Path.GetDirectoryName(destFile)
                    If Not Directory.Exists(destDir) Then Directory.CreateDirectory(destDir)
                    IO.File.Copy(f, destFile, True)
                Next

                ' Merge nuove chiavi da settings.json dell'OTA nel settings.json locale
                MergeSettingsFromOta(installDir, updateFilesPath)

                ' Marca la versione locale prima del riavvio per evitare loop di aggiornamento
                WriteLocalVersionMarker(installDir, latestVersion)

                Dim batchPath = Path.Combine(tempDir, "update.bat")
                Dim batchContent = $"@echo off
title Aggiornamento WhatsAppVB...
:waitloop
timeout /t 2 /nobreak > nul
tasklist /fi ""IMAGENAME eq WhatsAppVB.exe"" 2>nul | find /i ""WhatsAppVB.exe"" >nul
if not errorlevel 1 goto waitloop
robocopy ""{tempDir}"" ""{installDir}"" /e /is /it /r:3 /w:2 > nul
if errorlevel 8 (
    echo ERRORE: impossibile copiare i file.
    pause
    exit /b 1
)
echo {latestVersion}>""{installDir}\.app_version""
start """" ""{installDir}\WhatsAppVB.exe""
del ""%~f0""
"
                File.WriteAllText(batchPath, batchContent)

                Process.Start(New ProcessStartInfo With {
                    .FileName = batchPath,
                    .UseShellExecute = True
                })

                Application.Current.Dispatcher.Invoke(Sub()
                    Application.Current.Shutdown()
                End Sub)

            Catch ex As Exception
                Debug.WriteLine($"Update failed: {ex.Message}")
                Application.Current.Dispatcher.Invoke(Sub()
                    MessageBox.Show(
                        "Aggiornamento fallito: " & ex.Message,
                        "Errore",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    )
                End Sub)
            End Try
        End Sub)
    End Function
    ' Legge il settings.json dell'OTA e aggiunge le chiavi mancanti al settings.json locale
    Private Shared Sub MergeSettingsFromOta(installDir As String, updateFilesPath As String)
        Try
            Dim otaSettingsPath = Path.Combine(updateFilesPath, "settings.json")
            Dim localSettingsPath As String = Nothing
            ' Cerca settings.json: prima in base, poi in data\
            Dim basePath = Path.Combine(installDir, "settings.json")
            If File.Exists(basePath) Then
                localSettingsPath = basePath
            Else
                Dim dataPath = Path.Combine(installDir, "data", "settings.json")
                If File.Exists(dataPath) Then localSettingsPath = dataPath
            End If

            If Not File.Exists(otaSettingsPath) OrElse localSettingsPath Is Nothing Then Return

            Dim otaJson = JsonSerializer.Deserialize(Of Dictionary(Of String, Object))(
                File.ReadAllText(otaSettingsPath))
            Dim localJson = JsonSerializer.Deserialize(Of Dictionary(Of String, Object))(
                File.ReadAllText(localSettingsPath))

            Dim hasNewKeys As Boolean = False
            For Each kvp In otaJson
                If Not localJson.ContainsKey(kvp.Key) Then
                    localJson(kvp.Key) = kvp.Value
                    hasNewKeys = True
                End If
            Next

            If hasNewKeys Then
                Dim options As New JsonSerializerOptions With {.WriteIndented = True}
                File.WriteAllText(localSettingsPath, JsonSerializer.Serialize(localJson, options))
                Debug.WriteLine("Merge completato: nuove chiavi aggiunte da OTA settings.json")
            End If
        Catch ex As Exception
            Debug.WriteLine($"Merge settings failed (non bloccante): {ex.Message}")
        End Try
    End Sub

    ' Legge il marker .app_version locale e verifica se corrisponde alla versione corrente
    Private Shared Function IsLocalVersionCurrent(installDir As String) As Boolean
        Try
            Dim markerPath = Path.Combine(installDir, ".app_version")
            If File.Exists(markerPath) Then
                Dim localVersion = File.ReadAllText(markerPath).Trim()
                Return localVersion = Constants.AppVersion
            End If
        Catch
        End Try
        Return False
    End Function

    ' Scrive il marker .app_version nella directory di installazione
    Private Shared Sub WriteLocalVersionMarker(installDir As String, version As String)
        Try
            Dim markerPath = Path.Combine(installDir, ".app_version")
            File.WriteAllText(markerPath, version.Trim())
            Debug.WriteLine($"Local version marker written: {version}")
        Catch ex As Exception
            Debug.WriteLine($"Failed to write local version marker: {ex.Message}")
        End Try
    End Sub
End Class
