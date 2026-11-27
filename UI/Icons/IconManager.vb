Option Strict On
Option Explicit On

Imports System.Collections.Concurrent
#If Not NO_WPF Then
Imports System.Windows.Media
#End If

Namespace UI.Icons

    ''' <summary>
    ''' Standardized vector icon identifiers (24x24 Fluent/Material-style SVG path geometry).
    ''' </summary>
    Public Enum IconKey
        None = 0
        Home
        Dashboard
        Settings
        Search
        Close
        Check
        Warning
        [Error]
        Info
        Plus
        Trash
        Edit
        Save
        Refresh
        Sun
        Moon
        Monitor
        Palette
        Bell
        User
        Users
        Shield
        Send
        Folder
        Download
        Upload
        ChevronRight
        ChevronDown
        Filter
        Sparkles
    End Enum

    ''' <summary>
    ''' Centralized vector icon registry providing resolution-independent SVG path data and frozen WPF Geometry instances.
    ''' </summary>
    Public Module IconManager

        Private ReadOnly PathRegistry As New Dictionary(Of IconKey, String) From {
            {IconKey.None, String.Empty},
            {IconKey.Home, "M10,20V14H14V20H19V12H22L12,3L2,12H5V20H10Z"},
            {IconKey.Dashboard, "M3,13H11V3H3V13ZM3,21H11V15H3V21ZM13,21H21V11H13V21ZM13,3V9H21V3H13Z"},
            {IconKey.Settings, "M19.14,12.94C19.18,12.64 19.2,12.33 19.2,12C19.2,11.68 19.18,11.36 19.13,11.06L21.16,9.48C21.34,9.34 21.39,9.07 21.28,8.87L19.36,5.55C19.24,5.33 18.99,5.26 18.77,5.33L16.38,6.29C15.88,5.91 15.35,5.59 14.76,5.35L14.4,2.81C14.36,2.57 14.16,2.4 13.92,2.4H10.08C9.84,2.4 9.65,2.57 9.61,2.81L9.25,5.35C8.66,5.59 8.12,5.92 7.63,6.29L5.24,5.33C5.02,5.25 4.77,5.33 4.65,5.55L2.74,8.87C2.62,9.08 2.66,9.34 2.86,9.48L4.89,11.06C4.84,11.36 4.8,11.69 4.8,12C4.8,12.31 4.82,12.64 4.87,12.94L2.84,14.52C2.66,14.66 2.61,14.93 2.72,15.13L4.64,18.45C4.76,18.67 5.01,18.74 5.23,18.67L7.62,17.71C8.12,18.09 8.65,18.41 9.24,18.65L9.6,21.19C9.65,21.43 9.84,21.6 10.08,21.6H13.92C14.16,21.6 14.36,21.43 14.39,21.19L14.75,18.65C15.34,18.41 15.88,18.09 16.37,17.71L18.76,18.67C18.98,18.75 19.23,18.67 19.35,18.45L21.27,15.13C21.39,14.91 21.34,14.66 21.15,14.52L19.14,12.94ZM12,15.6C10.02,15.6 8.4,13.98 8.4,12C8.4,10.02 10.02,8.4 12,8.4C13.98,8.4 15.6,10.02 15.6,12C15.6,13.98 13.98,15.6 12,15.6Z"},
            {IconKey.Search, "M15.5,14H14.71L14.43,13.73C15.41,12.59 16,11.11 16,9.5C16,5.91 13.09,3 9.5,3C5.91,3 3,5.91 3,9.5C3,13.09 5.91,16 9.5,16C11.11,16 12.59,15.41 13.73,14.43L14,14.71V15.5L19,20.49L20.49,19L15.5,14ZM9.5,14C7.01,14 5,11.99 5,9.5C5,7.01 7.01,5 9.5,5C11.99,5 14,7.01 14,9.5C14,11.99 11.99,14 9.5,14Z"},
            {IconKey.Close, "M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z"},
            {IconKey.Check, "M9,16.17L4.83,12L3.41,13.41L9,19L21,7L19.59,5.59L9,16.17Z"},
            {IconKey.Warning, "M1,21H23L12,2L1,21ZM13,18H11V16H13V18ZM13,14H11V10H13V14Z"},
            {IconKey.Error, "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2ZM13,17H11V15H13V17ZM13,13H11V7H13V13Z"},
            {IconKey.Info, "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2ZM13,17H11V11H13V17ZM13,9H11V7H13V9Z"},
            {IconKey.Plus, "M19,13H13V19H11V13H5V11H11V5H13V11H19V13Z"},
            {IconKey.Trash, "M6,19C6,20.1 6.9,21 8,21H16C17.1,21 18,20.1 18,19V7H6V19ZM19,4H15.5L14.5,3H9.5L8.5,4H5V6H19V4Z"},
            {IconKey.Edit, "M3,17.25V21H6.75L17.81,9.94L14.06,6.19L3,17.25ZM20.71,7.04C21.1,6.65 21.1,6.02 20.71,5.63L18.37,3.29C17.98,2.9 17.35,2.9 16.96,3.29L15.13,5.12L18.88,8.87L20.71,7.04Z"},
            {IconKey.Save, "M17,3H5C3.89,3 3,3.9 3,5V19C3,20.1 3.89,21 5,21H19C20.1,21 21,20.1 21,19V7L17,3ZM12,19C10.34,19 9,17.66 9,16C9,14.34 10.34,13 12,13C13.66,13 15,14.34 15,16C15,17.66 13.66,19 12,19ZM15,9H5V5H15V9Z"},
            {IconKey.Refresh, "M17.65,6.35C16.2,4.9 14.21,4 12,4C7.58,4 4.01,7.58 4.01,12C4.01,16.42 7.58,20 12,20C15.73,20 18.84,17.45 19.73,14H17.65C16.83,16.33 14.61,18 12,18C8.69,18 6,15.31 6,12C6,8.69 8.69,6 12,6C13.66,6 15.14,6.69 16.22,7.78L13,11H20V4L17.65,6.35Z"},
            {IconKey.Sun, "M12,7C9.24,7 7,9.24 7,12C7,14.76 9.24,17 12,17C14.76,17 17,14.76 17,12C17,9.24 14.76,7 12,7ZM12,15C10.34,15 9,13.66 9,12C9,10.34 10.34,9 12,9C13.66,9 15,10.34 15,12C15,13.66 13.66,15 12,15ZM11,1H13V4H11V1ZM11,20H13V23H11V20ZM3.51,4.93L4.93,3.51L7.05,5.64L5.64,7.05L3.51,4.93ZM16.95,18.36L18.36,16.95L20.49,19.07L19.07,20.49L16.95,18.36ZM20,11H23V13H20V11ZM1,11H4V13H1V11ZM16.95,5.64L19.07,3.51L20.49,4.93L18.36,7.05L16.95,5.64ZM3.51,19.07L5.64,16.95L7.05,18.36L4.93,20.49L3.51,19.07Z"},
            {IconKey.Moon, "M12,3C7.03,3 3,7.03 3,12C3,16.97 7.03,21 12,21C16.97,21 21,16.97 21,12C21,11.54 20.96,11.08 20.9,10.64C19.92,12.01 18.32,12.9 16.5,12.9C13.52,12.9 11.1,10.48 11.1,7.5C11.1,5.69 11.99,4.08 13.36,3.1C12.92,3.04 12.46,3 12,3Z"},
            {IconKey.Monitor, "M21,2H3C1.9,2 1,2.9 1,4V16C1,17.1 1.9,18 3,18H10V20H8V22H16V20H14V18H21C22.1,18 23,17.1 23,16V4C23,2.9 22.1,2 21,2ZM21,16H3V4H21V16Z"},
            {IconKey.Palette, "M12,3C7.03,3 3,7.03 3,12C3,16.97 7.03,21 12,21C12.83,21 13.5,20.33 13.5,19.5C13.5,19.11 13.35,18.76 13.11,18.49C12.88,18.23 12.73,17.88 12.73,17.5C12.73,16.67 13.4,16 14.23,16H16C18.76,16 21,13.76 21,11C21,6.58 16.97,3 12,3ZM6.5,12C5.67,12 5,11.33 5,10.5C5,9.67 5.67,9 6.5,9C7.33,9 8,9.67 8,10.5C8,11.33 7.33,12 6.5,12ZM9.5,8C8.67,8 8,7.33 8,6.5C8,5.67 8.67,5 9.5,5C10.33,5 11,5.67 11,6.5C11,7.33 10.33,8 9.5,8ZM14.5,8C13.67,8 13,7.33 13,6.5C13,5.67 13.67,5 14.5,5C15.33,5 16,5.67 16,6.5C16,7.33 15.33,8 14.5,8ZM17.5,12C16.67,12 16,11.33 16,10.5C16,9.67 16.67,9 17.5,9C18.33,9 19,9.67 19,10.5C19,11.33 18.33,12 17.5,12Z"},
            {IconKey.Bell, "M12,22C13.1,22 14,21.1 14,20H10C10,21.1 10.9,22 12,22ZM18,16V11C18,7.93 16.37,5.36 13.5,4.68V4C13.5,3.17 12.83,2.5 12,2.5C11.17,2.5 10.5,3.17 10.5,4V4.68C7.64,5.36 6,7.92 6,11V16L4,18V19H20V18L18,16Z"},
            {IconKey.User, "M12,12C14.21,12 16,10.21 16,8C16,5.79 14.21,4 12,4C9.79,4 8,5.79 8,8C8,10.21 9.79,12 12,12ZM12,14C9.33,14 4,15.34 4,18V20H20V18C20,15.34 14.67,14 12,14Z"},
            {IconKey.Users, "M16,11C17.66,11 18.99,9.66 18.99,8C18.99,6.34 17.66,5 16,5C14.34,5 13,6.34 13,8C13,9.66 14.34,11 16,11ZM8,11C9.66,11 10.99,9.66 10.99,8C10.99,6.34 9.66,5 8,5C6.34,5 5,6.34 5,8C5,9.66 6.34,11 8,11ZM8,13C5.67,13 1,14.17 1,16.5V19H15V16.5C15,14.17 10.33,13 8,13ZM16,13C15.71,13 15.38,13.02 15.03,13.05C16.19,13.89 17,15.02 17,16.5V19H23V16.5C23,14.17 18.33,13 16,13Z"},
            {IconKey.Shield, "M12,1L3,5V11C3,16.55 6.84,21.74 12,23C17.16,21.74 21,16.55 21,11V5L12,1ZM10,17L6,13L7.41,11.59L10,14.17L16.59,7.58L18,9L10,17Z"},
            {IconKey.Send, "M2.01,21L23,12L2.01,3L2,10L17,12L2,14L2.01,21Z"},
            {IconKey.Folder, "M10,4H4C2.9,4 2.01,4.9 2.01,6L2,18C2,19.1 2.9,20 4,20H20C21.1,20 22,19.1 22,18V8C22,6.9 21.1,6 20,6H12L10,4Z"},
            {IconKey.Download, "M19,9H15V3H9V9H5L12,16L19,9ZM5,18V20H19V18H5Z"},
            {IconKey.Upload, "M9,16H15V10H19L12,3L5,10H9V16ZM5,18H19V20H5V18Z"},
            {IconKey.ChevronRight, "M10,6L8.59,7.41L13.17,12L8.59,16.59L10,18L16,12L10,6Z"},
            {IconKey.ChevronDown, "M7.41,8.59L12,13.17L16.59,8.59L18,10L12,16L6,10L7.41,8.59Z"},
            {IconKey.Filter, "M10,18H14V16H10V18ZM3,6V8H21V6H3ZM6,13H18V11H6V13Z"},
            {IconKey.Sparkles, "M19,9L20.25,6.25L23,5L20.25,3.75L19,1L17.75,3.75L15,5L17.75,6.25L19,9ZM11.5,9.5L9,4L6.5,9.5L1,12L6.5,14.5L9,20L11.5,14.5L17,12L11.5,9.5ZM19,15L17.75,17.75L15,19L17.75,20.25L19,23L20.25,20.25L23,19L20.25,17.75L19,15Z"}
        }

        ''' <summary>
        ''' Returns the SVG path data string for a given IconKey.
        ''' </summary>
        Public Function GetPathData(key As IconKey) As String
            Dim data As String = Nothing
            If PathRegistry.TryGetValue(key, data) Then
                Return data
            End If
            Return String.Empty
        End Function

        ''' <summary>
        ''' Returns all registered non-empty icon keys for showcase and audit inspection.
        ''' </summary>
        Public Function GetAllIconKeys() As IReadOnlyList(Of IconKey)
            Return PathRegistry.Keys.Where(Function(k) k <> IconKey.None).ToList()
        End Function

#If Not NO_WPF Then
        Private ReadOnly GeometryCache As New ConcurrentDictionary(Of IconKey, Geometry)()

        ''' <summary>
        ''' Returns a cached, frozen WPF Geometry object for high-performance vector rendering.
        ''' </summary>
        Public Function GetGeometry(key As IconKey) As Geometry
            If key = IconKey.None Then Return Geometry.Empty

            Return GeometryCache.GetOrAdd(key, Function(k)
                                                   Dim pathStr = GetPathData(k)
                                                   If String.IsNullOrWhiteSpace(pathStr) Then Return Geometry.Empty
                                                   Dim geom = Geometry.Parse(pathStr)
                                                   If geom.CanFreeze Then geom.Freeze()
                                                   Return geom
                                               End Function)
        End Function
#End If

    End Module

End Namespace
