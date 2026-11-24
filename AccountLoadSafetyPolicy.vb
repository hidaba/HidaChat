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

    ''' <summary>
    ''' Checks whether a proxy port is valid for a given platform (TASK-0124).
    ''' A valid port must be in [DefaultPort, DefaultPort + 99] for platforms requiring local proxy,
    ''' or 0 for platforms that do not require a local proxy.
    ''' </summary>
    Public Shared Function IsValidProxyPortForPlatform(platform As ChatPlatform, port As Integer) As Boolean
        Dim def = PlatformRegistry.Get(platform)
        If def Is Nothing OrElse Not def.RequiresLocalProxy Then
            Return port = 0
        End If
        Return port >= def.DefaultPort AndAlso port < def.DefaultPort + 100
    End Function

    ''' <summary>
    ''' Checks whether a proxy port is valid for a given platform name string (TASK-0124).
    ''' </summary>
    Public Shared Function IsValidProxyPortForPlatform(platformStr As String, port As Integer) As Boolean
        Return IsValidProxyPortForPlatform(PlatformRegistry.FromKey(platformStr), port)
    End Function

    ''' <summary>
    ''' Resolves a valid, deterministic proxy port within the platform's default range,
    ''' avoiding collisions with already assigned ports (TASK-0124).
    ''' </summary>
    Public Shared Function ResolveDeterministicPort(platform As ChatPlatform, usedPorts As ISet(Of Integer)) As Integer
        Dim def = PlatformRegistry.Get(platform)
        If def Is Nothing OrElse Not def.RequiresLocalProxy Then Return 0
        Dim targetPort = def.DefaultPort
        While usedPorts IsNot Nothing AndAlso usedPorts.Contains(targetPort)
            targetPort += 1
        End While
        Return targetPort
    End Function

    ''' <summary>
    ''' Resolves a valid, deterministic proxy port within the platform's default range for a platform name string (TASK-0124).
    ''' </summary>
    Public Shared Function ResolveDeterministicPort(platformStr As String, usedPorts As ISet(Of Integer)) As Integer
        Return ResolveDeterministicPort(PlatformRegistry.FromKey(platformStr), usedPorts)
    End Function

    ''' <summary>
    ''' Evaluates whether an anonymous orphan profile (WV2Profile_) can safely be migrated to an account profile
    ''' without overwriting, replacing, or colliding with an existing valid profile, backup, or snapshot (TASK-0090).
    ''' Returns True ONLY if the orphan directory exists and NO valid target profile, backup, or snapshot exists on disk.
    ''' </summary>
    Public Shared Function CanSafelyMigrateOrphanProfile(sharedDir As String, accountId As String) As Boolean
        If String.IsNullOrWhiteSpace(sharedDir) OrElse String.IsNullOrWhiteSpace(accountId) Then
            Return False
        End If

        Dim orphan = Path.Combine(sharedDir, "WV2Profile_")
        If Not Directory.Exists(orphan) Then
            Return False
        End If

        ' 1. Check legacy target directory and all its snapshot/backup variants
        Dim legacyTarget = Path.Combine(sharedDir, $"{ProfileIsolationPolicy.LegacyProfilePrefix}{accountId}")
        If Directory.Exists(legacyTarget) OrElse
           Directory.Exists(legacyTarget & ".previous") OrElse
           Directory.Exists(legacyTarget & ".bak") OrElse
           Directory.Exists(legacyTarget & ".migrated") OrElse
           Directory.Exists(legacyTarget & "_Snapshot") Then
            Return False
        End If

        ' 2. Check native profile directory under EBWebView and all its snapshot/backup variants
        Dim nativeTarget = ProfileIsolationPolicy.GetNativeProfileDirectory(sharedDir, accountId)
        If Not String.IsNullOrEmpty(nativeTarget) Then
            If Directory.Exists(nativeTarget) OrElse
               Directory.Exists(nativeTarget & ".previous") OrElse
               Directory.Exists(nativeTarget & ".bak") OrElse
               Directory.Exists(nativeTarget & ".migrated") OrElse
               Directory.Exists(nativeTarget & "_Snapshot") Then
                Return False
            End If
        End If

        ' 3. Check alternate subdirectories under EBWebView (e.g. EBWebView/Account_{id} or safe name)
        Dim ebDir = Path.Combine(sharedDir, ProfileIsolationPolicy.SharedEbWebViewFolder)
        If Directory.Exists(ebDir) Then
            Dim targetFolder = ProfileIsolationPolicy.GetNativeProfileFolderName(accountId)
            Dim betaName = ProfileIsolationPolicy.GetSafeProfileName(accountId)
            If Directory.Exists(Path.Combine(ebDir, targetFolder)) OrElse
               Directory.Exists(Path.Combine(ebDir, betaName)) OrElse
               Directory.Exists(Path.Combine(ebDir, "Account_" & accountId)) Then
                Return False
            End If
        End If

        ' 4. Check effective profile directory resolved by policy
        Dim effectiveTarget = ProfileIsolationPolicy.ResolveEffectiveProfileDirectory(sharedDir, accountId)
        If Not String.IsNullOrEmpty(effectiveTarget) Then
            If Directory.Exists(effectiveTarget) OrElse
               Directory.Exists(effectiveTarget & ".previous") OrElse
               Directory.Exists(effectiveTarget & ".bak") OrElse
               Directory.Exists(effectiveTarget & ".migrated") OrElse
               Directory.Exists(effectiveTarget & "_Snapshot") Then
                Return False
            End If
        End If

        Return True
    End Function

End Class
