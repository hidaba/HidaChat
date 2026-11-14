Imports System.IO
Imports System.Text
Imports System.Data
Imports System.Linq
Imports ExcelDataReader

''' <summary>
''' Servizio per il caricamento, parsing e normalizzazione di elenchi contatti da file Excel (.xlsx, .xls) e CSV.
''' Supporta il riconoscimento di numeri di telefono, @username (Telegram), nome, cognome, azienda e testo personalizzato.
''' </summary>
Public Class ExcelContactService
    Shared Sub New()
        Try
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
        Catch
        End Try
    End Sub

    ''' <summary>
    ''' Carica la lista dei contatti leggendo il file specificato (.xlsx, .xls, .csv).
    ''' </summary>
    Public Shared Function LoadContactsFromFile(filePath As String) As List(Of BulkContactItem)
        If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then
            Throw New FileNotFoundException("File non trovato.", filePath)
        End If

        Dim ext = Path.GetExtension(filePath).ToLowerInvariant()
        If ext = ".csv" OrElse ext = ".txt" Then
            Return LoadContactsFromCsv(filePath)
        ElseIf ext = ".xlsx" OrElse ext = ".xls" Then
            Return LoadContactsFromExcel(filePath)
        Else
            Throw New NotSupportedException($"Formato file '{ext}' non supportato. Usa file .xlsx, .xls o .csv.")
        End If
    End Function

    ''' <summary>
    ''' Legge un foglio di calcolo Excel (.xlsx o .xls) utilizzando ExcelDataReader.
    ''' </summary>
    Private Shared Function LoadContactsFromExcel(filePath As String) As List(Of BulkContactItem)
        Dim items As New List(Of BulkContactItem)()

        Using stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            Using reader = ExcelReaderFactory.CreateReader(stream)
                Dim result = reader.AsDataSet(New ExcelDataSetConfiguration() With {
                    .ConfigureDataTable = Function(__) New ExcelDataTableConfiguration() With {
                        .UseHeaderRow = False
                    }
                })

                If result.Tables.Count = 0 Then Return items

                Dim table = result.Tables(0)
                If table.Rows.Count = 0 Then Return items

                Dim phoneCol As Integer = -1
                Dim usernameCol As Integer = -1
                Dim firstNameCol As Integer = -1
                Dim lastNameCol As Integer = -1
                Dim companyCol As Integer = -1
                Dim customTextCol As Integer = -1
                Dim startRowIndex As Integer = 0

                Dim firstRow = table.Rows(0)
                Dim colCount = table.Columns.Count

                For c As Integer = 0 To colCount - 1
                    Dim val = firstRow(c)?.ToString()?.Trim()?.ToLowerInvariant()
                    If String.IsNullOrEmpty(val) Then Continue For

                    If phoneCol = -1 AndAlso IsPhoneHeader(val) Then
                        phoneCol = c
                    ElseIf usernameCol = -1 AndAlso IsUsernameHeader(val) Then
                        usernameCol = c
                    ElseIf firstNameCol = -1 AndAlso IsFirstNameHeader(val) Then
                        firstNameCol = c
                    ElseIf lastNameCol = -1 AndAlso IsLastNameHeader(val) Then
                        lastNameCol = c
                    ElseIf companyCol = -1 AndAlso IsCompanyHeader(val) Then
                        companyCol = c
                    ElseIf customTextCol = -1 AndAlso IsTextHeader(val) Then
                        customTextCol = c
                    End If
                Next

                If phoneCol <> -1 OrElse usernameCol <> -1 OrElse firstNameCol <> -1 OrElse lastNameCol <> -1 OrElse companyCol <> -1 OrElse customTextCol <> -1 Then
                    startRowIndex = 1
                End If

                ' Mappatura fallback se non sono state trovate intestazioni esplicite
                If phoneCol = -1 AndAlso usernameCol = -1 Then phoneCol = 0
                If firstNameCol = -1 AndAlso colCount > 1 Then firstNameCol = 1
                If lastNameCol = -1 AndAlso colCount > 2 Then lastNameCol = 2
                If companyCol = -1 AndAlso colCount > 3 Then companyCol = 3
                If customTextCol = -1 AndAlso colCount > 4 Then customTextCol = 4

                For r As Integer = startRowIndex To table.Rows.Count - 1
                    Dim row = table.Rows(r)
                    Dim rawPhone = If(phoneCol >= 0 AndAlso phoneCol < colCount, FormatCellValue(row(phoneCol)), "")
                    Dim rawUser = If(usernameCol >= 0 AndAlso usernameCol < colCount, FormatCellValue(row(usernameCol)), "")
                    Dim rawFirst = If(firstNameCol >= 0 AndAlso firstNameCol < colCount, FormatCellValue(row(firstNameCol)), "")
                    Dim rawLast = If(lastNameCol >= 0 AndAlso lastNameCol < colCount, FormatCellValue(row(lastNameCol)), "")
                    Dim rawComp = If(companyCol >= 0 AndAlso companyCol < colCount, FormatCellValue(row(companyCol)), "")
                    Dim rawText = If(customTextCol >= 0 AndAlso customTextCol < colCount, FormatCellValue(row(customTextCol)), "")

                    ' Se il numero di telefono contiene un @ e username è vuoto, potrebbe essere stato scambiato
                    If String.IsNullOrWhiteSpace(rawUser) AndAlso rawPhone.StartsWith("@") Then
                        rawUser = rawPhone
                        rawPhone = String.Empty
                    End If

                    Dim cleanPhone = CleanPhoneNumber(rawPhone)
                    Dim cleanUser = rawUser.Trim()

                    If Not String.IsNullOrWhiteSpace(cleanPhone) OrElse Not String.IsNullOrWhiteSpace(cleanUser) OrElse Not String.IsNullOrWhiteSpace(rawFirst) Then
                        items.Add(New BulkContactItem With {
                            .Phone = cleanPhone,
                            .Username = cleanUser,
                            .FirstName = rawFirst,
                            .LastName = rawLast,
                            .Company = rawComp,
                            .CustomText = rawText,
                            .Status = "In attesa"
                        })
                    End If
                Next
            End Using
        End Using

        Return items
    End Function

    ''' <summary>
    ''' Legge un file CSV supportando vari separatori (, ; \t), gestione campi tra virgolette multilinea (RFC 4180) e quote escaping.
    ''' </summary>
    Public Shared Function LoadContactsFromCsv(filePath As String) As List(Of BulkContactItem)
        Dim items As New List(Of BulkContactItem)()
        If Not File.Exists(filePath) Then Return items

        Dim csvContent = File.ReadAllText(filePath, Encoding.UTF8)
        If String.IsNullOrWhiteSpace(csvContent) Then Return items

        Dim delimiter = DetectCsvDelimiter(csvContent)
        Dim parsedRows = ParseCsvRecords(csvContent, delimiter)

        If parsedRows.Count = 0 Then Return items

        Dim phoneCol As Integer = -1
        Dim usernameCol As Integer = -1
        Dim firstNameCol As Integer = -1
        Dim lastNameCol As Integer = -1
        Dim companyCol As Integer = -1
        Dim customTextCol As Integer = -1
        Dim startRowIndex As Integer = 0

        Dim firstRow = parsedRows(0)
        Dim colCount = firstRow.Count

        For c As Integer = 0 To colCount - 1
            Dim val = firstRow(c).Trim().ToLowerInvariant()
            If String.IsNullOrEmpty(val) Then Continue For

            If phoneCol = -1 AndAlso IsPhoneHeader(val) Then
                phoneCol = c
            ElseIf usernameCol = -1 AndAlso IsUsernameHeader(val) Then
                usernameCol = c
            ElseIf firstNameCol = -1 AndAlso IsFirstNameHeader(val) Then
                firstNameCol = c
            ElseIf lastNameCol = -1 AndAlso IsLastNameHeader(val) Then
                lastNameCol = c
            ElseIf companyCol = -1 AndAlso IsCompanyHeader(val) Then
                companyCol = c
            ElseIf customTextCol = -1 AndAlso IsTextHeader(val) Then
                customTextCol = c
            End If
        Next

        If phoneCol <> -1 OrElse usernameCol <> -1 OrElse firstNameCol <> -1 OrElse lastNameCol <> -1 OrElse companyCol <> -1 OrElse customTextCol <> -1 Then
            startRowIndex = 1
        End If

        If phoneCol = -1 AndAlso usernameCol = -1 Then phoneCol = 0
        If firstNameCol = -1 AndAlso colCount > 1 Then firstNameCol = 1
        If lastNameCol = -1 AndAlso colCount > 2 Then lastNameCol = 2
        If companyCol = -1 AndAlso colCount > 3 Then companyCol = 3
        If customTextCol = -1 AndAlso colCount > 4 Then customTextCol = 4

        For r As Integer = startRowIndex To parsedRows.Count - 1
            Dim row = parsedRows(r)
            Dim rawPhone = If(phoneCol >= 0 AndAlso phoneCol < row.Count, row(phoneCol), "")
            Dim rawUser = If(usernameCol >= 0 AndAlso usernameCol < row.Count, row(usernameCol), "")
            Dim rawFirst = If(firstNameCol >= 0 AndAlso firstNameCol < row.Count, row(firstNameCol), "")
            Dim rawLast = If(lastNameCol >= 0 AndAlso lastNameCol < row.Count, row(lastNameCol), "")
            Dim rawComp = If(companyCol >= 0 AndAlso companyCol < row.Count, row(companyCol), "")
            Dim rawText = If(customTextCol >= 0 AndAlso customTextCol < row.Count, row(customTextCol), "")

            If String.IsNullOrWhiteSpace(rawUser) AndAlso rawPhone.StartsWith("@") Then
                rawUser = rawPhone
                rawPhone = String.Empty
            End If

            Dim cleanPhone = CleanPhoneNumber(rawPhone)
            Dim cleanUser = rawUser.Trim()

            If Not String.IsNullOrWhiteSpace(cleanPhone) OrElse Not String.IsNullOrWhiteSpace(cleanUser) OrElse Not String.IsNullOrWhiteSpace(rawFirst) Then
                items.Add(New BulkContactItem With {
                    .Phone = cleanPhone,
                    .Username = cleanUser,
                    .FirstName = rawFirst,
                    .LastName = rawLast,
                    .Company = rawComp,
                    .CustomText = rawText,
                    .Status = "In attesa"
                })
            End If
        Next

        Return items
    End Function

    ''' <summary>
    ''' Rileva il delimitatore CSV (, ; \t) analizzando i caratteri al di fuori dei campi quotati (RFC 4180).
    ''' Ignora virgole, punti e virgola o tabulazioni racchiusi tra doppi apici (TODO #84).
    ''' </summary>
    Public Shared Function DetectCsvDelimiter(csvContent As String) As Char
        If String.IsNullOrEmpty(csvContent) Then Return ","c

        Dim commaCount = 0
        Dim semiCount = 0
        Dim tabCount = 0
        Dim inQuotes = False
        Dim lineCount = 0
        Dim maxLinesToSample = 10
        Dim maxCharsToSample = Math.Min(csvContent.Length, 8192)

        Dim i = 0
        While i < maxCharsToSample
            Dim c = csvContent(i)
            If c = """"c Then
                If inQuotes AndAlso i + 1 < maxCharsToSample AndAlso csvContent(i + 1) = """"c Then
                    i += 1
                Else
                    inQuotes = Not inQuotes
                End If
            ElseIf Not inQuotes Then
                If c = ","c Then
                    commaCount += 1
                ElseIf c = ";"c Then
                    semiCount += 1
                ElseIf c = ControlChars.Tab Then
                    tabCount += 1
                ElseIf c = ControlChars.Lf Then
                    lineCount += 1
                    If lineCount >= maxLinesToSample Then Exit While
                End If
            End If
            i += 1
        End While

        If semiCount > commaCount AndAlso semiCount > tabCount Then Return ";"c
        If tabCount > commaCount AndAlso tabCount > semiCount Then Return ControlChars.Tab
        Return ","c
    End Function

    ''' <summary>
    ''' Overload per array di righe testuali.
    ''' </summary>
    Public Shared Function DetectCsvDelimiter(lines As String()) As Char
        If lines Is Nothing OrElse lines.Length = 0 Then Return ","c
        Return DetectCsvDelimiter(String.Join(vbLf, lines))
    End Function

    ''' <summary>
    ''' Analizza l'intero contenuto CSV in record conformi allo standard RFC 4180.
    ''' Mantiene i ritorni a capo all'interno di campi racchiusi tra virgolette, gestisce l'escape delle virgolette doppie ("")
    ''' e separa correttamente le righe senza rompere i messaggi multilinea (TODO #84).
    ''' </summary>
    Public Shared Function ParseCsvRecords(csvContent As String, delimiter As Char) As List(Of List(Of String))
        Dim records As New List(Of List(Of String))()
        If String.IsNullOrWhiteSpace(csvContent) Then Return records

        Dim currentRecord As New List(Of String)()
        Dim currentField As New StringBuilder()
        Dim inQuotes = False
        Dim i = 0
        Dim len = csvContent.Length

        While i < len
            Dim c = csvContent(i)

            If c = """"c Then
                If inQuotes AndAlso i + 1 < len AndAlso csvContent(i + 1) = """"c Then
                    currentField.Append(""""c)
                    i += 1
                Else
                    inQuotes = Not inQuotes
                End If
            ElseIf c = delimiter AndAlso Not inQuotes Then
                currentRecord.Add(currentField.ToString().Trim())
                currentField.Clear()
            ElseIf (c = ControlChars.Cr OrElse c = ControlChars.Lf) AndAlso Not inQuotes Then
                If c = ControlChars.Cr AndAlso i + 1 < len AndAlso csvContent(i + 1) = ControlChars.Lf Then
                    i += 1
                End If
                currentRecord.Add(currentField.ToString().Trim())
                currentField.Clear()

                If currentRecord.Count > 1 OrElse (currentRecord.Count = 1 AndAlso Not String.IsNullOrWhiteSpace(currentRecord(0))) Then
                    records.Add(currentRecord)
                End If
                currentRecord = New List(Of String)()
            Else
                currentField.Append(c)
            End If

            i += 1
        End While

        If currentField.Length > 0 OrElse currentRecord.Count > 0 Then
            currentRecord.Add(currentField.ToString().Trim())
            If currentRecord.Count > 1 OrElse (currentRecord.Count = 1 AndAlso Not String.IsNullOrWhiteSpace(currentRecord(0))) Then
                records.Add(currentRecord)
            End If
        End If

        Return records
    End Function

    ''' <summary>
    ''' Analizza una singola riga CSV (mantenuto per retrocompatibilitÃ ).
    ''' </summary>
    Public Shared Function ParseCsvLine(line As String, delimiter As Char) As List(Of String)
        Dim res = ParseCsvRecords(line, delimiter)
        If res.Count > 0 Then Return res(0)
        Return New List(Of String)()
    End Function

    Private Shared Function FormatCellValue(cellValue As Object) As String
        If cellValue Is Nothing OrElse IsDBNull(cellValue) Then Return String.Empty
        
        If TypeOf cellValue Is Double Then
            Dim d = CDbl(cellValue)
            If d = Math.Floor(d) Then
                Return CLng(d).ToString()
            End If
        End If

        Return cellValue.ToString().Trim()
    End Function

    Private Shared Function CleanPhoneNumber(raw As String) As String
        If String.IsNullOrWhiteSpace(raw) Then Return String.Empty
        Dim trimmed = raw.Trim().Replace(" ", "").Replace("-", "").Replace(".", "").Replace("(", "").Replace(")", "").Replace("/", "")
        If trimmed.Contains("E+", StringComparison.OrdinalIgnoreCase) Then
            Dim dbl As Double
            If Double.TryParse(trimmed, Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, dbl) Then
                Return CLng(dbl).ToString()
            End If
        End If
        Return trimmed
    End Function

    Private Shared Function IsPhoneHeader(h As String) As Boolean
        Return h.Contains("telef") OrElse h.Contains("phone") OrElse h.Contains("cell") OrElse
               h.Contains("mobil") OrElse h.Contains("num") OrElse h.Contains("contatt") OrElse h = "tel"
    End Function

    Private Shared Function IsUsernameHeader(h As String) As Boolean
        Return h.Contains("username") OrElse h.Contains("utente") OrElse h.Contains("user") OrElse
               h.Contains("nick") OrElse h.Contains("handle") OrElse h.Contains("telegram") OrElse
               h.Contains("tg") OrElse h.StartsWith("@")
    End Function

    Private Shared Function IsFirstNameHeader(h As String) As Boolean
        Return (h.Contains("nome") AndAlso Not h.Contains("cognome")) OrElse
               h = "name" OrElse h = "firstname" OrElse h = "first name" OrElse h = "first_name"
    End Function

    Private Shared Function IsLastNameHeader(h As String) As Boolean
        Return h.Contains("cognome") OrElse h.Contains("surname") OrElse
               h = "lastname" OrElse h = "last name" OrElse h = "last_name"
    End Function

    Private Shared Function IsCompanyHeader(h As String) As Boolean
        Return h.Contains("aziend") OrElse h.Contains("company") OrElse h.Contains("societ") OrElse
               h.Contains("ditta") OrElse h.Contains("ragione") OrElse h.Contains("ragione_sociale") OrElse h.Contains("business")
    End Function

    Private Shared Function IsTextHeader(h As String) As Boolean
        Return h.Contains("testo") OrElse h.Contains("messagg") OrElse h.Contains("text") OrElse
               h.Contains("message") OrElse h.Contains("msg") OrElse h.Contains("body") OrElse h.Contains("note")
    End Function
End Class
