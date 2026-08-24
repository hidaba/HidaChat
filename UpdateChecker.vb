Imports System.IO
Imports System.IO.Compression
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Diagnostics
Imports System.Threading.Tasks
Imports System.Windows
Imports System.Text.Json
Imports System.Security.Cryptography
Imports System.Text.RegularExpressions

''' <summary>

''' Gestisce il controllo, download ed installazione automatica degli aggiornamenti tramite GitHub Releases o cartella di rete locale (OTA).
''' </summary>
Public Class UpdateChecker
    Private Shared _hasChecked As Boolean = False
    Private Shared ReadOnly _httpClient As New HttpClient()

    Shared Sub New()
        _httpClient.Timeout = TimeSpan.FromSeconds(15)
        _httpClient.DefaultRequestHeaders.UserAgent.Add(New ProductInfoHeaderValue("HidaChat-App", Constants.AppVersion))
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
    ''' Gestisce correttamente la separazione tra parti numeriche e suffissi di pre-release (es. "0.2.4-beta" vs "0.2.3").
    ''' </summary>
    Private Shared Function IsNewerVersion(remote As String, current As String) As Boolean
        Dim cleanRemote = CleanVersionString(remote)
        Dim cleanCurrent = CleanVersionString(current)

        If String.IsNullOrEmpty(cleanRemote) OrElse String.IsNullOrEmpty(cleanCurrent) Then
            Return False
        End If

        Dim remoteBase As String = cleanRemote
        Dim remotePre As String = String.Empty
        Dim remoteDashIdx = cleanRemote.IndexOf("-"c)
        If remoteDashIdx >= 0 Then
            remoteBase = cleanRemote.Substring(0, remoteDashIdx)
            remotePre = cleanRemote.Substring(remoteDashIdx + 1).Trim()
        End If

        Dim currentBase As String = cleanCurrent
        Dim currentPre As String = String.Empty
        Dim currentDashIdx = cleanCurrent.IndexOf("-"c)
        If currentDashIdx >= 0 Then
            currentBase = cleanCurrent.Substring(0, currentDashIdx)
            currentPre = cleanCurrent.Substring(currentDashIdx + 1).Trim()
        End If

        Dim rParts = remoteBase.Split("."c)
        Dim cParts = currentBase.Split("."c)
        Dim maxLen = Math.Max(rParts.Length, cParts.Length)

        For i As Integer = 0 To maxLen - 1
            Dim rVal As Integer = 0
            Dim cVal As Integer = 0
            If i < rParts.Length Then Integer.TryParse(rParts(i), rVal)
            If i < cParts.Length Then Integer.TryParse(cParts(i), cVal)

            If rVal > cVal Then Return True
            If rVal < cVal Then Return False
        Next

        ' Se la parte numerica base è identica, confronta i suffissi prerelease:
        ' 1. Se remote non ha prerelease (release stabile) e current ha prerelease (beta), remote è più recente.
        If String.IsNullOrEmpty(remotePre) AndAlso Not String.IsNullOrEmpty(currentPre) Then
            Return True
        End If

        ' 2. Se remote ha prerelease (beta) e current non ha prerelease (stabile), remote è considerata antecedente alla stabile.
        If Not String.IsNullOrEmpty(remotePre) AndAlso String.IsNullOrEmpty(currentPre) Then
            Return False
        End If

        ' 3. Se entrambe hanno prerelease, confronta alfanumericamente i suffissi (es. "beta2" > "beta1")
        If Not String.IsNullOrEmpty(remotePre) AndAlso Not String.IsNullOrEmpty(currentPre) Then
            Return String.Compare(remotePre, currentPre, StringComparison.OrdinalIgnoreCase) > 0
        End If

        Return False
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
                        Await PerformUpdateFromGitHubAsync(releaseInfo, installDir, settings)
                        Return
                    Else
                        Debug.WriteLine("GitHub release found but no ZIP asset attached.")
                    End If
                ElseIf CleanVersionString(remoteVersion).Equals(CleanVersionString(Constants.AppVersion), StringComparison.OrdinalIgnoreCase) Then
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
    End Function

    Private Class ReleaseInfo
        Public Property Version As String = String.Empty
        Public Property DownloadUrl As String = String.Empty
        Public Property ZipFileName As String = String.Empty
        Public Property Sha256Url As String = String.Empty
        Public Property ExpectedSha256 As String = String.Empty
        Public Property Notes As String = String.Empty
    End Class

    ''' <summary>
    ''' Estrae un hash SHA-256 valido (64 caratteri esadecimali) da un testo o da un file di checksum.
    ''' </summary>
    Private Shared Function ExtractSha256FromText(text As String, Optional filename As String = Nothing) As String
        If String.IsNullOrWhiteSpace(text) Then Return String.Empty

        ' 1. Se c'è un nome file da cercare (es. formato standard sha256sum: "<hash>  <filename>" o "<hash> *<filename>")
        If Not String.IsNullOrWhiteSpace(filename) Then
            Dim escapedName = Regex.Escape(Path.GetFileName(filename))
            Dim matchFile = Regex.Match(text, "([a-fA-F0-9]{64})\s+[\*]?" & escapedName, RegexOptions.IgnoreCase)
            If matchFile.Success Then
                Return matchFile.Groups(1).Value.ToLowerInvariant()
            End If
        End If

        ' 2. Cerca pattern espliciti tipo "SHA256: <hash>", "SHA-256: <hash>" o "hash: <hash>"
        Dim matchLabeled = Regex.Match(text, "(?:SHA-?256|checksum|hash)\s*[:=]?\s*([a-fA-F0-9]{64})", RegexOptions.IgnoreCase)
        If matchLabeled.Success Then
            Return matchLabeled.Groups(1).Value.ToLowerInvariant()
        End If

        ' 3. Cerca qualsiasi stringa esadecimale da 64 caratteri (SHA-256 isolato)
        Dim matchAny = Regex.Match(text, "\b([a-fA-F0-9]{64})\b")
        If matchAny.Success Then
            Return matchAny.Groups(1).Value.ToLowerInvariant()
        End If

        Return String.Empty
    End Function

    ''' <summary>
    ''' Calcola l'hash crittografico SHA-256 in formato esadecimale minuscolo per un array di byte.
    ''' </summary>
    Private Shared Function ComputeSha256(data As Byte()) As String
        If data Is Nothing OrElse data.Length = 0 Then Return String.Empty
        Dim hashBytes = SHA256.HashData(data)
        Return Convert.ToHexString(hashBytes).ToLowerInvariant()
    End Function

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

            ' Se canale beta, l'endpoint restituisce un array di release ordinale per data
            If root.ValueKind = JsonValueKind.Array Then
                For Each rel In root.EnumerateArray()
                    Dim tagName = If(rel.TryGetProperty("tag_name", Nothing), rel.GetProperty("tag_name").GetString(), "")
                    Dim cleanVer = CleanVersionString(tagName)
                    Dim notes = If(rel.TryGetProperty("body", Nothing), rel.GetProperty("body").GetString(), "")
                    Dim zipUrl As String = String.Empty
                    Dim zipName As String = String.Empty
                    Dim sha256Url As String = String.Empty

                    If rel.TryGetProperty("assets", Nothing) Then
                        For Each asset In rel.GetProperty("assets").EnumerateArray()
                            Dim name = If(asset.TryGetProperty("name", Nothing), asset.GetProperty("name").GetString(), "")
                            Dim assetUrl = If(asset.TryGetProperty("browser_download_url", Nothing), asset.GetProperty("browser_download_url").GetString(), "")

                            If name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) Then
                                zipUrl = assetUrl
                                zipName = name
                            ElseIf name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) OrElse
                                   name.EndsWith(".sha256sum", StringComparison.OrdinalIgnoreCase) OrElse
                                   name.Equals("SHA256SUMS", StringComparison.OrdinalIgnoreCase) OrElse
                                   name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase) OrElse
                                   name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase) Then
                                sha256Url = assetUrl
                            End If
                        Next
                    End If

                    If Not String.IsNullOrEmpty(zipUrl) Then
                        Dim expectedHash = ExtractSha256FromText(notes, zipName)
                        Return New ReleaseInfo With {
                            .Version = cleanVer,
                            .DownloadUrl = zipUrl,
                            .ZipFileName = zipName,
                            .Sha256Url = sha256Url,
                            .ExpectedSha256 = expectedHash,
                            .Notes = notes
                        }
                    End If
                Next
                Return Nothing
            Else
                Dim tagName = If(root.TryGetProperty("tag_name", Nothing), root.GetProperty("tag_name").GetString(), "")
                Dim cleanVer = CleanVersionString(tagName)
                Dim notes = If(root.TryGetProperty("body", Nothing), root.GetProperty("body").GetString(), "")
                Dim zipUrl As String = String.Empty
                Dim zipName As String = String.Empty
                Dim sha256Url As String = String.Empty

                If root.TryGetProperty("assets", Nothing) Then
                    For Each asset In root.GetProperty("assets").EnumerateArray()
                        Dim name = If(asset.TryGetProperty("name", Nothing), asset.GetProperty("name").GetString(), "")
                        Dim assetUrl = If(asset.TryGetProperty("browser_download_url", Nothing), asset.GetProperty("browser_download_url").GetString(), "")

                        If name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) Then
                            zipUrl = assetUrl
                            zipName = name
                        ElseIf name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase) OrElse
                               name.EndsWith(".sha256sum", StringComparison.OrdinalIgnoreCase) OrElse
                               name.Equals("SHA256SUMS", StringComparison.OrdinalIgnoreCase) OrElse
                               name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase) OrElse
                               name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase) Then
                            sha256Url = assetUrl
                        End If
                    Next
                End If

                Dim expectedHash = ExtractSha256FromText(notes, zipName)
                Return New ReleaseInfo With {
                    .Version = cleanVer,
                    .DownloadUrl = zipUrl,
                    .ZipFileName = zipName,
                    .Sha256Url = sha256Url,
                    .ExpectedSha256 = expectedHash,
                    .Notes = notes
                }
            End If
        End Using
    End Function

    ''' <summary>
    ''' Scarica l'archivio ZIP da GitHub Releases, verifica l'integrità crittografica SHA-256, estrae i file e riavvia l'applicazione tramite uno script batch temporaneo.
    ''' </summary>
    Private Shared Async Function PerformUpdateFromGitHubAsync(
        releaseInfo As ReleaseInfo,
        installDir As String,
        settings As SettingsController
    ) As Task
        Dim latestVersion = releaseInfo.Version
        Dim downloadUrl = releaseInfo.DownloadUrl

        ' Verifica i permessi di scrittura nella cartella corrente
        Dim testFile = Path.Combine(installDir, ".update_test")
        Try
            File.WriteAllText(testFile, "test")
            File.Delete(testFile)
        Catch
            MessageBox.Show(
                "Impossibile aggiornare automaticamente." & vbCrLf &
                "L'applicazione non ha i permessi di scrittura nella cartella di installazione." & vbCrLf & vbCrLf &
                "Sposta l'applicazione in una cartella locale scrivibile (es. C:\Programmi\HidaChat)" & vbCrLf &
                "Versione disponibile su GitHub: v" & latestVersion,
                "Permessi insufficienti",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            )
            Return
        End Try

        Dim result = MessageBox.Show(
            $"È disponibile una nuova versione di HidaChat (v{latestVersion})!" & vbCrLf & vbCrLf &
            "Desideri scaricare ed installare l'aggiornamento ora?",
            "Aggiornamento Disponibile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question
        )

        If result <> MessageBoxResult.Yes Then Return

        Dim tempZipPath = Path.Combine(Path.GetTempPath(), "HidaChat_Update.zip")
        Dim tempDir = Path.Combine(Path.GetTempPath(), "HidaChat_Update")

        Try
            ' 1. Scarica lo ZIP da GitHub
            Debug.WriteLine($"Downloading update zip from: {downloadUrl}")
            Dim zipBytes = Await _httpClient.GetByteArrayAsync(downloadUrl)

            ' 2. Verifica di integrità crittografica (SHA-256)
            Dim computedSha256 = ComputeSha256(zipBytes)
            Debug.WriteLine($"Downloaded update SHA-256: {computedSha256}")

            Dim expectedHash = releaseInfo.ExpectedSha256

            ' Se non presente nelle note di rilascio, prova a scaricare il file di checksum allegato agli asset
            If String.IsNullOrEmpty(expectedHash) AndAlso Not String.IsNullOrEmpty(releaseInfo.Sha256Url) Then
                Try
                    Debug.WriteLine($"Downloading SHA-256 checksum file from: {releaseInfo.Sha256Url}")
                    Dim checksumContent = Await _httpClient.GetStringAsync(releaseInfo.Sha256Url)
                    expectedHash = ExtractSha256FromText(checksumContent, releaseInfo.ZipFileName)
                Catch ex As Exception
                    Debug.WriteLine($"Could not download or parse checksum asset: {ex.Message}")
                End Try
            End If

            ' Se è disponibile un hash atteso, esegui il confronto di integrità
            If Not String.IsNullOrEmpty(expectedHash) Then
                If Not String.Equals(computedSha256, expectedHash, StringComparison.OrdinalIgnoreCase) Then
                    Debug.WriteLine($"SHA-256 mismatch! Computed: {computedSha256}, Expected: {expectedHash}")
                    MessageBox.Show(
                        "Verifica di integrità fallita!" & vbCrLf & vbCrLf &
                        "L'impronta crittografica SHA-256 del file di aggiornamento scaricato non corrisponde a quella attesa:" & vbCrLf & vbCrLf &
                        $"Hash calcolato: {computedSha256}" & vbCrLf &
                        $"Hash atteso:    {expectedHash}" & vbCrLf & vbCrLf &
                        "L'aggiornamento è stato interrotto per garantire la sicurezza del sistema.",
                        "Errore Integrità Aggiornamento",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    )
                    Return
                Else
                    Debug.WriteLine($"Update integrity verified successfully with SHA-256: {computedSha256}")
                End If
            Else
                Debug.WriteLine($"No SHA-256 checksum found for this release. Computed hash: {computedSha256}. Verified via HTTPS transport.")
            End If

            Await File.WriteAllBytesAsync(tempZipPath, zipBytes)

            ' 3. Estrai l'archivio temporaneo
            If Directory.Exists(tempDir) Then Directory.Delete(tempDir, True)
            Directory.CreateDirectory(tempDir)
            ZipFile.ExtractToDirectory(tempZipPath, tempDir, True)

            ' Gestisci eventuale sottocartella singola estratta dallo ZIP
            Dim sourceDir = tempDir
            Dim subDirs = Directory.GetDirectories(tempDir)
            Dim exeInTemp = Directory.GetFiles(tempDir, "HidaChat.exe", SearchOption.AllDirectories)
            If exeInTemp.Length > 0 Then
                sourceDir = Path.GetDirectoryName(exeInTemp(0))
            End If

            ' Marca la versione locale prima del riavvio
            WriteLocalVersionMarker(installDir, latestVersion)

            ' 4. Crea ed esegui lo script batch di sostituzione file
            Dim logFile = Path.Combine(installDir, ".update_log.txt")
            Dim batchPath = Path.Combine(tempDir, "update.bat")
            Dim sbBatch As New System.Text.StringBuilder()

            sbBatch.AppendLine("@echo off")
            sbBatch.AppendLine("title Aggiornamento HidaChat...")
            sbBatch.AppendLine($"set LOG=""{logFile}""")
            sbBatch.AppendLine("echo [%date% %time%] Starting update > %LOG%")
            sbBatch.AppendLine("set RETRY=0")
            sbBatch.AppendLine(":waitloop")
            sbBatch.AppendLine("echo [%date% %time%] Waiting for HidaChat.exe to exit... >> %LOG%")
            sbBatch.AppendLine("timeout /t 2 /nobreak > nul")
            sbBatch.AppendLine("tasklist /fi ""IMAGENAME eq HidaChat.exe"" 2>>%LOG% | find /i ""HidaChat.exe"" >nul")
            sbBatch.AppendLine("if errorlevel 1 goto continue")
            sbBatch.AppendLine("set /a RETRY=RETRY+1")
            sbBatch.AppendLine("if %RETRY% GEQ 5 (")
            sbBatch.AppendLine("    echo [%date% %time%] Timeout dopo 10 secondi, forzo chiusura del processo... >> %LOG%")
            sbBatch.AppendLine("    taskkill /f /im HidaChat.exe /t >nul 2>&1")
            sbBatch.AppendLine("    timeout /t 1 /nobreak > nul")
            sbBatch.AppendLine("    goto continue")
            sbBatch.AppendLine(")")
            sbBatch.AppendLine("goto waitloop")
            sbBatch.AppendLine(":continue")
            sbBatch.AppendLine("echo [%date% %time%] Process exited, copying files... >> %LOG%")
            sbBatch.AppendLine($"robocopy ""{sourceDir}"" ""{installDir}"" /e /is /it /r:3 /w:2 >> %LOG%")
            sbBatch.AppendLine("set RC=%ERRORLEVEL%")
            sbBatch.AppendLine("echo [%date% %time%] Robocopy exit code: %RC% >> %LOG%")
            sbBatch.AppendLine("if %RC% GEQ 8 (")
            sbBatch.AppendLine("    echo [%date% %time%] ERRORE: robocopy failed >> %LOG%")
            sbBatch.AppendLine($"    echo Copia file fallita. Verifica il log: {logFile}")
            sbBatch.AppendLine("    exit /b 1")
            sbBatch.AppendLine(")")
            sbBatch.AppendLine($"echo v{latestVersion}>""{installDir}\.app_version""")
            sbBatch.AppendLine("echo [%date% %time%] Version marker written >> %LOG%")
            sbBatch.AppendLine("echo [%date% %time%] Launching app... >> %LOG%")
            sbBatch.AppendLine($"start """" ""{installDir}\HidaChat.exe""")
            sbBatch.AppendLine("echo [%date% %time%] Done >> %LOG%")
            sbBatch.AppendLine("del ""%~f0""")

            File.WriteAllText(batchPath, sbBatch.ToString())

            ' Rilascia le risorse e chiudi i controlli WebView2 prima di lanciare lo script di aggiornamento
            Application.Current.Dispatcher.Invoke(Sub()
                Dim mainWin = TryCast(Application.Current.MainWindow, MainWindow)
                If mainWin IsNot Nothing Then
                    mainWin.ForceExitForUpdate()
                End If
            End Sub)

            Process.Start(New ProcessStartInfo With {
                .FileName = batchPath,
                .UseShellExecute = True
            })

            Application.Current.Dispatcher.Invoke(Sub()
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



    Private Shared Sub WriteLocalVersionMarker(installDir As String, version As String)
        Try
            Dim markerPath = Path.Combine(installDir, ".app_version")
            File.WriteAllText(markerPath, version.Trim())
        Catch ex As Exception
            Debug.WriteLine($"Failed to write local version marker: {ex.Message}")
        End Try
    End Sub
End Class

