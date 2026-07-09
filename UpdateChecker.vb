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
            Await PerformUpdateAsync(latestVersion)
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

    Private Shared Async Function PerformUpdateAsync(latestVersion As String) As Task
        Await Task.Run(Sub()
                           Try
                               Dim installDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName)
                               Dim tempDir = Path.Combine(Path.GetTempPath(), "WhatsAppVB_Update")

                               If Directory.Exists(tempDir) Then Directory.Delete(tempDir, True)
                               Directory.CreateDirectory(tempDir)

                               For Each file In Directory.GetFiles(Constants.UpdateFilesPath, "*.*", SearchOption.AllDirectories)
                                   Dim destFile = file.Replace(Constants.UpdateFilesPath, tempDir + "\")
                                   Dim destDir = Path.GetDirectoryName(destFile)
                                   If Not Directory.Exists(destDir) Then Directory.CreateDirectory(destDir)
                                   file.Copy(file, destFile, True)
                               Next

                               Dim batchPath = Path.Combine(tempDir, "update.bat")
                               Dim batchContent = $"@echo off
ping 127.0.0.1 -n 4 > nul
xcopy /y /e /q ""{tempDir}\*.*"" ""{installDir}\"" > nul
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
