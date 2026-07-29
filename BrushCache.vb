Imports System.Collections.Concurrent
Imports System.Windows.Media

''' <summary>
''' Cache thread-safe e ad alte prestazioni per le istanze di SolidColorBrush utilizzate nell'interfaccia utente.
''' Evita la continua allocazione ed istanziazione di pennelli WPF durante i cambi di tema o la rielaborazione grafica.
''' Tutti i pennelli inseriti in cache vengono congelati (.Freeze()) per garantire l'immutabilità e la sicurezza multi-thread.
''' </summary>
Public Module BrushCache
    Private ReadOnly _cache As New ConcurrentDictionary(Of String, SolidColorBrush)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>
    ''' Restituisce un'istanza condivisa e congelata (Frozen) di SolidColorBrush corrispondente al codice colore specificato.
    ''' </summary>
    ''' <param name="hexColor">Il codice colore esadecimale (es. "#111b21" o "#ffffff").</param>
    ''' <returns>L'oggetto SolidColorBrush in cache.</returns>
    Public Function GetBrush(hexColor As String) As SolidColorBrush
        If String.IsNullOrWhiteSpace(hexColor) Then Return Brushes.Transparent

        Return _cache.GetOrAdd(hexColor, Function(colorStr)
            Dim colorObj = CType(ColorConverter.ConvertFromString(colorStr), Color)
            Dim brush As New SolidColorBrush(colorObj)
            brush.Freeze()
            Return brush
        End Function)
    End Function
End Module
