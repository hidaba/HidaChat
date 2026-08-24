Imports System.IO
Imports System.Text
Imports System.Data
Imports System.Linq
Imports ExcelDataReader

''' <summary>
''' Servizio per il caricamento, parsing e normalizzazione di elenchi contatti da file Excel (.xlsx, .xls) e CSV.
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

                If phoneCol <> -1 OrElse firstNameCol <> -1 OrElse lastNameCol <> -1 OrElse companyCol <> -1 OrElse customTextCol <> -1 Then
                    startRowIndex = 1
                End If

                If phoneCol = -1 Then phoneCol = 0
                If firstNameCol = -1 AndAlso colCount > 1 Then firstNameCol = 1
                If lastNameCol = -1 AndAlso colCount > 2 Then lastNameCol = 2
                If companyCol = -1 AndAlso colCount > 3 Then companyCol = 3
                If customTextCol = -1 AndAlso colCount > 4 Then customTextCol = 4

                For r As Integer = startRowIndex To table.Rows.Count - 1
                    Dim row = table.Rows(r)
                    Dim rawPhone = If(phoneCol >= 0 AndAlso phoneCol < colCount, FormatCellValue(row(phoneCol)), "")
                    Dim rawFirst = If(firstNameCol >= 0 AndAlso firstNameCol < colCount, FormatCellValue(row(firstNameCol)), "")
                    Dim rawLast = If(lastNameCol >= 0 AndAlso lastNameCol < colCount, FormatCellValue(row(lastNameCol)), "")
                    Dim rawComp = If(companyCol >= 0 AndAlso companyCol < colCount, FormatCellValue(row(companyCol)), "")
                    Dim rawText = If(customTextCol >= 0 AndAlso customTextCol < colCount, FormatCellValue(row(customTextCol)), "")

                    Dim cleanPhone = CleanPhoneNumber(rawPhone)
                    If Not String.IsNullOrWhiteSpace(cleanPhone) OrElse Not String.IsNullOrWhiteSpace(rawFirst) Then
                        items.Add(New BulkContactItem With {
                            .Phone = cleanPhone,
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
    ''' Legge un file CSV supportando vari separatori (, ; \t) e gestione dei doppi apici.
    ''' </summary>
    Private Shared Function LoadContactsFromCsv(filePath As String) As List(Of BulkContactItem)
        Dim items As New List(Of BulkContactItem)()
        Dim lines = File.ReadAllLines(filePath, Encoding.UTF8)
        If lines.Length = 0 Then Return items

        Dim delimiter = DetectCsvDelimiter(lines)

        Dim parsedRows As New List(Of List(Of String))()
        For Each line In lines
            If String.IsNullOrWhiteSpace(line) Then Continue For
            parsedRows.Add(ParseCsvLine(line, delimiter))
        Next

        If parsedRows.Count = 0 Then Return items

        Dim phoneCol As Integer = -1
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

        If phoneCol <> -1 OrElse firstNameCol <> -1 OrElse lastNameCol <> -1 OrElse companyCol <> -1 OrElse customTextCol <> -1 Then
            startRowIndex = 1
        End If

        If phoneCol = -1 Then phoneCol = 0
        If firstNameCol = -1 AndAlso colCount > 1 Then firstNameCol = 1
        If lastNameCol = -1 AndAlso colCount > 2 Then lastNameCol = 2
        If companyCol = -1 AndAlso colCount > 3 Then companyCol = 3
        If customTextCol = -1 AndAlso colCount > 4 Then customTextCol = 4

        For r As Integer = startRowIndex To parsedRows.Count - 1
            Dim row = parsedRows(r)
            Dim rawPhone = If(phoneCol >= 0 AndAlso phoneCol < row.Count, row(phoneCol), "")
            Dim rawFirst = If(firstNameCol >= 0 AndAlso firstNameCol < row.Count, row(firstNameCol), "")
            Dim rawLast = If(lastNameCol >= 0 AndAlso lastNameCol < row.Count, row(lastNameCol), "")
            Dim rawComp = If(companyCol >= 0 AndAlso companyCol < row.Count, row(companyCol), "")
            Dim rawText = If(customTextCol >= 0 AndAlso customTextCol < row.Count, row(customTextCol), "")

            Dim cleanPhone = CleanPhoneNumber(rawPhone)
            If Not String.IsNullOrWhiteSpace(cleanPhone) OrElse Not String.IsNullOrWhiteSpace(rawFirst) Then
                items.Add(New BulkContactItem With {
                    .Phone = cleanPhone,
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

    Private Shared Function DetectCsvDelimiter(lines As String()) As Char
        Dim sample = lines.Take(5).ToList()
        Dim commaCount = 0
        Dim semiCount = 0
        Dim tabCount = 0

        For Each l In sample
            commaCount += l.Count(Function(c) c = ","c)
            semiCount += l.Count(Function(c) c = ";"c)
            tabCount += l.Count(Function(c) c = vbTab)
        Next

        If semiCount > commaCount AndAlso semiCount > tabCount Then Return ";"c
        If tabCount > commaCount AndAlso tabCount > semiCount Then Return vbTab
        Return ","c
    End Function

    Private Shared Function ParseCsvLine(line As String, delimiter As Char) As List(Of String)
        Dim fields As New List(Of String)()
        Dim sb As New StringBuilder()
        Dim inQuotes = False
        Dim i = 0

        While i < line.Length
            Dim c = line(i)
            If c = """"c Then
                If inQuotes AndAlso i + 1 < line.Length AndAlso line(i + 1) = """"c Then
                    sb.Append(""""c)
                    i += 1
                Else
                    inQuotes = Not inQuotes
                End If
            ElseIf c = delimiter AndAlso Not inQuotes Then
                fields.Add(sb.ToString().Trim())
                sb.Clear()
            Else
                sb.Append(c)
            End If
            i += 1
        End While

        fields.Add(sb.ToString().Trim())
        Return fields
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
