﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using disfr.Configuration;
using disfr.Doc;
using IColumnDesc = disfr.Writer.IColumnDesc;

namespace disfr.UI
{
    /// <summary>
    /// Interaction logic for TableView.xaml
    /// </summary>
    public partial class TableView : TabItem
    {
        private readonly IConfigService _configService;

        public TableView(IConfigService configService)
        {
            _configService = configService;
            InitializeComponent();
            StandardColumns = CreateStandardColumns();
            DataContextChanged += this_DataContextChanged;

            FilterTimer = new DispatcherTimer(
                TimeSpan.FromSeconds(0.5),
                DispatcherPriority.ContextIdle,
                FilterTimer_Tick,
                Dispatcher)
            {
                IsEnabled = false
            };
            QuickFilter = configService.QuickFilter;
        }

        #region ColumnInUse attached property

        public static readonly DependencyProperty ColumnInUseProperty =
            DependencyProperty.Register("ColumnInUse", typeof(bool), typeof(TableView),
                new PropertyMetadata(false));

        public static void SetColumnInUse(DependencyObject target, bool in_use)
        {
            target.SetValue(ColumnInUseProperty, in_use);
        }

        public static bool GetColumnInUse(DependencyObject target)
        {
            return (bool)target.GetValue(ColumnInUseProperty);
        }

        #endregion

        #region FilterOptions attached property

        public static readonly DependencyProperty FilterOptionsProperty =
            DependencyProperty.Register("FilterOptions", typeof(IEnumerable<FilterOption>), typeof(TableView));

        public static void SetFilterOptions(DependencyObject target, IEnumerable<FilterOption> filter_options)
        {
            target.SetValue(FilterOptionsProperty, filter_options);
        }

        public static IEnumerable<FilterOption> GetFilterOptions(DependencyObject target)
        {
            return (IEnumerable<FilterOption>)target.GetValue(FilterOptionsProperty);
        }

        public class FilterOption
        {
            public const string MARKER = "\u2060\u200B";

            private FilterOption(string text)
            {
                String = text;
                DisplayString = MARKER + text;
            }

            private FilterOption()
            {
                String = "";
                DisplayString = MARKER + MARKER + "(empty)";
            }

            public static FilterOption Get(string text)
            {
                return string.IsNullOrEmpty(text) ? new FilterOption() : new FilterOption(text);
            }

            public readonly String String;

            public readonly string DisplayString;

            public override string ToString() { return DisplayString; }
        }

        #endregion

        #region StandardColumns local array

        private class StandardColumnInfo
        {
            public readonly DataGridColumn Column;
            public readonly Func<IRowData, bool> InUse;

            public StandardColumnInfo(DataGridColumn column, Func<IRowData, bool> in_use)
            {
                Column = column;
                InUse = in_use;
            }
        }

        private StandardColumnInfo[] CreateStandardColumns()
        {
            var columns = new[]
            {
                new StandardColumnInfo(Serial, r => true),
                new StandardColumnInfo(Package, r => true),
                new StandardColumnInfo(Asset, r => true),
                new StandardColumnInfo(Id, r => !string.IsNullOrWhiteSpace(r.Id)),
                new StandardColumnInfo(Source, r => true),
                new StandardColumnInfo(Target, r => true),
                new StandardColumnInfo(Asset2, r => !string.IsNullOrWhiteSpace(r.Asset2)),
                new StandardColumnInfo(Id2, r => !string.IsNullOrWhiteSpace(r.Id2)),
                new StandardColumnInfo(Target2, r => !GlossyString.IsNullOrEmpty(r.Target2)),
                new StandardColumnInfo(Notes, r => !string.IsNullOrWhiteSpace(r.Notes)),
                new StandardColumnInfo(TagList, r => true),
            };
#if DEBUG
            if (columns.Length != dataGrid.Columns.Count)
            {
                throw new ApplicationException("Extra DataGridColumn in XAML.");
            }
#endif
            return columns;
        }

