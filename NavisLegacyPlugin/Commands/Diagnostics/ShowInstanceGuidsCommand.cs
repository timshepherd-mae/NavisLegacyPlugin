using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;

namespace NavisLegacyPlugin.Commands.Diagnostics
{
    [Plugin(
        "ShowInstanceGuids",
        "MAE",
        DisplayName = "Show Instance GUIDs",
        ToolTip = "Display InstanceGuid values for the current Navisworks selection")]
    public sealed class ShowInstanceGuidsCommand : AddInPlugin
    {
        private static InstanceGuidDiagnosticForm _openForm;

        public override int Execute(params string[] parameters)
        {
            Document document =
                Autodesk.Navisworks.Api.Application.ActiveDocument;

            if (document == null)
            {
                MessageBox.Show(
                    "There is no active Navisworks document.",
                    "Instance GUID Diagnostics",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return 0;
            }

            ModelItemCollection selectedItems =
                document.CurrentSelection.SelectedItems;

            if (selectedItems == null ||
                selectedItems.Count == 0)
            {
                MessageBox.Show(
                    "Select one or more model items first.",
                    "Instance GUID Diagnostics",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return 0;
            }

            List<DiagnosticRow> rows =
                selectedItems
                    .Cast<ModelItem>()
                    .Where(item => item != null)
                    .Select(
                        (item, index) =>
                            CreateRow(
                                item,
                                index + 1))
                    .ToList();

            if (_openForm != null &&
                !_openForm.IsDisposed)
            {
                _openForm.Activate();
                _openForm.BringToFront();

                return 0;
            }

            _openForm =
                new InstanceGuidDiagnosticForm(rows);

            _openForm.FormClosed +=
                (sender, args) => _openForm = null;

            IntPtr ownerHandle =
                Autodesk.Navisworks.Api.Application
                    .Gui
                    .MainWindow
                    .Handle;

            _openForm.Show(
                new NativeWindowOwner(ownerHandle));

            _openForm.Activate();

            return 0;
        }

        private static DiagnosticRow CreateRow(
            ModelItem item,
            int rowNumber)
        {
            return new DiagnosticRow
            {
                RowNumber = rowNumber,
                ModelItem = item,
                DisplayName =
                    item.DisplayName ?? string.Empty,
                InstanceGuidValue = item.InstanceGuid,
                InstanceGuid =
                    item.InstanceGuid.ToString("D"),
                ItemGuid = ReadProperty(
                    item,
                    "Item",
                    "GUID"),
                IfcGuid = ReadPropertyByName(
                    item,
                    "IfcGuid"),
                SourceFile = ReadSourceFile(item),
                HasChildren =
                    HasChildren(item)
                        ? "Yes"
                        : "No"
            };
        }

        private static string ReadPropertyByName(
            ModelItem item,
            string propertyName)
        {
            if (item == null ||
                item.PropertyCategories == null ||
                string.IsNullOrWhiteSpace(propertyName))
            {
                return string.Empty;
            }

            foreach (
                PropertyCategory category
                in item.PropertyCategories)
            {
                if (category == null ||
                    category.Properties == null)
                {
                    continue;
                }

                foreach (
                    DataProperty property
                    in category.Properties)
                {
                    if (property == null ||
                        !string.Equals(
                            property.DisplayName,
                            propertyName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        return property.Value == null
                            ? string.Empty
                            : property.Value.ToDisplayString();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
            }

            return string.Empty;
        }

        private static string ReadSourceFile(
            ModelItem item)
        {
            string value = ReadProperty(
                item,
                "Item",
                "Source File Name");

            return !string.IsNullOrWhiteSpace(value)
                ? value
                : ReadProperty(
                    item,
                    "Item",
                    "Source File");
        }

        private static string ReadProperty(
            ModelItem item,
            string categoryName,
            string propertyName)
        {
            if (item == null ||
                item.PropertyCategories == null)
            {
                return string.Empty;
            }

            foreach (
                PropertyCategory category
                in item.PropertyCategories)
            {
                if (category == null ||
                    !string.Equals(
                        category.DisplayName,
                        categoryName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (
                    DataProperty property
                    in category.Properties)
                {
                    if (property == null ||
                        !string.Equals(
                            property.DisplayName,
                            propertyName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    try
                    {
                        return property.Value == null
                            ? string.Empty
                            : property.Value.ToDisplayString();
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
            }

            return string.Empty;
        }

        private static bool HasChildren(
            ModelItem item)
        {
            if (item == null)
            {
                return false;
            }

            foreach (ModelItem child in item.Children)
            {
                return true;
            }

            return false;
        }

        private sealed class NativeWindowOwner
            : IWin32Window
        {
            public NativeWindowOwner(
                IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle
            {
                get;
                private set;
            }
        }

        private sealed class DiagnosticRow
        {
            public int RowNumber
            {
                get;
                set;
            }

            public ModelItem ModelItem
            {
                get;
                set;
            }

            public string DisplayName
            {
                get;
                set;
            }

            public Guid InstanceGuidValue
            {
                get;
                set;
            }

            public string InstanceGuid
            {
                get;
                set;
            }

            public string DuplicateInstanceGuid
            {
                get;
                set;
            }

            public int InstanceGuidCount
            {
                get;
                set;
            }

            public string DuplicateIfcGuid
            {
                get;
                set;
            }

            public int IfcGuidCount
            {
                get;
                set;
            }

            public string ItemGuid
            {
                get;
                set;
            }

            public string IfcGuid
            {
                get;
                set;
            }

            public string SourceFile
            {
                get;
                set;
            }

            public string HasChildren
            {
                get;
                set;
            }
        }

        private sealed class InstanceGuidDiagnosticForm
            : Form
        {
            private static readonly System.Drawing.Color
                SelectedSortHeaderColour =
                    System.Drawing.Color.FromArgb(
                        224,
                        235,
                        247);

            private readonly List<DiagnosticRow> _rows;
            private readonly List<DiagnosticRow> _displayRows;
            private readonly List<SortCriterion> _sortCriteria;

            private readonly DataGridView _grid;
            private readonly Label _summary;
            private readonly Button _selectRowsButton;
            private readonly CheckBox _caseSensitiveMatchingCheckBox;
            private readonly CheckBox _caseSensitiveSortingCheckBox;

            public InstanceGuidDiagnosticForm(
                List<DiagnosticRow> rows)
            {
                _rows =
                    rows ?? new List<DiagnosticRow>();

                _displayRows =
                    new List<DiagnosticRow>(_rows);

                _sortCriteria =
                    new List<SortCriterion>();

                Text = "Instance GUID Diagnostics";
                Width = 1450;
                Height = 650;
                StartPosition =
                    FormStartPosition.CenterScreen;
                MinimizeBox = false;

                _summary = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 42,
                    Padding = new Padding(8),
                    Text = string.Empty
                };

                _grid = new DataGridView
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    AutoGenerateColumns = false,
                    SelectionMode =
                        DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = true,
                    ClipboardCopyMode =
                        DataGridViewClipboardCopyMode
                            .EnableAlwaysIncludeHeaderText
                };

                _grid.ColumnHeaderMouseClick +=
                    Grid_ColumnHeaderMouseClick;

                _grid.SelectionChanged +=
                    Grid_SelectionChanged;

                AddColumns();

                var copyAllButton = new Button
                {
                    Text = "Copy All",
                    Width = 100,
                    Height = 30
                };

                copyAllButton.Click +=
                    CopyAllButton_Click;

                _selectRowsButton = new Button
                {
                    Text =
                        "Selected Rows to Current Selection",
                    Width = 230,
                    Height = 30,
                    Enabled = false
                };

                _selectRowsButton.Click +=
                    SelectRowsButton_Click;

                _caseSensitiveMatchingCheckBox = new CheckBox
                {
                    Text = "matching",
                    AutoSize = true,
                    Checked = false,
                    Margin = new Padding(4, 7, 10, 0)
                };

                _caseSensitiveMatchingCheckBox.CheckedChanged +=
                    CaseSensitiveMatchingCheckBox_CheckedChanged;

                _caseSensitiveSortingCheckBox = new CheckBox
                {
                    Text = "sorting",
                    AutoSize = true,
                    Checked = false,
                    Margin = new Padding(4, 7, 10, 0)
                };

                _caseSensitiveSortingCheckBox.CheckedChanged +=
                    CaseSensitiveSortingCheckBox_CheckedChanged;

                RecalculateDuplicateMatches();
                RebuildGridRows();

                var closeButton = new Button
                {
                    Text = "Close",
                    Width = 100,
                    Height = 30
                };

                closeButton.Click +=
                    (sender, args) => Close();

                var caseSensitiveLabel = new Label
                {
                    Text = "Case Sensitive?",
                    AutoSize = true,
                    Margin = new Padding(0, 8, 2, 0)
                };

                var optionsPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false
                };

                optionsPanel.Controls.Add(caseSensitiveLabel);
                optionsPanel.Controls.Add(_caseSensitiveMatchingCheckBox);
                optionsPanel.Controls.Add(_caseSensitiveSortingCheckBox);

                var actionPanel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    FlowDirection = FlowDirection.RightToLeft,
                    WrapContents = false
                };

                actionPanel.Controls.Add(closeButton);
                actionPanel.Controls.Add(copyAllButton);
                actionPanel.Controls.Add(_selectRowsButton);

                var bottomPanel = new TableLayoutPanel
                {
                    Dock = DockStyle.Bottom,
                    Height = 48,
                    ColumnCount = 2,
                    Padding = new Padding(8)
                };

                bottomPanel.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 40F));
                bottomPanel.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 60F));
                bottomPanel.Controls.Add(optionsPanel, 0, 0);
                bottomPanel.Controls.Add(actionPanel, 1, 0);

                Controls.Add(_grid);
                Controls.Add(_summary);
                Controls.Add(bottomPanel);
            }

            private void AddColumns()
            {
                AddColumn(
                    "Row",
                    "Row",
                    55);

                AddColumn(
                    "Duplicate",
                    "Same InstanceGuid?",
                    115);

                AddColumn(
                    "DuplicateIfcGuid",
                    "Same IfcGuid?",
                    100);

                AddColumn(
                    "InstanceGuid",
                    "InstanceGuid",
                    260);

                AddColumn(
                    "DisplayName",
                    "Display Name",
                    200);

                AddColumn(
                    "ItemGuid",
                    "Item.GUID",
                    260);

                AddColumn(
                    "IfcGuid",
                    "IfcGuid",
                    260);

                AddColumn(
                    "SourceFile",
                    "Item.Source File",
                    220);

                AddColumn(
                    "HasChildren",
                    "Children?",
                    75);
            }

            private void AddColumn(
                string name,
                string header,
                int width)
            {
                _grid.Columns.Add(
                    new DataGridViewTextBoxColumn
                    {
                        Name = name,
                        HeaderText = header,
                        Width = width,
                        SortMode =
                            DataGridViewColumnSortMode
                                .Programmatic
                    });
            }

            private bool CaseSensitiveMatching
            {
                get
                {
                    return _caseSensitiveMatchingCheckBox.Checked;
                }
            }

            private bool CaseSensitiveSorting
            {
                get
                {
                    return _caseSensitiveSortingCheckBox.Checked;
                }
            }

            private void CaseSensitiveMatchingCheckBox_CheckedChanged(
                object sender,
                EventArgs e)
            {
                RecalculateDuplicateMatches();
                ApplySort();
                UpdateColumnHeaders();
            }

            private void CaseSensitiveSortingCheckBox_CheckedChanged(
                object sender,
                EventArgs e)
            {
                ApplySort();
                UpdateColumnHeaders();
            }

            private void RecalculateDuplicateMatches()
            {
                Dictionary<Guid, int> instanceGuidCounts =
                    _rows
                        .GroupBy(row => row.InstanceGuidValue)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Count());

                StringComparer ifcGuidComparer =
                    CaseSensitiveMatching
                        ? StringComparer.Ordinal
                        : StringComparer.OrdinalIgnoreCase;

                Dictionary<string, int> ifcGuidCounts =
                    _rows
                        .Where(row => !string.IsNullOrWhiteSpace(row.IfcGuid))
                        .GroupBy(row => row.IfcGuid.Trim(), ifcGuidComparer)
                        .ToDictionary(
                            group => group.Key,
                            group => group.Count(),
                            ifcGuidComparer);

                foreach (DiagnosticRow row in _rows)
                {
                    int count;
                    row.InstanceGuidCount =
                        instanceGuidCounts.TryGetValue(row.InstanceGuidValue, out count)
                            ? count
                            : 0;
                    row.DuplicateInstanceGuid =
                        row.InstanceGuidCount > 1 ? "YES" : string.Empty;

                    row.IfcGuidCount = 0;
                    row.DuplicateIfcGuid = string.Empty;
                    if (!string.IsNullOrWhiteSpace(row.IfcGuid) &&
                        ifcGuidCounts.TryGetValue(row.IfcGuid.Trim(), out count))
                    {
                        row.IfcGuidCount = count;
                        row.DuplicateIfcGuid = count > 1 ? "YES" : string.Empty;
                    }
                }

                _summary.Text = BuildSummary(_rows, CaseSensitiveMatching);
            }

            private void Grid_SelectionChanged(
                object sender,
                EventArgs e)
            {
                _selectRowsButton.Enabled =
                    _grid.SelectedRows.Count > 0;
            }

            private void SelectRowsButton_Click(
                object sender,
                EventArgs e)
            {
                try
                {
                    Document document =
                        Autodesk.Navisworks.Api.Application
                            .ActiveDocument;

                    if (document == null)
                    {
                        MessageBox.Show(
                            "There is no active Navisworks document.",
                            "Instance GUID Diagnostics",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        return;
                    }

                    List<DiagnosticRow> selectedRows =
                        _grid
                            .SelectedRows
                            .Cast<DataGridViewRow>()
                            .OrderBy(row => row.Index)
                            .Select(
                                row =>
                                    row.Tag as DiagnosticRow)
                            .Where(
                                row =>
                                    row != null &&
                                    row.ModelItem != null)
                            .ToList();

                    if (selectedRows.Count == 0)
                    {
                        MessageBox.Show(
                            "Select one or more table rows first.",
                            "Instance GUID Diagnostics",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);

                        return;
                    }

                    var modelItems =
                        new ModelItemCollection();

                    foreach (
                        DiagnosticRow row
                        in selectedRows)
                    {
                        modelItems.Add(row.ModelItem);
                    }

                    document.CurrentSelection.CopyFrom(
                        modelItems);

                    _summary.Text =
                        BuildSummary(_rows, CaseSensitiveMatching) +
                        "    |    Current selection updated: " +
                        modelItems.Count;
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        "The Navisworks current selection " +
                        "could not be updated.\r\n\r\n" +
                        exception.Message,
                        "Instance GUID Diagnostics",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            private void Grid_ColumnHeaderMouseClick(
                object sender,
                DataGridViewCellMouseEventArgs e)
            {
                if (e.ColumnIndex < 0 ||
                    e.ColumnIndex >= _grid.Columns.Count)
                {
                    return;
                }

                DataGridViewColumn column =
                    _grid.Columns[e.ColumnIndex];

                bool controlPressed =
                    (Control.ModifierKeys & Keys.Control) ==
                    Keys.Control;

                if (controlPressed)
                {
                    ToggleGroupedSortColumn(
                        column.Name);
                }
                else
                {
                    ApplySingleColumnSort(
                        column.Name);
                }

                ApplySort();
                UpdateColumnHeaders();
            }

            private void ApplySingleColumnSort(
                string columnName)
            {
                SortDirection direction =
                    SortDirection.Ascending;

                if (_sortCriteria.Count == 1 &&
                    string.Equals(
                        _sortCriteria[0].ColumnName,
                        columnName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    direction =
                        _sortCriteria[0].Direction ==
                        SortDirection.Ascending
                            ? SortDirection.Descending
                            : SortDirection.Ascending;
                }

                _sortCriteria.Clear();

                _sortCriteria.Add(
                    new SortCriterion(
                        columnName,
                        direction));
            }

            private void ToggleGroupedSortColumn(
                string columnName)
            {
                SortCriterion existing =
                    _sortCriteria.FirstOrDefault(
                        criterion =>
                            string.Equals(
                                criterion.ColumnName,
                                columnName,
                                StringComparison
                                    .OrdinalIgnoreCase));

                if (existing != null)
                {
                    _sortCriteria.Remove(existing);
                    return;
                }

                _sortCriteria.Add(
                    new SortCriterion(
                        columnName,
                        SortDirection.Ascending));
            }

            private void ApplySort()
            {
                _displayRows.Clear();

                if (_sortCriteria.Count == 0)
                {
                    _displayRows.AddRange(
                        _rows.OrderBy(
                            row => row.RowNumber));
                }
                else
                {
                    List<DiagnosticRow> sortedRows =
                        new List<DiagnosticRow>(_rows);

                    sortedRows.Sort(
                        new DiagnosticRowComparer(
                            _sortCriteria,
                            CaseSensitiveSorting));

                    _displayRows.AddRange(
                        sortedRows);
                }

                RebuildGridRows();
            }

            private static Func<DiagnosticRow, object>
                GetSortSelector(
                    string columnName)
            {
                switch (columnName)
                {
                    case "Row":
                        return row => row.RowNumber;

                    case "Duplicate":
                        return row =>
                            row.InstanceGuidCount;

                    case "DuplicateIfcGuid":
                        return row =>
                            row.IfcGuidCount;

                    case "InstanceGuid":
                        return row =>
                            row.InstanceGuid;

                    case "DisplayName":
                        return row =>
                            row.DisplayName;

                    case "ItemGuid":
                        return row =>
                            row.ItemGuid;

                    case "IfcGuid":
                        return row =>
                            row.IfcGuid;

                    case "SourceFile":
                        return row =>
                            row.SourceFile;

                    case "HasChildren":
                        return row =>
                            string.Equals(
                                row.HasChildren,
                                "Yes",
                                StringComparison
                                    .OrdinalIgnoreCase)
                                ? 1
                                : 0;

                    default:
                        return row =>
                            row.RowNumber;
                }
            }

            private void RebuildGridRows()
            {
                _grid.SuspendLayout();

                try
                {
                    _grid.Rows.Clear();

                    foreach (
                        DiagnosticRow row
                        in _displayRows)
                    {
                        int index =
                            _grid.Rows.Add(
                                row.RowNumber,
                                row.DuplicateInstanceGuid,
                                row.DuplicateIfcGuid,
                                row.InstanceGuid,
                                row.DisplayName,
                                row.ItemGuid,
                                row.IfcGuid,
                                row.SourceFile,
                                row.HasChildren);

                        _grid.Rows[index].Tag = row;

                        if (row.InstanceGuidCount > 1)
                        {
                            _grid.Rows[index]
                                .DefaultCellStyle
                                .BackColor =
                                    System.Drawing.Color
                                        .LightYellow;
                        }
                    }
                }
                finally
                {
                    _grid.ResumeLayout();

                    _selectRowsButton.Enabled =
                        _grid.SelectedRows.Count > 0;
                }
            }

            private void UpdateColumnHeaders()
            {
                foreach (
                    DataGridViewColumn column
                    in _grid.Columns)
                {
                    column.HeaderCell.SortGlyphDirection =
                        SortOrder.None;

                    column.HeaderCell.Style.BackColor =
                        _grid
                            .ColumnHeadersDefaultCellStyle
                            .BackColor;

                    column.HeaderCell.Style.ForeColor =
                        _grid
                            .ColumnHeadersDefaultCellStyle
                            .ForeColor;

                    column.HeaderText =
                        GetBaseHeaderText(
                            column.Name);
                }

                for (
                    int index = 0;
                    index < _sortCriteria.Count;
                    index++)
                {
                    SortCriterion criterion =
                        _sortCriteria[index];

                    DataGridViewColumn column =
                        _grid.Columns[
                            criterion.ColumnName];

                    if (column == null)
                    {
                        continue;
                    }

                    column.HeaderCell.Style.BackColor =
                        SelectedSortHeaderColour;

                    column.HeaderCell.SortGlyphDirection =
                        criterion.Direction ==
                        SortDirection.Ascending
                            ? SortOrder.Ascending
                            : SortOrder.Descending;

                    column.HeaderText =
                        GetBaseHeaderText(
                            column.Name) +
                        " [" +
                        (index + 1) +
                        "]";
                }
            }

            private static string GetBaseHeaderText(
                string columnName)
            {
                switch (columnName)
                {
                    case "Row":
                        return "Row";

                    case "Duplicate":
                        return "Same InstanceGuid?";

                    case "DuplicateIfcGuid":
                        return "Same IfcGuid?";

                    case "InstanceGuid":
                        return "InstanceGuid";

                    case "DisplayName":
                        return "Display Name";

                    case "ItemGuid":
                        return "Item.GUID";

                    case "IfcGuid":
                        return "IfcGuid";

                    case "SourceFile":
                        return "Item.Source File";

                    case "HasChildren":
                        return "Children?";

                    default:
                        return columnName;
                }
            }

            private void CopyAllButton_Click(
                object sender,
                EventArgs e)
            {
                try
                {
                    Clipboard.SetText(
                        BuildTabSeparatedText(
                            _displayRows));

                    MessageBox.Show(
                        "Table copied to the clipboard.\r\n\r\n" +
                        "You can paste it directly into Excel, " +
                        "Notepad or another application.",
                        "Instance GUID Diagnostics",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception exception)
                {
                    MessageBox.Show(
                        "The table could not be copied.\r\n\r\n" +
                        exception.Message,
                        "Instance GUID Diagnostics",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            private static string BuildSummary(
                List<DiagnosticRow> rows,
                bool caseSensitiveMatching)
            {
                int duplicateInstanceGuidGroups =
                    rows
                        .GroupBy(
                            row =>
                                row.InstanceGuidValue)
                        .Count(
                            group =>
                                group.Count() > 1);

                int itemsInDuplicateInstanceGuidGroups =
                    rows.Count(
                        row =>
                            row.InstanceGuidCount > 1);

                List<IGrouping<string, DiagnosticRow>>
                    duplicateIfcGuidGroups =
                        rows
                            .Where(
                                row =>
                                    !string.IsNullOrWhiteSpace(
                                        row.IfcGuid))
                            .GroupBy(
                                row =>
                                    row.IfcGuid.Trim(),
                                caseSensitiveMatching
                                    ? StringComparer.Ordinal
                                    : StringComparer.OrdinalIgnoreCase)
                            .Where(
                                group =>
                                    group.Count() > 1)
                            .ToList();

                int duplicateIfcGuidGroupCount =
                    duplicateIfcGuidGroups.Count;

                int itemsInDuplicateIfcGuidGroups =
                    duplicateIfcGuidGroups.Sum(
                        group =>
                            group.Count());

                return
                    "Selected items: " +
                    rows.Count +
                    "    |    Duplicate InstanceGuid: " +
                    itemsInDuplicateInstanceGuidGroups +
                    " items in " +
                    duplicateInstanceGuidGroups +
                    " groups" +
                    "    |    Duplicate IfcGuid: " +
                    itemsInDuplicateIfcGuidGroups +
                    " items in " +
                    duplicateIfcGuidGroupCount +
                    " groups";
            }

            private static string BuildTabSeparatedText(
                IEnumerable<DiagnosticRow> rows)
            {
                var builder = new StringBuilder();

                builder.AppendLine(
                    "Row\tSame InstanceGuid?\tSame IfcGuid?\t" +
                    "InstanceGuid\tDisplay Name\tItem.GUID\tIfcGuid\t" +
                    "Item.Source File\tChildren?");

                foreach (DiagnosticRow row in rows)
                {
                    builder.Append(
                        row.RowNumber);
                    builder.Append('\t');

                    builder.Append(
                        EscapeTabValue(
                            row.DuplicateInstanceGuid));
                    builder.Append('\t');

                    builder.Append(
                        EscapeTabValue(
                            row.DuplicateIfcGuid));
                    builder.Append('\t');

                    builder.Append(
                        EscapeTabValue(
                            row.InstanceGuid));
                    builder.Append('\t');

                    builder.Append(
                        EscapeTabValue(
                            row.DisplayName));
                    builder.Append('\t');

                    builder.Append(
                        EscapeTabValue(
                            row.ItemGuid));
                    builder.Append('\t');

                    builder.Append(
                        EscapeTabValue(
                            row.IfcGuid));
                    builder.Append('\t');

                    builder.Append(
                        EscapeTabValue(
                            row.SourceFile));
                    builder.Append('\t');

                    builder.Append(
                        EscapeTabValue(
                            row.HasChildren));

                    builder.AppendLine();
                }

                return builder.ToString();
            }

            private static string EscapeTabValue(
                string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    return string.Empty;
                }

                return value
                    .Replace("\t", " ")
                    .Replace("\r", " ")
                    .Replace("\n", " ");
            }

            private enum SortDirection
            {
                Ascending,
                Descending
            }

            private sealed class SortCriterion
            {
                public SortCriterion(
                    string columnName,
                    SortDirection direction)
                {
                    ColumnName = columnName;
                    Direction = direction;
                }

                public string ColumnName
                {
                    get;
                    private set;
                }

                public SortDirection Direction
                {
                    get;
                    private set;
                }
            }

            private sealed class DiagnosticRowComparer
                : IComparer<DiagnosticRow>
            {
                private readonly List<SortCriterion>
                    _criteria;
                private readonly DiagnosticValueComparer
                    _valueComparer;

                public DiagnosticRowComparer(
                    IEnumerable<SortCriterion> criteria,
                    bool caseSensitiveSorting)
                {
                    _criteria =
                        criteria == null
                            ? new List<SortCriterion>()
                            : criteria.ToList();
                    _valueComparer =
                        new DiagnosticValueComparer(caseSensitiveSorting);
                }

                public int Compare(
                    DiagnosticRow first,
                    DiagnosticRow second)
                {
                    if (ReferenceEquals(
                        first,
                        second))
                    {
                        return 0;
                    }

                    if (first == null)
                    {
                        return -1;
                    }

                    if (second == null)
                    {
                        return 1;
                    }

                    foreach (
                        SortCriterion criterion
                        in _criteria)
                    {
                        Func<DiagnosticRow, object>
                            selector =
                                GetSortSelector(
                                    criterion.ColumnName);

                        int result =
                            _valueComparer
                                .Compare(
                                    selector(first),
                                    selector(second));

                        if (result == 0)
                        {
                            continue;
                        }

                        return
                            criterion.Direction ==
                            SortDirection.Ascending
                                ? result
                                : -result;
                    }

                    return first.RowNumber.CompareTo(
                        second.RowNumber);
                }
            }

            private sealed class DiagnosticValueComparer
                : IComparer<object>
            {
                private readonly StringComparer _stringComparer;

                public DiagnosticValueComparer(bool caseSensitive)
                {
                    _stringComparer = caseSensitive
                        ? StringComparer.Ordinal
                        : StringComparer.OrdinalIgnoreCase;
                }

                public int Compare(
                    object x,
                    object y)
                {
                    if (ReferenceEquals(x, y))
                    {
                        return 0;
                    }

                    if (x == null)
                    {
                        return -1;
                    }

                    if (y == null)
                    {
                        return 1;
                    }

                    if (x is int &&
                        y is int)
                    {
                        return ((int)x).CompareTo(
                            (int)y);
                    }

                    string first =
                        Convert.ToString(x) ??
                        string.Empty;

                    string second =
                        Convert.ToString(y) ??
                        string.Empty;

                    return
                        _stringComparer.Compare(
                                first,
                                second);
                }
            }
        }
    }
}
