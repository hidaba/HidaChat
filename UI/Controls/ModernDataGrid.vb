Option Strict On
Option Explicit On

Imports System.Windows
Imports System.Windows.Controls

Namespace UI.Controls

    ''' <summary>
    ''' Fluent Design DataGrid with row/column virtualization, clean horizontal gridlines, and sortable columns enabled by default.
    ''' </summary>
    Public Class ModernDataGrid
        Inherits DataGrid

        Public Sub New()
            MyBase.New()
            AutoGenerateColumns = False
            CanUserAddRows = False
            CanUserDeleteRows = False
            CanUserResizeRows = False
            CanUserSortColumns = True
            IsReadOnly = True
            HeadersVisibility = DataGridHeadersVisibility.Column
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
            SelectionMode = DataGridSelectionMode.Single
            SelectionUnit = DataGridSelectionUnit.FullRow
            EnableRowVirtualization = True
            EnableColumnVirtualization = True
            VirtualizingPanel.SetIsVirtualizing(Me, True)
            VirtualizingPanel.SetVirtualizationMode(Me, VirtualizationMode.Recycling)
            RowHeight = 38
            ColumnHeaderHeight = 38
            BorderThickness = New Thickness(1)
        End Sub
    End Class

End Namespace
