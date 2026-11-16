Imports System.IO
Imports System.Text.Json

''' <summary>
''' Safety policy enforcing non-destructive behavior when accounts deserialization fails.
''' Protects existing WebView2 profiles and session databases from accidental wipe or overwriting.
''' </summary>
Public Class AccountLoadSafetyPolicy

    ''' <summary>
    ''' Checks whether save operations must be inhibited based on the load failed state.
    ''' </summary>
    Public Shared Function ShouldInhibitSave(loadFailed As Boolean) As Boolean
        Return loadFailed
    End Function

    ''' <summary>
    ''' Checks whether profile directory cleanups, moves, or deletions must be inhibited based on the load failed state.
    ''' </summary>
    Public Shared Function ShouldInhibitCleanup(loadFailed As Boolean) As Boolean
        Return loadFailed
    End Function

    ''' <summary>
    ''' Creates a safety backup copy of the settings file (by default appending .bak).
    ''' Returns the path of the created backup file if successful, or Nothing on failure.
    ''' </summary>
    Public Shared Function CreateBackup(settingsFilePath As String, Optional backupFilePath As String = Nothing) As String
        If String.IsNullOrWhiteSpace(settingsFilePath) Then Return Nothing
        Try
            If Not File.Exists(settingsFilePath) Then Return Nothing

            Dim targetBak = If(String.IsNullOrWhiteSpace(backupFilePath), settingsFilePath & ".bak", backupFilePath)
            Dim bakDir = Path.GetDirectoryName(targetBak)
            If Not String.IsNullOrEmpty(bakDir) AndAlso Not Directory.Exists(bakDir) Then
                Directory.CreateDirectory(bakDir)
            End If

            File.Copy(settingsFilePath, targetBak, overwrite:=True)
            AppLogger.LogApp($"[ACCOUNT_SAFETY_POLICY] Safety backup successfully created at '{targetBak}'")
            Return targetBak
        Catch ex As Exception
            AppLogger.LogApp($"[ACCOUNT_SAFETY_POLICY] Error creating backup '{settingsFilePath}': {ex.Message}")
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Validates whether an accounts JSON string is structurally well-formed and can be deserialized.
    ''' Returns False if the payload is empty, not an array, has an empty array, or has incompatible types.
    ''' </summary>
    Public Shared Function TryValidateAccountsJson(accountsJson As String, ByRef errorMessage As String) As Boolean
        errorMessage = Nothing
        If String.IsNullOrWhiteSpace(accountsJson) Then
            errorMessage = "Accounts JSON payload is null or whitespace."
            Return False
        End If

        Try
            Using doc = JsonDocument.Parse(accountsJson)
                If doc.RootElement.ValueKind <> JsonValueKind.Array Then
                    errorMessage = $"Expected JSON Array but got {doc.RootElement.ValueKind}."
                    Return False
                End If

                If doc.RootElement.GetArrayLength() = 0 Then
                    errorMessage = "Accounts JSON array is empty."
                    Return False
                End If

                For Each el In doc.RootElement.EnumerateArray()
                    If el.ValueKind <> JsonValueKind.Object Then
                        errorMessage = $"Array element is not an object (found {el.ValueKind})."
                        Return False
                    End If

                    Dim idProp As JsonElement = Nothing
                    If el.TryGetProperty("id", idProp) OrElse el.TryGetProperty("Id", idProp) Then
                        If idProp.ValueKind <> JsonValueKind.String AndAlso idProp.ValueKind <> JsonValueKind.Null Then
                            errorMessage = $"Field 'id' has invalid type {idProp.ValueKind} (expected String)."
                            Return False
                        End If
                    End If

                    Dim nameProp As JsonElement = Nothing
                    If el.TryGetProperty("name", nameProp) OrElse el.TryGetProperty("Name", nameProp) Then
                        If nameProp.ValueKind <> JsonValueKind.String AndAlso nameProp.ValueKind <> JsonValueKind.Null Then
                            errorMessage = $"Field 'name' has invalid type {nameProp.ValueKind} (expected String)."
                            Return False
                        End If
                    End If
                Next

                Return True
            End Using
        Catch ex As Exception
            errorMessage = ex.Message
            Return False
        End Try
    End Function

End Class