        private readonly StandardColumnInfo[] StandardColumns;

        #endregion

        #region Controller and dynamic column generation

        /// <summary>
        /// A typed alias of <see cref="DataContext"/>.
        /// </summary>
        /// <value>
        /// An <see cref="ITableController"/> to show on the grid.
        /// </value>
        public ITableController Controller
        {
            get { return DataContext as ITableController; }
            set { DataContext = value; }
        }

        private static readonly DataGridLength DataGridLengthStar = new DataGridLength(1.0, DataGridLengthUnitType.Star);

        /// <summary>
        /// Handles DataContextChanged event of this TableView instance.
        /// </summary>
        /// <remarks>
        /// <see cref="TableView"/> has no OnDataContextChanged method to override.
        /// </remarks>
        private void this_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // We don't need this under the current scenario.
            if (e.OldValue is ITableController)
            {
                (e.OldValue as ITableController).AdditionalPropsChanged -= ITableController_AdditionalPropsChanged;
            }

            // Reset the column organization.
            // This should be useless under the current scenario.
            foreach (var dc in dataGrid.Columns.Except(StandardColumns.Select(sc => sc.Column)).ToList())
            {
                dataGrid.Columns.Remove(dc);
            }
            foreach (var dc in dataGrid.Columns)
            {
                dc.SetValue(ColumnInUseProperty, false);
            }

            // Our DataContext should always be an ITable, but null should be allowed (though we don't need.)
            var table = e.NewValue as ITableController;
            if (table == null) return;

            // Subscribe the change notification event.
            table.AdditionalPropsChanged += ITableController_AdditionalPropsChanged;

            // Update DataGrid columns appropriately for the table we are showing.
            UpdateColumns();

            // Set the initial visibilities of standard columns.
            foreach (var colInfo in StandardColumns)
            {
                var col = colInfo.Column;
                col.Visibility = GetColumnInUse(col) ? Visibility.Visible : Visibility.Collapsed;
                
            }

            // A special handling of Asset column visibility.
            // There are many cases that a file contains only one Asset,
            // and Asset column is just redundant in the cases, so hide it in the cases.
            // (but users can still view it if they want.)
            if (Asset.Visibility == Visibility.Visible &&
                Asset2.Visibility != Visibility.Visible &&
                !table.AllRows.Select(r => r.Asset).Distinct().Skip(1).Any())
            {
                Asset.Visibility = Visibility.Collapsed;
            }

            // Same thing as Asset for Package visibility.
            if (Package.Visibility == Visibility.Visible &&
                !table.AllRows.Select(r => r.Package).Distinct().Skip(1).Any())
            {
                Package.Visibility = Visibility.Collapsed;
            }

            // Yet another special handling of TagList column visibility.
            // It is always hidden initially.
            TagList.Visibility = Visibility.Collapsed;
        }

        private void ITableController_AdditionalPropsChanged(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke((Action)UpdateColumns);
        }

