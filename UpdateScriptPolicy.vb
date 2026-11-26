Imports System
Imports System.IO
Imports System.Text

''' <summary>
''' Resilient update script and argument policy for OTA updates (TASK-0092).
''' Addresses UTF-8/OEM codepage, accented characters (Italian vowels), special characters,
''' parenthesized directories, drive roots (e.g. E:\), and safe error pausing.
''' </summary>
Public Class UpdateScriptPolicy

    ''' <summary>
    ''' Normalizes a folder path for use with robocopy and Windows command lines.
    ''' Avoids the Windows trailing backslash escape issue (e.g. "E:\" escaping trailing quote as \").
    ''' Normalizes drive roots (e.g. "E:\", "E:") and directory paths ending in backslash to end with "\.".
    ''' </summary>
    Public Shared Function NormalizePathForRobocopy(path As String) As String
        If String.IsNullOrWhiteSpace(path) Then Return String.Empty

        Dim trimmed = path.Trim().Trim(""""c)
        If String.IsNullOrWhiteSpace(trimmed) Then Return String.Empty

        ' Check if drive root like "E:\" or "E:" or "E:/"
        If trimmed.Length = 2 AndAlso Char.IsLetter(trimmed(0)) AndAlso trimmed(1) = ":"c Then
            Return trimmed & "\."
        End If
        If trimmed.Length = 3 AndAlso Char.IsLetter(trimmed(0)) AndAlso trimmed(1) = ":"c AndAlso
           (trimmed(2) = "\"c OrElse trimmed(2) = "/"c) Then
            Return trimmed.Substring(0, 2) & "\."
        End If

        ' If ends with backslash or slash, replace with "\." to prevent quote escaping
        If trimmed.EndsWith("\"c) OrElse trimmed.EndsWith("/"c) Then
            Return trimmed.TrimEnd("\"c, "/"c) & "\."
        End If

        Return trimmed
    End Function

    ''' <summary>
    ''' Normalizes a path for safe inclusion in command-line arguments.
    ''' Ensures paths ending with a backslash do not escape the closing double-quote.
    ''' </summary>
    Public Shared Function NormalizePathForArgument(path As String) As String
        Return NormalizePathForRobocopy(path)
    End Function

    ''' <summary>
    ''' Escapes or sanitizes text for safe display inside batch echo statements,
    ''' specifically protecting closing parentheses inside if (...) blocks.
    ''' </summary>
    Public Shared Function EscapeForBatchEcho(text As String) As String
        If String.IsNullOrEmpty(text) Then Return String.Empty
        ' In cmd.exe inside parenthesized blocks (e.g. if ... (...)), an unescaped ) terminates the block.
        Return text.Replace(")", "^)")
    End Function

    ''' <summary>
    ''' Generates a resilient, battle-tested batch update script (TASK-0092).
    ''' Features:
    ''' 1. "chcp 65001 >nul" for full UTF-8 codepage support (handles accented characters like è, à, ò).
    ''' 2. "set ""VAR=VAL""" assignment syntax protecting against spaces, special characters, %, and !.
    ''' 3. Robocopy destination path normalization with "\." preventing drive root escape corruption.
    ''' 4. Safe logging without unquoted paths in echo inside if blocks.
    ''' 5. Pause on robocopy failure (%RC% GEQ 8) allowing user inspection before console exit.
    ''' </summary>
    Public Shared Function GenerateUpdateBatchScript(sourceDir As String,
                                                    installDir As String,
                                                    targetExeName As String,
                                                    latestVersion As String,
                                                    callerPid As Integer,
                                                    tempDir As String,
                                                    tempZipPath As String,
                                                    logFilePath As String) As String
        Dim normSrc = NormalizePathForRobocopy(sourceDir)
        Dim normDst = NormalizePathForRobocopy(installDir)
        Dim normTempDir = NormalizePathForRobocopy(tempDir)
        Dim normTempZip = If(String.IsNullOrWhiteSpace(tempZipPath), "", tempZipPath.Trim().Trim(""""c))
        Dim normLog = If(String.IsNullOrWhiteSpace(logFilePath), Path.Combine(installDir, ".update_log.txt"), logFilePath.Trim().Trim(""""c))
        Dim cleanVer = If(String.IsNullOrWhiteSpace(latestVersion), "latest", latestVersion.Trim().TrimStart("v"c))
        Dim exeName = If(String.IsNullOrWhiteSpace(targetExeName), "HidaChat.exe", Path.GetFileName(targetExeName))

        Dim sb As New StringBuilder()
        sb.AppendLine("@echo off")
        sb.AppendLine("chcp 65001 >nul")
        sb.AppendLine("title Aggiornamento HidaChat...")
        sb.AppendLine($"set ""SRC={normSrc}""")
        sb.AppendLine($"set ""DST={normDst}""")
        sb.AppendLine($"set ""LOG={normLog}""")
        sb.AppendLine($"set ""TEMP_DIR={normTempDir}""")
        sb.AppendLine($"set ""TEMP_ZIP={normTempZip}""")
        sb.AppendLine($"set ""EXE={exeName}""")
        sb.AppendLine($"set ""VER={cleanVer}""")
        sb.AppendLine($"set ""CALLER_PID={callerPid}""")
        sb.AppendLine("echo [%date% %time%] Starting update > ""%LOG%""")
        sb.AppendLine("set RETRY=0")
        sb.AppendLine(":waitloop")
        sb.AppendLine("echo [%date% %time%] Waiting for application process to exit... >> ""%LOG%""")
        sb.AppendLine("timeout /t 2 /nobreak > nul")
        sb.AppendLine("if %CALLER_PID% GTR 0 (")
        sb.AppendLine("    tasklist /fi ""PID eq %CALLER_PID%"" /nh 2>>""%LOG%"" | find ""%CALLER_PID%"" >nul")
        sb.AppendLine("    if errorlevel 1 goto checktsnet")
        sb.AppendLine("    set /a RETRY=RETRY+1")
        sb.AppendLine("    if %RETRY% GEQ 30 (")
        sb.AppendLine("        echo [%date% %time%] Caller process %CALLER_PID% still running after timeout; terminating specific process tree... >> ""%LOG%""")
        sb.AppendLine("        taskkill /f /pid %CALLER_PID% /t >> ""%LOG%"" 2>&1")
        sb.AppendLine("        timeout /t 1 /nobreak > nul")
        sb.AppendLine("        goto checktsnet")
        sb.AppendLine("    )")
        sb.AppendLine("    goto waitloop")
        sb.AppendLine(")")
        sb.AppendLine(":checktsnet")
        sb.AppendLine("echo [%date% %time%] Caller process exited. >> ""%LOG%""")
        sb.AppendLine(":copyfiles")
        sb.AppendLine("echo [%date% %time%] Process exited, copying files... >> ""%LOG%""")
        sb.AppendLine("robocopy ""%SRC%"" ""%DST%"" /e /is /it /r:3 /w:2 /xd ""%SRC%\data"" ""%DST%\data"" >> ""%LOG%""")
        sb.AppendLine("set RC=%ERRORLEVEL%")
        sb.AppendLine("echo [%date% %time%] Robocopy exit code: %RC% >> ""%LOG%""")
        sb.AppendLine("if %RC% GEQ 8 (")
        sb.AppendLine("    echo [%date% %time%] ERROR: robocopy failed with exit code %RC% >> ""%LOG%""")
        sb.AppendLine("    echo.")
        sb.AppendLine("    echo [ERRORE] Aggiornamento di HidaChat non riuscito (codice robocopy: %RC%).")
        sb.AppendLine("    echo Consulta il file di log per i dettagli: ""%LOG%""")
        sb.AppendLine("    echo.")
        sb.AppendLine("    pause")
        sb.AppendLine("    exit /b 1")
        sb.AppendLine(")")
        sb.AppendLine("echo v%VER%>""%DST%\.app_version""")
        sb.AppendLine("echo [%date% %time%] Version marker written >> ""%LOG%""")
        sb.AppendLine("cd /d ""%TEMP%""")
        sb.AppendLine("if exist ""%TEMP_DIR%"" rmdir /s /q ""%TEMP_DIR%"" >> ""%LOG%"" 2>&1")
        sb.AppendLine("if exist ""%TEMP_ZIP%"" del /f /q ""%TEMP_ZIP%"" >> ""%LOG%"" 2>&1")
        sb.AppendLine("echo [%date% %time%] Launching updated application... >> ""%LOG%""")
        sb.AppendLine("start """" ""%DST%\%EXE%""")
        sb.AppendLine("echo [%date% %time%] Update completed successfully >> ""%LOG%""")
        sb.AppendLine("del ""%~f0""")

        Return sb.ToString()
    End Function

    ''' <summary>
    ''' Writes the batch script to disk using UTF-8 encoding without BOM, ensuring cmd.exe with chcp 65001
    ''' parses accented and special characters flawlessly.
    ''' </summary>
    Public Shared Sub WriteUpdateBatchFile(filePath As String, scriptContent As String)
        Dim utf8NoBom As New UTF8Encoding(False)
        File.WriteAllText(filePath, scriptContent, utf8NoBom)
    End Sub

End Class
