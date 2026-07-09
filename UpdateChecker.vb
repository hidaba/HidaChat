Imports System.IO
Imports System.Diagnostics
Imports System.Threading.Tasks
Imports System.Windows

Public Class UpdateChecker
    Private Shared _hasChecked As Boolean = False

    Public Shared Async Function CheckForUpdatesAsync(
        settings As SettingsController,
        accountManager As AccountManager,
        Optional force As Boolean = False
    ) As Task
        If Not force AndAlso _hasChecked Then Return
        _hasChecked = True

        If Not force AndAlso Not settings.CheckForUpdates Then
            Debug.WriteLine("Update check on launch is disabled by user.")
            Return
        End If

        Dim installDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)

        ' If running directly from the OTA repository, skip update
        If installDir.TrimEnd("\"c).Equals(Constants.UpdateFilesPath.TrimEnd("\"c), StringComparison.OrdinalIgnoreCase) Then
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

        Dim latestVersion = Await ReadVersionFromFileAsync()

        If String.IsNullOrEmpty(latestVersion) Then
            Debug.WriteLine("Update check failed: could not read version file.")
            If force Then
                MessageBox.Show(
                    "Impossibile leggere il file degli aggiornamenti." & vbCrLf &
                    "Verifica la connettività al percorso di rete: " & Constants.UpdateVersionFile,
                    "Errore connessione",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                )
            End If
            Return
        End If

        Debug.WriteLine($"Current version: {Constants.AppVersion}, Remote version: {latestVersion}")

        If latestVersion <> Constants.AppVersion Then
            Await PerformUpdateAsync(latestVersion, installDir)
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

    Private Shared Async Function ReadVersionFromFileAsync() As Task(Of String)
        Try
            Return (Await File.ReadAllTextAsync(Constants.UpdateVersionFile)).Trim()
        Catch ex As Exception
            Debug.WriteLine($"Error reading version file: {ex.Message}")
            Return Nothing
        End Try
    End Function

    Private Shared Async Function PerformUpdateAsync(latestVersion As String, installDir As String) As Task
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
                            Constants.UpdateFilesPath & vbCrLf & vbCrLf &
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

                For Each f In Directory.GetFiles(Constants.UpdateFilesPath, "*.*", SearchOption.AllDirectories)
                    Dim destFile = f.Replace(Constants.UpdateFilesPath, tempDir + "\")
                    Dim destDir = Path.GetDirectoryName(destFile)
                    If Not Directory.Exists(destDir) Then Directory.CreateDirectory(destDir)
                    IO.File.Copy(f, destFile, True)
                Next

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
End Class
