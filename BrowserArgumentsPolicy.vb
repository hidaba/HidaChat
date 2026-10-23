Option Explicit On
Option Strict On

Imports System
Imports System.Collections.Generic

''' <summary>
''' Politiche e generazione deterministica degli argomenti da riga di comando per Chromium (TODO #102).
''' Impone tetti massimi alla cache su disco, disabilita il download di componenti e modelli non necessari in background
''' ed esclude feature ridondanti (come il traduttore nativo Chromium, gestito internamente dal motore HidaChat).
''' </summary>
Public Module BrowserArgumentsPolicy
    ''' <summary>Dimensione predefinita della cache HTTP su disco per profilo in MB (50 MB).</summary>
    Public Const DefaultDiskCacheLimitMb As Integer = 50

    ''' <summary>Dimensione fissa massima della cache media audio/video in byte (20 MB = 20,971,520 byte).</summary>
    Public Const MediaCacheSizeBytes As Long = 20971520L

    ''' <summary>
    ''' Costruisce la stringa completa di AdditionalBrowserArguments per CoreWebView2EnvironmentOptions.
    ''' </summary>
    ''' <param name="diskCacheLimitMb">Limite cache HTTP in MB. Valori &gt; 0 impostano --disk-cache-size. 0 indica cache illimitata.</param>
    ''' <param name="enableSpellcheck">Se abilitare il correttore ortografico di Chromium.</param>
    ''' <param name="effectiveLang">Codice lingua attiva per il correttore o UI Chromium.</param>
    ''' <returns>Stringa di argomenti da passare ad AdditionalBrowserArguments.</returns>
    Public Function BuildAdditionalBrowserArguments(diskCacheLimitMb As Integer,
                                                  enableSpellcheck As Boolean,
                                                  effectiveLang As String) As String
        Dim argsList As New List(Of String)()

        ' Features disabilitate alla fonte:
        ' - Translate: traduttore nativo disabilitato (sostituito dal modulo JavaScript/TranslationProvider di HidaChat)
        ' - MediaRouter: scansione/trasmissione verso dispositivi Cast/DLNA non necessaria
        ' - OptimizationGuideModelDownloading: download automatico di modelli pesanti Edge/Chromium in background
        ' - OptimizationHintsFetching: download di hint di ottimizzazione remoti
        Dim disabledFeatures As New List(Of String) From {
            "Translate",
            "MediaRouter",
            "OptimizationGuideModelDownloading",
            "OptimizationHintsFetching"
        }

        If enableSpellcheck AndAlso Not String.IsNullOrWhiteSpace(effectiveLang) Then
            argsList.Add($"--enable-features=Spellcheck --lang={effectiveLang}")
        Else
            disabledFeatures.Add("Spellcheck")
        End If

        argsList.Add($"--disable-features={String.Join(",", disabledFeatures)}")

        ' Hard limit per la cache HTTP su disco (se > 0)
        If diskCacheLimitMb > 0 Then
            Dim diskCacheBytes As Long = CLng(diskCacheLimitMb) * 1024L * 1024L
            argsList.Add($"--disk-cache-size={diskCacheBytes}")
        End If

        ' Hard limit per cache media audio/video (Max 20 MB)
        argsList.Add($"--media-cache-size={MediaCacheSizeBytes}")

        ' Scarta subito la cache vecchia
        argsList.Add("--aggressive-cache-discard")

        ' Non salvare gli shader GPU su disco (previene la crescita di GPUCache, GrShaderCache, ecc.)
        argsList.Add("--disable-gpu-shader-disk-cache")

        ' Blocca il download di estensioni/componenti CRX in background
        argsList.Add("--disable-component-update")

        ' Evita download di telemetria, predizioni e modelli Edge in background
        argsList.Add("--disable-background-networking")

        Return String.Join(" ", argsList).Trim()
    End Function
End Module
