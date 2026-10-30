Option Explicit On
Option Strict On

Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.Threading.Tasks

#If Not NO_WPF Then
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.Wpf
#End If

''' <summary>
''' Politiche e utilità per la compattazione chirurgica a caldo della cache WebView2 (TODO #104).
''' Svuota a runtime tramite ClearBrowsingDataAsync esclusivamente le cache rigenerabili (DiskCache e CacheStorage dei Service Worker),
''' escludendo tassativamente Cookie, LocalStorage, IndexedDB, credenziali e token di sessione di WhatsApp e Telegram.
''' </summary>
Public Module CacheCompactionPolicy

#If Not NO_WPF Then
    ''' <summary>
    ''' Maschera binaria delle categorie di dati sicuri e rigenerabili per CoreWebView2Profile.ClearBrowsingDataAsync:
    ''' - DiskCache (cache HTTP / asset di rete)
    ''' - CacheStorage (cache worker e asset PWA dei Service Worker)
    ''' </summary>
    Public Const SafeRegenerableDataKinds As CoreWebView2BrowsingDataKinds =
        CoreWebView2BrowsingDataKinds.DiskCache Or
        CoreWebView2BrowsingDataKinds.CacheStorage
#End If

    ' Valori numerici corrispondenti all'enum CoreWebView2BrowsingDataKinds
    Public Const KindFileSystems As ULong = 1UL << 0
    Public Const KindIndexedDb As ULong = 1UL << 1
    Public Const KindLocalStorage As ULong = 1UL << 2
    Public Const KindWebSql As ULong = 1UL << 3
    Public Const KindOptimizedCache As ULong = 1UL << 4
    Public Const KindAllProfile As ULong = 1UL << 5
    Public Const KindAllSite As ULong = 1UL << 6
    Public Const KindDiskCache As ULong = 1UL << 7
    Public Const KindDownloadHistory As ULong = 1UL << 8
    Public Const KindGeneralAutofill As ULong = 1UL << 9
    Public Const KindPasswordAutosave As ULong = 1UL << 10
    Public Const KindBrowsingHistory As ULong = 1UL << 11
    Public Const KindSettings As ULong = 1UL << 12
    Public Const KindAllDomStorage As ULong = 1UL << 13
    Public Const KindCookies As ULong = 1UL << 14
    Public Const KindServiceWorkers As ULong = 1UL << 15
    Public Const KindCacheStorage As ULong = 1UL << 16

    ''' <summary>
    ''' Maschera combinata delle sole categorie di dati sicuri e rigenerabili.
    ''' </summary>
    Public Const SafeCompactionKindsMask As ULong = KindDiskCache Or KindCacheStorage

    ''' <summary>
    ''' Maschera combinata dei dati sensibili di sessione che NON devono mai essere cancellati durante la compattazione.
    ''' </summary>
    Public Const DangerousSessionKindsMask As ULong =
        KindCookies Or
        KindIndexedDb Or
        KindLocalStorage Or
        KindAllDomStorage Or
        KindPasswordAutosave Or
        KindGeneralAutofill Or
        KindSettings Or
        KindAllProfile Or
        KindAllSite Or
        KindServiceWorkers ' non de-registrare i worker, solo svuotare la CacheStorage

    ''' <summary>
    ''' Verifica se la combinazione di tipi di dati specificata è rigorosamente sicura per la sessione utente
    ''' (non include alcun flag sensibile ed include solo dati rigenerabili).
    ''' </summary>
    Public Function IsSafeCompactionKind(kindMask As ULong) As Boolean
        If (kindMask And DangerousSessionKindsMask) <> 0UL Then
            Return False
        End If
        ' Deve contenere almeno un flag rigenerabile valido
        Return (kindMask And SafeCompactionKindsMask) <> 0UL
    End Function

#If Not NO_WPF Then
    ''' <summary>
    ''' Verifica se l'insieme di CoreWebView2BrowsingDataKinds include esclusivamente tipi di cache sicuri.
    ''' </summary>
    Public Function IsSafeRegenerableDataKinds(kinds As CoreWebView2BrowsingDataKinds) As Boolean
        Return IsSafeCompactionKind(CULng(kinds))
    End Function

    ''' <summary>
    ''' Esegue la compattazione a caldo della cache del profilo associato all'istanza WebView2 specificata (TODO #104).
    ''' </summary>
    ''' <param name="webView">Controllo WebView2 attivo.</param>
    ''' <returns>True se la pulizia chirurgica è stata avviata e completata con successo, False altrimenti.</returns>
    Public Async Function CompactWebViewProfileAsync(webView As Microsoft.Web.WebView2.Wpf.WebView2) As Task(Of Boolean)
        If webView Is Nothing OrElse webView.CoreWebView2 Is Nothing Then
            Return False
        End If
        Return Await CompactProfileAsync(webView.CoreWebView2.Profile)
    End Function

    ''' <summary>
    ''' Esegue la compattazione a caldo su una specifica istanza CoreWebView2Profile (TODO #104).
    ''' </summary>
    Public Async Function CompactProfileAsync(profile As CoreWebView2Profile) As Task(Of Boolean)
        If profile Is Nothing Then Return False
        Try
            Await profile.ClearBrowsingDataAsync(SafeRegenerableDataKinds)
            Return True
        Catch ex As Exception
            Debug.WriteLine($"CompactProfileAsync error: {ex.Message}")
            Return False
        End Try
    End Function
#End If

    ''' <summary>
    ''' Nomi delle sottocartelle di cache volatili rigenerabili all'interno della cartella del profilo.
    ''' </summary>
    Public ReadOnly RegenerableFolderNames As String() = {
        "Cache",
        "Code Cache",
        "GPUCache",
        "DawnCache",
        "GraphiteDawnCache",
        "GrShaderCache",
        "ShaderCache"
    }

    ''' <summary>
    ''' Restituisce la lista dei percorsi relativi o assoluti delle cartelle di cache rigenerabili per un dato profilo.
    ''' </summary>
    Public Function GetProfileCacheDirectories(profileDirectory As String) As List(Of String)
        Dim list As New List(Of String)()
        If String.IsNullOrWhiteSpace(profileDirectory) OrElse Not Directory.Exists(profileDirectory) Then
            Return list
        End If

        For Each folder In RegenerableFolderNames
            Dim pathCandidate = Path.Combine(profileDirectory, folder)
            If Directory.Exists(pathCandidate) Then
                list.Add(pathCandidate)
            End If
        Next

        ' Sottocartelle CacheStorage dei Service Worker
        Dim swDir = Path.Combine(profileDirectory, "Service Worker")
        If Directory.Exists(swDir) Then
            Dim cacheStorageDir = Path.Combine(swDir, "CacheStorage")
            If Directory.Exists(cacheStorageDir) Then
                list.Add(cacheStorageDir)
            End If
            Dim scriptCacheDir = Path.Combine(swDir, "ScriptCache")
            If Directory.Exists(scriptCacheDir) Then
                list.Add(scriptCacheDir)
            End If
        End If

        Return list
    End Function

    ''' <summary>
    ''' Calcola lo spazio su disco occupato complessivamente dalle cartelle di cache rigenerabili di un profilo.
    ''' </summary>
    Public Function CalculateTransientCacheSizeBytes(profileDirectory As String) As Long
        Dim total As Long = 0
        Dim dirs = GetProfileCacheDirectories(profileDirectory)
        For Each d In dirs
            Try
                Dim di As New DirectoryInfo(d)
                For Each fi In di.EnumerateFiles("*", SearchOption.AllDirectories)
                    total += fi.Length
                Next
            Catch
                ' Ignora file temporaneamente bloccati
            End Try
        Next
        Return total
    End Function

    ''' <summary>
    ''' Formatta una dimensione in byte in stringa leggibile (B, KB, MB, GB).
    ''' </summary>
    Public Function FormatByteSize(bytes As Long) As String
        Dim inv = Globalization.CultureInfo.InvariantCulture
        If bytes < 1024L Then
            Return $"{bytes} B"
        ElseIf bytes < 1024L * 1024L Then
            Return $"{(bytes / 1024.0).ToString("0.0", inv)} KB"
        ElseIf bytes < 1024L * 1024L * 1024L Then
            Return $"{(bytes / (1024.0 * 1024.0)).ToString("0.0", inv)} MB"
        Else
            Return $"{(bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.00", inv)} GB"
        End If
    End Function

End Module