        private void UpdateColumns()
        {
            var table = DataContext as ITableController;
            if (table == null) return;

            // Use/unuse standard columns.
            // Unused standard columns are kept in the list, but users can't view them.
            // We use a false value of ColumnInUse atatched property to tell the case.
            // Unused (ColumnInUse == false) columns can be used (ColumnInUse == true) in future reloading.
            // Once used, however, a column will never get back to Unused status.
            foreach (var colInfo in StandardColumns)
            {
                if (GetColumnInUse(colInfo.Column) == false && table.AllRows.Any(colInfo.InUse))
                {
                    SetColumnInUse(colInfo.Column, true);
                }
            }

            // Create columns for additional properties.
            // Their initial shown/hidden states are specified by the DOC module via AdditionalProps.
            // ColumnInUse attached property is always set to true so that users can view any column.
            // We don't check whether an additonal property is actually used;
            // If such a checking is essential for a particular additional property,
            // the responsible AssetReader can do it when constructing the Properties.
            // See TableController.cs for the interaction between the AssetReader and TableController.AdditionalProps. 
            foreach (var props in table.AdditionalProps)
            {
                var path = "[" + props.Index + "]";

                // Don't add a duplicate column.
                if (dataGrid.Columns.Any(col => ((col as DataGridTextColumn)?.Binding as Binding)?.Path?.Path == path)) continue;

                var column = new DataGridTextColumn()
                {
                    Header = props.Key.Replace("_", " "), // XXX: No, we should not do this!
                    Binding = new Binding(path),
                    Visibility = props.Visible ? Visibility.Visible : Visibility.Collapsed,
                    ElementStyle = FindResource("AdditionalColumnElementStyle") as Style,
                };
                column.SetValue(ColumnInUseProperty, true);

                // a sort of a hack to avoid a few columns (containing some very long text) to occupy the entire space.
                // The following code includes several magic numbers that were determined without grounds.  FIXME.
                if (table.AllRows.Take(100)
                    .Select(row => row[props.Index])
                    .Any(text => text?.Length > 50 || text?.IndexOf('\n') >= 0))
                {
                    column.Width = DataGridLengthStar;
                }

                dataGrid.Columns.Add(column);
            }

            // Set FilterOptions attached property to all in-use columns.
            foreach (var column in dataGrid.Columns.Where(GetColumnInUse))
            {
                var grabber = CreateGrabber(column.SortMemberPath);
                var list = table.AllRows.Select(grabber).Distinct().OrderBy(s => string.IsNullOrEmpty(s) ? 0 : 1).ThenBy(s => s).Select(s => FilterOption.Get(s));
                column.SetValue(FilterOptionsProperty, list);
            }
        }

        #endregion

        #region Refresh command

