Imports System.ComponentModel
Imports System.Linq
Imports System.Windows.Media

''' <summary>
''' Rappresenta un singolo contatto importato da file Excel/CSV con i relativi dati anagrafici, 
''' testo personalizzato, anteprima del messaggio e stato dell'invio.
''' </summary>
Public Class BulkContactItem
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private _selected As Boolean = True
    Public Property Selected As Boolean
        Get
            Return _selected
        End Get
        Set(value As Boolean)
            If _selected <> value Then
                _selected = value
                OnPropertyChanged(NameOf(Selected))
            End If
        End Set
    End Property

    Private _phone As String = String.Empty
    Public Property Phone As String
        Get
            Return _phone
        End Get
        Set(value As String)
            If _phone <> value Then
                _phone = value
                OnPropertyChanged(NameOf(Phone))
                OnPropertyChanged(NameOf(CleanPhone))
            End If
        End Set
    End Property

    Private _firstName As String = String.Empty
    Public Property FirstName As String
        Get
            Return _firstName
        End Get
        Set(value As String)
            If _firstName <> value Then
                _firstName = value
                OnPropertyChanged(NameOf(FirstName))
                OnPropertyChanged(NameOf(FullName))
            End If
        End Set
    End Property

    Private _lastName As String = String.Empty
    Public Property LastName As String
        Get
            Return _lastName
        End Get
        Set(value As String)
            If _lastName <> value Then
                _lastName = value
                OnPropertyChanged(NameOf(LastName))
                OnPropertyChanged(NameOf(FullName))
            End If
        End Set
    End Property

    Private _company As String = String.Empty
    Public Property Company As String
        Get
            Return _company
        End Get
        Set(value As String)
            If _company <> value Then
                _company = value
                OnPropertyChanged(NameOf(Company))
            End If
        End Set
    End Property

    Private _customText As String = String.Empty
    Public Property CustomText As String
        Get
            Return _customText
        End Get
        Set(value As String)
            If _customText <> value Then
                _customText = value
                OnPropertyChanged(NameOf(CustomText))
            End If
        End Set
    End Property

    Private _previewMessage As String = String.Empty
    Public Property PreviewMessage As String
        Get
            Return _previewMessage
        End Get
        Set(value As String)
            If _previewMessage <> value Then
                _previewMessage = value
                OnPropertyChanged(NameOf(PreviewMessage))
            End If
        End Set
    End Property

    Private _status As String = "In attesa"
    Public Property Status As String
        Get
            Return _status
        End Get
        Set(value As String)
            If _status <> value Then
                _status = value
                OnPropertyChanged(NameOf(Status))
                OnPropertyChanged(NameOf(StatusBrush))
            End If
        End Set
    End Property

    Private _errorMessage As String = String.Empty
    Public Property ErrorMessage As String
        Get
            Return _errorMessage
        End Get
        Set(value As String)
            If _errorMessage <> value Then
                _errorMessage = value
                OnPropertyChanged(NameOf(ErrorMessage))
            End If
        End Set
    End Property

    Public ReadOnly Property FullName As String
        Get
            Dim name = $"{FirstName} {LastName}".Trim()
            Return If(String.IsNullOrEmpty(name), "-", name)
        End Get
    End Property

    Public ReadOnly Property CleanPhone As String
        Get
            If String.IsNullOrWhiteSpace(Phone) Then Return String.Empty
            Dim trimmed = Phone.Trim()
            Dim hasPlus = trimmed.StartsWith("+")
            Dim digits = New String(trimmed.Where(AddressOf Char.IsDigit).ToArray())
            Return If(hasPlus, "+" & digits, digits)
        End Get
    End Property

    Public ReadOnly Property StatusBrush As Brush
        Get
            Select Case Status.ToLowerInvariant()
                Case "inviato ✔", "inviato", "sent", "sent ✔"
                    Return New SolidColorBrush(Color.FromRgb(0, 168, 132))
                Case "inviando...", "sending...", "in corso"
                    Return New SolidColorBrush(Color.FromRgb(245, 158, 11))
                Case "errore ✖", "errore", "error", "error ✖", "non valido", "numero non valido"
                    Return New SolidColorBrush(Color.FromRgb(234, 67, 53))
                Case "saltato", "skipped"
                    Return New SolidColorBrush(Color.FromRgb(134, 150, 160))
                Case Else
                    Return New SolidColorBrush(Color.FromRgb(134, 150, 160))
            End Select
        End Get
    End Property

    ''' <summary>
    ''' Genera il messaggio finale sostituendo i tag segnaposto ({Nome}, {Cognome}, {Azienda}, {Telefono}, {Testo}).
    ''' Se il template è vuoto o impostato su "{Testo}", restituisce direttamente il testo personalizzato del contatto.
    ''' </summary>
    Public Function GenerateMessage(template As String) As String
        If String.IsNullOrWhiteSpace(template) OrElse template.Trim().Equals("{Testo}", StringComparison.OrdinalIgnoreCase) Then
            Return If(Not String.IsNullOrEmpty(CustomText), CustomText, String.Empty)
        End If

        Dim msg = template
        msg = msg.Replace("{Nome}", FirstName, StringComparison.OrdinalIgnoreCase)
        msg = msg.Replace("{Name}", FirstName, StringComparison.OrdinalIgnoreCase)
        msg = msg.Replace("{Cognome}", LastName, StringComparison.OrdinalIgnoreCase)
        msg = msg.Replace("{Surname}", LastName, StringComparison.OrdinalIgnoreCase)
        msg = msg.Replace("{LastName}", LastName, StringComparison.OrdinalIgnoreCase)
        msg = msg.Replace("{Azienda}", Company, StringComparison.OrdinalIgnoreCase)
        msg = msg.Replace("{Company}", Company, StringComparison.OrdinalIgnoreCase)
        msg = msg.Replace("{Telefono}", Phone, StringComparison.OrdinalIgnoreCase)
        msg = msg.Replace("{Phone}", Phone, StringComparison.OrdinalIgnoreCase)
        msg = msg.Replace("{Testo}", CustomText, StringComparison.OrdinalIgnoreCase)
        msg = msg.Replace("{Text}", CustomText, StringComparison.OrdinalIgnoreCase)
        msg = msg.Replace("{Messaggio}", CustomText, StringComparison.OrdinalIgnoreCase)
        Return msg
    End Function

    Protected Sub OnPropertyChanged(propertyName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub
End Class
