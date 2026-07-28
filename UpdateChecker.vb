Imports System.IO
Imports System.IO.Compression
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Diagnostics
Imports System.Threading.Tasks
Imports System.Windows
Imports System.Text.Json

''' <summary>

''' Gestisce il controllo, download ed installazione automatica degli aggiornamenti tramite GitHub Releases o cartella di rete locale (OTA).
''' </summary>
Public Class UpdateChecker
    Private Shared _hasChecked As Boolean = False
    Private Shared ReadOnly _httpClient As New HttpClient()

    Shared Sub New()
        _httpClient.Timeout = TimeSpan.FromSeconds(15)
        _httpClient.DefaultRequestHeaders.UserAgent.Add(New ProductInfoHeaderValue("WhatsappH-App", Constants.AppVersion))
        _httpClient.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/json"))
    End Sub

    ''' <summary>
    ''' Rimuove il prefisso 'v' e gli spazi vuoti da una stringa di versione (es. "v1.2.0" -> "1.2.0").
    ''' </summary>
    Private Shared Function CleanVersionString(verStr As String) As String
        If String.IsNullOrWhiteSpace(verStr) Then Return String.Empty
        Dim clean = verStr.Trim()
        If clean.StartsWith("v", StringComparison.OrdinalIgnoreCase) Then
            clean = clean.Substring(1)
        End If
        Return clean
    End Function

    ''' <summary>
    ''' Confronta la versione remota con la versione corrente e restituisce true se la versione remota è più recente.
    ''' </summary>
    Private Shared Function IsNewerVersion(remote As String, current As String) As Boolean
        Dim cleanRemote = CleanVersionString(remote)
        Dim cleanCurrent = CleanVersionString(current)

        Dim rParts = cleanRemote.Split("."c)
        Dim cParts = cleanCurrent.Split("."c)
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

    ''' <summary>
    ''' Controlla la presenza di aggiornamenti su GitHub Releases ed eventualmente esegue il download ed installazione.
    ''' </summary>
    ''' <param name="settings">Controller delle impostazioni utente.</param>
    ''' <param name="accountManager">Gestore degli account.</param>
    ''' <param name="force">Se true, forza il controllo ignorando le impostazioni automatiche all'avvio.</param>
    Public Shared Async Function CheckForUpdatesAsync(
        settings As SettingsController,
        accountManager As AccountManager,
        Optional force As Boolean = False
    ) As Task
        If Not force AndAlso _hasChecked Then Return
        _hasChecked = True

        Dim installDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)

        If Not force AndAlso Not settings.CheckForUpdates Then
            Debug.WriteLine("Update check on launch is disabled by user.")
            Return
        End If

        Try
            ' 1. Tenta la verifica tramite le API di GitHub Releases
            Dim releaseInfo = Await FetchGitHubReleaseInfoAsync(settings.UseBetaChannel)
            If releaseInfo IsNot Nothing Then
                Dim remoteVersion = releaseInfo.Version
                Dim downloadUrl = releaseInfo.DownloadUrl

                Debug.WriteLine($"GitHub Release check: Current={Constants.AppVersion}, Remote={remoteVersion}")

                If IsNewerVersion(remoteVersion, Constants.AppVersion) Then
                    If Not String.IsNullOrEmpty(downloadUrl) Then
                        Await PerformUpdateFromGitHubAsync(remoteVersion, downloadUrl, installDir, settings)
                        Return
                    Else
                        Debug.WriteLine("GitHub release found but no ZIP asset attached.")
                    End If
                ElseIf remoteVersion = Constants.AppVersion Then
                    If force Then
                        MessageBox.Show(
                            "Hai già la versione più recente (v" & Constants.AppVersion & ")!",
                            "Aggiornato",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        )
                    End If
                    Return
                Else
                    If force Then
                        MessageBox.Show(
                            "La versione remota (v" & remoteVersion & ") è precedente o uguale a quella corrente (v" & Constants.AppVersion & ").",
                            "Aggiornamento",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        )
                    End If
                    Return
                End If
            End If

        Catch ex As Exception
            Debug.WriteLine($"GitHub update check error: {ex.Message}")
        End Try

        ' 2. Fallback su percorsi di rete locale (se configurati e accessibili)
        Await CheckLocalOtaFallbackAsync(settings, installDir, force)
    End Function

    Private Class ReleaseInfo
        Public Property Version As String = String.Empty
        Public Property DownloadUrl As String = String.Empty
        Public Property Notes As String = String.Empty
    End Class

    ''' <summary>
    ''' Recupera le informazioni sull'ultima release pubblicata su GitHub interpellando le API REST.
    ''' </summary>
    Private Shared Async Function FetchGitHubReleaseInfoAsync(useBeta As Boolean) As Task(Of ReleaseInfo)
        Dim apiUrl = If(useBeta, Constants.GitHubReleasesApiUrl, Constants.GitHubLatestReleaseApiUrl)
        
        Dim response = Await _httpClient.GetAsync(apiUrl)
        If Not response.IsSuccessStatusCode Then
            Debug.WriteLine($"GitHub API response code: {response.StatusCode}")
            Return Nothing
        End If

        Dim jsonText = Await response.Content.ReadAsStringAsync()
        Using doc As JsonDocument = JsonDocument.Parse(jsonText)
            Dim root = doc.RootElement

            ' Se canale beta, l'endpoint restituisce un array di release
            Dim releaseElement As JsonElement = root
            If root.ValueKind = JsonValueKind.Array Then
                If root.GetArrayLength() = 0 Then Return Nothing
                releaseElement = root(0)
            End If

            Dim tagName = If(releaseElement.TryGetProperty("tag_name", Nothing), releaseElement.GetProperty("tag_name").GetString(), "")
            Dim cleanVer = CleanVersionString(tagName)

            Dim notes = If(releaseElement.TryGetProperty("body", Nothing), releaseElement.GetProperty("body").GetString(), "")

            ' Trova l'asset .zip nei file allegati alla release
            Dim zipUrl As String = String.Empty
            If releaseElement.TryGetProperty("assets", Nothing) Then
                For Each asset In releaseElement.GetProperty("assets").EnumerateArray()
                    Dim name = If(asset.TryGetProperty("name", Nothing), asset.GetProperty("name").GetString(), "")
                    If name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) Then
                        zipUrl = If(asset.TryGetProperty("browser_download_url", Nothing), asset.GetProperty("browser_download_url").GetString(), "")
                        Exit For
                    End If
                Next
            End If

            Return New ReleaseInfo With {
                .Version = cleanVer,
                .DownloadUrl = zipUrl,
                .Notes = notes
            }
        End Using
    End Function

    ''' <summary>
    ''' Scarica l'archivio ZIP da GitHub Releases, estrae i file e riavvia l'applicazione tramite uno script batch temporaneo.
    ''' </summary>
    Private Shared Async Function PerformUpdateFromGitHubAsync(
        latestVersion As String,
        downloadUrl As String,
        installDir As String,
        settings As SettingsController
    ) As Task
        ' Verifica i permessi di scrittura nella cartella corrente
        Dim testFile = Path.Combine(installDir, ".update_test")
        Try
            File.WriteAllText(testFile, "test")
            File.Delete(testFile)
        Catch
            MessageBox.Show(
                "Impossibile aggiornare automaticamente." & vbCrLf &
                "L'applicazione non ha i permessi di scrittura nella cartella di installazione." & vbCrLf & vbCrLf &
                "Sposta l'applicazione in una cartella locale scrivibile (es. C:\Programmi\WhatsappH)" & vbCrLf &
                "Versione disponibile su GitHub: v" & latestVersion,
                "Permessi insufficienti",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            )
            Return
        End Try

        Dim result = MessageBox.Show(
            $"È disponibile una nuova versione di WhatsappH (v{latestVersion})!" & vbCrLf & vbCrLf &
            "Desideri scaricare ed installare l'aggiornamento ora?",
            "Aggiornamento Disponibile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        )

        If result <> MessageBoxResult.Yes Then Return

        Dim tempZipPath = Path.Combine(Path.GetTempPath(), "WhatsappH_Update.zip")
        Dim tempDir = Path.Combine(Path.GetTempPath(), "WhatsappH_Update")

        Try
            ' 1. Scarica lo ZIP da GitHub
            Debug.WriteLine($"Downloading update zip from: {downloadUrl}")
            Dim zipBytes = Await _httpClient.GetByteArrayAsync(downloadUrl)
            Await File.WriteAllBytesAsync(tempZipPath, zipBytes)

            ' 2. Estrai l'archivio temporaneo
            If Directory.Exists(tempDir) Then Directory.Delete(tempDir, True)
            Directory.CreateDirectory(tempDir)
            ZipFile.ExtractToDirectory(tempZipPath, tempDir, True)

            ' Gestisci eventuale sottocartella singola estratta dallo ZIP
            Dim sourceDir = tempDir
            Dim subDirs = Directory.GetDirectories(tempDir)
            Dim exeInTemp = Directory.GetFiles(tempDir, "WhatsappH.exe", SearchOption.AllDirectories)
            If exeInTemp.Length > 0 Then
                sourceDir = Path.GetDirectoryName(exeInTemp(0))
            End If

            ' Marca la versione locale prima del riavvio
            WriteLocalVersionMarker(installDir, latestVersion)

            ' 3. Crea ed esegui lo script batch di sostituzione file
            Dim logFile = Path.Combine(installDir, ".update_log.txt")
            Dim batchPath = Path.Combine(tempDir, "update.bat")
            Dim batchContent = $"@echo off
title Aggiornamento WhatsappH...
set LOG=""{logFile}""
echo [%date% %time%] Starting update > %LOG%
set RETRY=0
:waitloop
echo [%date% %time%] Waiting for WhatsappH.exe to exit... >> %LOG%
timeout /t 2 /nobreak > nul
tasklist /fi ""IMAGENAME eq WhatsappH.exe"" 2>>%LOG% | find /i ""WhatsappH.exe"" >nul
if errorlevel 1 goto continue
set /a RETRY=RETRY+1
if %RETRY% GEQ 5 (
    echo [%date% %time%] Timeout dopo 10 secondi, forzo prosecuzione... >> %LOG%
    goto continue
)
goto waitloop
:continue
echo [%date% %time%] Process exited, copying files... >> %LOG%
robocopy ""{sourceDir}"" ""{installDir}"" /e /is /it /r:3 /w:2 >> %LOG%
set RC=%ERRORLEVEL%
echo [%date% %time%] Robocopy exit code: %RC% >> %LOG%
if %RC% GEQ 8 (
    echo [%date% %time%] ERRORE: robocopy failed >> %LOG%
    echo Copia file fallita. Verifica il log: {logFile}
    pause
    exit /b 1
)
echo v{latestVersion}>""{installDir}\.app_version""
echo [%date% %time%] Version marker written >> %LOG%
echo [%date% %time%] Launching app... >> %LOG%
start """" ""{installDir}\WhatsappH.exe""
echo [%date% %time%] Done >> %LOG%
del ""%~f0""
"
            File.WriteAllText(batchPath, batchContent)

            Process.Start(New ProcessStartInfo With {
                .FileName = batchPath,
                .UseShellExecute = True
            })

            Application.Current.Dispatcher.Invoke(Sub()
                Dim mainWin = TryCast(Application.Current.MainWindow, MainWindow)
                If mainWin IsNot Nothing Then
                    mainWin.ForceExitForUpdate()
                End If
                Application.Current.Shutdown()
            End Sub)

        Catch ex As Exception
            Debug.WriteLine($"Update execution failed: {ex.Message}")
            MessageBox.Show(
                "Errore durante l'installazione dell'aggiornamento: " & ex.Message,
                "Errore Aggiornamento",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            )
        Finally
            Try
                If File.Exists(tempZipPath) Then File.Delete(tempZipPath)
            Catch
            End Try
        End Try
    End Function

    ''' <summary>
    ''' Controllo di fallback per la presenza di aggiornamenti da cartella di rete locale.
    ''' </summary>
    Private Shared Async Function CheckLocalOtaFallbackAsync(settings As SettingsController, installDir As String, force As Boolean) As Task
        Try
            Dim versionFile = If(settings.UseBetaChannel, Constants.UpdateVersionFileBeta, Constants.UpdateVersionFile)
            If Not File.Exists(versionFile) Then Return

            Dim latestVersion = (Await File.ReadAllTextAsync(versionFile)).Trim()
            Dim cleanVer = CleanVersionString(latestVersion)

            If IsNewerVersion(cleanVer, Constants.AppVersion) Then
                Debug.WriteLine($"Local OTA update found: v{cleanVer}")
            End If
        Catch ex As Exception
            Debug.WriteLine($"Local OTA fallback check error: {ex.Message}")
        End Try
    End Function

    Private Shared Sub WriteLocalVersionMarker(installDir As String, version As String)
        Try
            Dim markerPath = Path.Combine(installDir, ".app_version")
            File.WriteAllText(markerPath, version.Trim())
        Catch ex As Exception
            Debug.WriteLine($"Failed to write local version marker: {ex.Message}")
        End Try
    End Sub
End Class