        private void Refresh_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Controller.RefreshCommand.Execute();
            e.Handled = true;
        }

        private void Refresh_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Controller.RefreshCommand.CanExecute();
            e.Handled = true;
        }

        #endregion

        public IEnumerable<DataGridColumn> Columns
        {
            get
            {
                return dataGrid.Columns;
            }
        }

        private void UnselectAll_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            dataGrid.UnselectAllCells();
            e.Handled = true;
        }

        /// <summary>
        /// List of columns as currently shown to the user.
        /// </summary>
        /// <remarks>
        /// Some <see cref="IRowWriter"/> implementation uses it.
        /// </remarks>
        public IColumnDesc[] ColumnDescs
        {
            get
            {
                return dataGrid.Columns
                    .Where(c => c.Visibility == Visibility.Visible)
                    .OrderBy(c => c.DisplayIndex)
                    .Select(c => RowDataColumnDesc.Create(
                        c.Header.ToString(),
                        (c.ClipboardContentBinding as Binding)?.Path?.Path))
                    .ToArray();
            }
        }

        private void dataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            // We manage DataGrid sorting in a slightly different way than usual:
            //  - When a column header is clicked, the table is sorted via the column (as usual),
            //    but Seq (invisible to user) is always automatically specified as the second sort key.
            //  - The second clicking on a same column reverses the sort order (this is normal),
            //    but the third clicking on a sort key column resets the sorting,
            //    as opposed to reverses the sort order again.
            //    The fourth clicking is same as the first clicking.
            //  - Shift key is ignored; users can't specify his/her own secondary sort keys.

            var dataGrid = sender as DataGrid;
            foreach (var c in dataGrid.Columns) c.SortDirection = null;
            var cv = dataGrid.Items as CollectionView;

            var path = e.Column.SortMemberPath;
            if (cv.SortDescriptions.Count == 0 || cv.SortDescriptions[0].PropertyName != path)
            {
                // This is the first clicking on a column.
                cv.SortDescriptions.Clear();
                cv.SortDescriptions.Add(new SortDescription(path, ListSortDirection.Ascending));
                cv.SortDescriptions.Add(new SortDescription("Seq", ListSortDirection.Ascending));
                e.Column.SortDirection = ListSortDirection.Ascending;
            }
            else if (cv.SortDescriptions[0].Direction == ListSortDirection.Ascending)
            {
                // This is the second clicking on a column.
                cv.SortDescriptions[0] = new SortDescription(path, ListSortDirection.Descending);
                e.Column.SortDirection = ListSortDirection.Descending;
            }
            else
            {
                // This should be the third clicking on a column.
                cv.SortDescriptions.Clear();
            }

            e.Handled = true;
        }

        #region QuickFilter dependency property

        public static readonly DependencyProperty QuickFilterProperty =
            DependencyProperty.Register("QuickFilter", typeof(bool), typeof(TableView),
                new FrameworkPropertyMetadata(QuickFilterChangedCallback) { BindsTwoWayByDefault = true, DefaultValue = true });
        
        public bool QuickFilter
        {
            get { return (bool)GetValue(QuickFilterProperty); }
            set { SetValue(QuickFilterProperty, value); }
        }

        private static void QuickFilterChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as TableView).OnQuickFilterChanged(true.Equals(e.NewValue));
        }

        private void OnQuickFilterChanged(bool value)
        {
            var visible = value ? Visibility.Visible : Visibility.Collapsed;
            foreach (var c in dataGrid.Columns)
            {
                var h = c.Header;
            }

            _configService.QuickFilter = value;
        }

        #endregion

        #region FilterBox backends

        /// <summary>
        /// Identifire of the private attached property Column.
        /// </summary>
        /// <remarks>
        /// A Column attached property is attached to a TextBox portion in a FilterBox ComboBox.
        /// It holds a reference to the corresponding DataGridColumn object.
        /// </remarks>
        private static readonly DependencyProperty ColumnProperty
            = DependencyProperty.Register("Column", typeof(DataGridColumn), typeof(TableView));

        /// <summary>
        /// Identifier of the private attached property Filter.
        /// </summary>
        /// <remarks>
        /// A Filter property is attached to a DataGridColumn.
        /// It holds a filtering delegate (of type <see cref="Func{IRowData,Boolean}"/>)
        /// for the column.
        /// It is null if no filtering applies to the column.
        /// </remarks>
        private static readonly DependencyProperty FilterProperty
            = DependencyProperty.Register("Filter", typeof(Func<IRowData, bool>), typeof(TableView));

        private void FilterBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // We want to hook into a PART_EditableTextBox component of a ComboBox.
            // I first thought we can do so in Loaded event handler, but I was wrong.
            // The visual tree appears to be created only after the ComboBox becomes visible,
            // and doing it in IsVisibleChanged event handler sometimes failed...
            // We need some tricky maneuver.

            Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, (Action)delegate ()
            {
                var filterbox = sender as ComboBox;

                // Find a TextBox portion of the ComboBox.
                var textbox = FindVisualChild<TextBox>(filterbox, "PART_EditableTextBox");

                if (textbox == null)
                {
                    // Appears not ready yet ... but why?  FIXME.
                    return;
                }

                // We need to access the corresponding DataGridColumn quickly,
                // so find one now and store it in a handy place.
                var header = FindVisualParent<DataGridColumnHeader>(filterbox);
                textbox.SetValue(ColumnProperty, header.Column);

                // Then, hook into the text box.
                textbox.TextChanged += FilterBox_TextBox_TextChanged;

                // Hooked.  We don't need to do it again.
                filterbox.GotFocus -= FilterBox_GotFocus;
            });
        }

        /// <summary>
        /// A stripped down version of <see cref="DataGridCellInfo"/>.
        /// </summary>
        /// <remarks>
        /// Despite its simple documentation on MSDN,
        /// <see cref="DataGridCellInfo"/> is a complex data type with a lot of hidden (i.e., private/internal) members.
        /// <see cref="CellLocator"/> is its stripped down version that holds only information we need.  
        /// </remarks>
        private struct CellLocator
        {
            public readonly object Item;
            public readonly DataGridColumn Column;

            public CellLocator(object item, DataGridColumn column)
            {
                Item = item;
                Column = column;
            }
        }

        /// <summary>
        /// List of selected cells before the filter started updating.
        /// </summary>
        private readonly List<CellLocator> SelectedCells = new List<CellLocator>();

        private void dataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            SelectedCellsChanged = true;
        }

        private bool SelectedCellsChanged = false;

        private readonly DispatcherTimer FilterTimer;

        private readonly HashSet<TextBox> PendingFilterTextChanges = new HashSet<TextBox>();

        private void FilterBox_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTimer.Stop();
            PendingFilterTextChanges.Add(sender as TextBox);
            FilterTimer.Start();
        }

        private void FilterTimer_Tick(object sender, EventArgs e)
        {
            FilterTimer.Stop();
            foreach (var textbox in PendingFilterTextChanges)
            {
                UpdateColumnFilter(textbox);
            }
            PendingFilterTextChanges.Clear();
            InstallFilter();
        }

        private void UpdateColumnFilter(TextBox textbox)
        {
            var column = textbox.GetValue(ColumnProperty) as DataGridColumn;

            Func<IRowData, bool> filter;
            var text = textbox.Text;
            if (string.IsNullOrEmpty(text))
            {
                filter = null;
            }
            else if (!text.StartsWith(FilterOption.MARKER))
            {
                // This is the text the user typed in.
                var regex = new Regex(Regex.Escape(textbox.Text), RegexOptions.IgnoreCase);
                var grabber = CreateGrabber(column.SortMemberPath);
                filter = row => regex.IsMatch(grabber(row));
            }
            else
            {
                // This is the text selected from the drop-down.
                var match_text = GetFilterOptions(column).FirstOrDefault(w => w.DisplayString == text)?.String;
                if (match_text == null)
                {
                    // This is probably because the user is editing a text from dropdown list.
                    // We can't handle the case as the user expects. Give the user a chance to know it. 
                    textbox.Clear();
                    filter = null;
                }
                else
                {
                    // This is really from the drop-down.
                    var grabber = CreateGrabber(column.SortMemberPath);
                    filter = row => match_text == grabber(row);
                }
            }
            column.SetValue(FilterProperty, filter);
        }

        private void InstallFilter()
        {
            // If the selection has been somehow changed outside of this method,
            if (SelectedCellsChanged)
            {
                // Save the currently selected cells in SelectedCells,
                // so that we can preserve the selection across filter changes.
                SelectedCells.Clear();
                SelectedCells.Capacity = Math.Max(SelectedCells.Capacity, dataGrid.SelectedCells.Count);
                SelectedCells.AddRange(dataGrid.SelectedCells.Select(ci => new CellLocator(ci.Item, ci.Column)));
            }

            var cv = dataGrid.Items as CollectionView;

            // Create a new filter (reflecting the text box updates) and set it to the controller. 
            var matchers = dataGrid.Columns.Select(c => c.GetValue(FilterProperty) as Func<IRowData, bool>).Where(m => m != null).ToArray();
            switch (matchers.Length)
            {
                case 0:
                    cv.Filter = null;
                    break;
                case 1:
                    cv.Filter = row => matchers[0](row as IRowData);
                    break;
                default:
                    cv.Filter = row => matchers.All(m => m(row as IRowData));
                    break;
            }

            // Select all cells (that passed the filter) that was selected previously.
            foreach (var s in SelectedCells)
            {
                if (dataGrid.Items.Contains(s.Item))
                {
                    dataGrid.SelectedCells.Add(new DataGridCellInfo(s.Item, s.Column));
                }
            }

            // If any selected cell passed the filter, make it viewable on the window.
            if (dataGrid.SelectedCells.Count > 0)
            {
                var s = dataGrid.SelectedCells[0];
                dataGrid.ScrollIntoView(s.Item, s.Column);
            }

            SelectedCellsChanged = false;
        }

        private static Func<IRowData, string> CreateGrabber(string path)
        {
            if (path.StartsWith("[") && path.EndsWith("]"))
            {
                var index = int.Parse(path.Substring(1, path.Length - 2));
                return r => r[index] ?? "";
            }

            var property = typeof(IRowData).GetProperty(path) ?? typeof(ITransPair).GetProperty(path);
            if (property.PropertyType == typeof(string))
            {
                return r => property.GetValue(r) as string ?? "";
            }
            else
            {
                return r => property.GetValue(r)?.ToString() ?? ""; 
            }
        }

        /// <summary>
        /// Find a node of a particular type and optinally a name in a visual tree.
        /// </summary>
        /// <typeparam name="ChildType">Type of a node to find.</typeparam>
        /// <param name="obj">An object which forms a visual tree to find a child in.</param>
        /// <param name="name">name of a child to find, or null if name is not important.</param>
        /// <returns>Any one of the children of the type and the name, or null if none found.</returns>
        private static ChildType FindVisualChild<ChildType>(DependencyObject obj, string name = null) where ChildType : FrameworkElement
        {
            if (obj == null) return null;
            if (obj is ChildType &&
                (name == null || (obj as FrameworkElement).Name == name)) return obj as ChildType;
            for (int i = VisualTreeHelper.GetChildrenCount(obj) - 1; i >= 0; --i)
            {
                var child = FindVisualChild<ChildType>(VisualTreeHelper.GetChild(obj, i), name);
                if (child != null) return child;
            }
            return null;
        }

        private static ParentType FindVisualParent<ParentType>(DependencyObject obj) where ParentType : DependencyObject
        {
            while (obj != null)
            {
                if (obj is ParentType) return obj as ParentType;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }

        #endregion

        private void dataGrid_CopyingRowClipboardContent(object sender, DataGridRowClipboardEventArgs e)
        {
        }
    }

    #region class VisibilityToBooleanConverter

    /// <summary>
    /// Converts a Boolean value to and from a <see cref="Visibility"/> value.
    /// </summary>
    /// <seealso cref="BooleanToVisibilityConverter"/>
    /// <remarks>
    /// This class supports an opposite direction of conversion/binding than <see cref="BooleanToVisibilityConverter"/>. 
    /// </remarks>
    [ValueConversion(typeof(Visibility), typeof(bool))]
    public class VisibilityToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as Visibility?) == Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as bool?) == true ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    #endregion

    #region class SubtractingConverter

    /// <summary>
    /// Converts a double value to another double value by subtracting a constant double value.
    /// </summary>
    public class SubtractingConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as double?) - (parameter as IConvertible)?.ToDouble(culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as double?) + (parameter as IConvertible)?.ToDouble(culture);
        }
    }

    #endregion

    #region class RowStyleSelector

    /// <summary>
    /// A StyleSelector that selects one of two styles to see whether a row is at an Asset boundary.
    /// </summary>
    public class RowStyleSelector : StyleSelector
    {
        /// <summary>
        /// Style to apply to a row at an Asset boundary.
        /// </summary>
        public Style BoundaryStyle { get; set; }

        /// <summary>
        /// Style to apply to a row other than at an Asset boundary.
        /// </summary>
        public Style MiddleStyle { get; set; }

        public override Style SelectStyle(object item, DependencyObject container)
        {
            var info = item as IRowData;
            if (info == null) return null;

            // We can't simply check info.SerialInAsset == 1,
            // since the DataGrid may be sorted by Source or Target contents. 

            var itemsControl = ItemsControl.ItemsControlFromItemContainer(container);
            var index = itemsControl.ItemContainerGenerator.IndexFromContainer(container);
            if (index == 0) return BoundaryStyle;

            var prev = itemsControl.Items[index - 1] as IRowData;
            return object.ReferenceEquals(info.AssetIdentity, prev?.AssetIdentity) ? MiddleStyle : BoundaryStyle;
        }
    }

    #endregion
}
