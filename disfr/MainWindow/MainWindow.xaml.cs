﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.ComponentModel;
using disfr.Configuration;
using Dragablz;
using WpfColorFontDialog;

namespace disfr.UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IComparable<MainWindow>
    {
        private IConfigService _configService; 
        
        public MainWindow()
        {
            InitializeComponent();
            DataContextChanged += this_DataContextChanged;
            tables.ClosingItemCallback = tables_ClosingItemCallback;
            tables.SelectionChanged += Tables_SelectionChanged;
            tables.
            //tabControl.Items.Clear();
            Dispatcher.UnhandledException += Dispatcher_UnhandledException;
            _configService = ConfigService.Instance;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show((e.OriginalSource as MenuItem).Header.ToString());
        }

        private void Tables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && _configService.Font != null)
            {
                var fontInfo = _configService.Font.GetFontInfo();
                foreach (var item in e.AddedItems)
                {
                    FontInfo.ApplyFont(item as Control, fontInfo);
                }
            }

            if (e.RemovedItems != null && e.RemovedItems.Count == 1)
            {
                TableView tableView = e.RemovedItems[0] as TableView;
                if (tableView == null)
                {
                    return;
                }

                var columns = tableView.Columns.ToList();

                for (int i = 0; i < columns.Count(); i++)
                {
                    ColumnInfo columnInfo = new ColumnInfo() { Id = columns[i].Header.ToString(), IsVisible = (columns[i].Visibility == Visibility.Visible ? true: false) };
                    _configService.AddColumn(columnInfo);
                }
            }

            if (e.AddedItems != null && e.AddedItems.Count == 1)
            {
                TableView tableView = e.AddedItems[0] as TableView;
                if (tableView == null)
                {
                    return;
                }

                var columns = tableView.Columns.ToList();

                for (int i = 0; i < columns.Count(); i++)
                {
                    var column = _configService.GetColumn(columns[i].Header.ToString());
                    if (column != null)
                    {
                        columns[i].Visibility = column.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }

        private void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (e.Handled) return;
            var exception = e.Exception;
            Dispatcher.BeginInvoke((Action)delegate
            {
                new ExceptionDialog() { Exception = exception, Owner = this }.ShowDialog();
            });
            e.Handled = true;
        }


        /// <summary>
        /// A static instance of <see cref="InterTabClient"/> shared by all <see cref="TabablzControl"/> on all windows.
        /// </summary>
        public static readonly InterTabClient InterTabClient = new InterTabClient();


        private IMainController Controller { get { return (IMainController)DataContext; } }


        private void this_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // This code tries to cover the cases that the DataContext is changed to another backend on-the-fly.
            // However, it is not tested.  Assume it doesn't.
            if (e.OldValue != null)
            {
                ((INotifyPropertyChanged)e.OldValue).PropertyChanged -= DataContext_PropertyChanged;
            }
            if (e.NewValue != null)
            {
                ((INotifyPropertyChanged)e.NewValue).PropertyChanged += DataContext_PropertyChanged;
            }
            SyncToTables();
        }

        private bool IsClosing = false;

        private void DataContext_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Tables")
            {
                SyncToTables();
            }
        }

        /// <summary>
        /// Syncrhonize tabs to the Tables on the backend.
        /// </summary>
        private void SyncToTables()
        {
            bool previously_used = tables.Items.Count > 0;

            // Remove all redundant tabs, i.e., tabs that have no corresponding tables in the backend.
            foreach (var tab in tables.Items.Cast<TableView>().Where(i => !Controller.Tables.Contains(i.DataContext)).ToList())
            {
                tables.Items.Remove(tab);
            }

            // Create a tab for each of new tables, then add it to the appropriate window.
            foreach (var t in Controller.Tables.Where(t => t.Tag != null))
            {
                (t.Tag as MainWindow)?.AddTabFromITableController(t);
                t.Tag = null;
            }

            // If this MainWindow was used previously and lost all its content tabs, close it.
            if (!IsClosing && previously_used && tables.Items.Count == 0)
            {
                Close();
            }
        }

        private void AddTabFromITableController(ITableController t)
        {
            if (!IsClosing)
            {
                tables.Items.Add(new TableView(_configService) { DataContext = t });
                tables.SelectedIndex = tables.Items.Count - 1;
            }
        }

        #region ActiveMainWindow static property

        private static int ActivatedCount = 0;

        private int LastActivated = 0;

        private void this_Activated(object sender, EventArgs e)
        {
            LastActivated = ++ActivatedCount;
        }

        int IComparable<MainWindow>.CompareTo(MainWindow other)
        {
            return LastActivated - other.LastActivated;
        }

        private static MainWindow ActiveMainWindow
        {
            get { return Application.Current.Windows.OfType<MainWindow>().Max(); }
        }

        #endregion

        private void tables_ClosingItemCallback(ItemActionCallbackArgs<TabablzControl> args)
        {
            var tab = args.DragablzItem.Content as TableView;
            Controller.CloseCommand.Execute(tab?.Controller);
        }

        private void this_Closing(object sender, CancelEventArgs e)
        {
            IsClosing = true;
            foreach (var tab in tables.Items.Cast<TableView>().ToArray())
            {
                Controller.CloseCommand.Execute(tab.Controller);
            }
        }

        #region RoutedCommand Handlers

        #region Open

        private readonly DuckOpenFileDialog OpenFileDialog = new DuckOpenFileDialog();

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Under some circumstances (that we can't control; e.g., what shortcuts the user has on his/her desktop),
            // OpenFileDialog.ShowDialog() may require very long time (more than several seconds)
            // after a user clicks on OK or Cancel button and the dialog box disappears.
            // It appears to a user that this app stops working.
            // I tried to work around it by setting Busy flag early, though it seems not very effective...

            Controller.Busy = true;
            OpenFileDialog.Filter = Controller.OpenFilterString;
            if (OpenFileDialog.ShowDialog(this))
            {
                var filenames = OpenFileDialog.FileNames;
                var index = OpenFileDialog.FilterIndex - 1; // Returned index is 1-based but we expect a 0-based index.
                var single_tab = OpenFileDialog.SingleTab;
                Controller.OpenCommand.Execute(filenames, index, single_tab, this);

                //if (_configService.Font != null)
                //{
                //    var fontInfo = _configService.Font.GetFontInfo();
                //    FontInfo.ApplyFont(tables.SelectedContent as Control, fontInfo);
                //}

            }
            else
            {
                Controller.Busy = false;
            }
            e.Handled = true;
        }

        /// <summary>
        /// A special method to simulate Open RoutedCommand.
        /// </summary>
        /// <remarks>
        /// This is used by <see cref="App"/> only.
        /// </remarks>
        public void OpenFiles(string[] filenames, bool single_tab)
        {
            if (filenames?.Length > 0)
            {
                Controller.Busy = true;
                Controller.OpenCommand.Execute(filenames, -1, single_tab, this);
            }
        }

        #endregion

        #region SaveAs

        private SaveFileDialog SaveFileDialog = new SaveFileDialog();

        private void SaveAs_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var table_view = e.Parameter as TableView;
            var table = table_view?.Controller as ITableController;
            Controller.Busy = true;
            SaveFileDialog.Filter = Controller.SaveAsFilterString;
            SaveFileDialog.FileName = table_view?.Controller?.Name;
            if (SaveFileDialog.ShowDialog(this) == true)
            {
                var filename = SaveFileDialog.FileName;
                var index = SaveFileDialog.FilterIndex - 1; // Returned index is 1-based but we expect a 0-based index.
                Controller.SaveAsCommand.Execute(filename, index, table_view?.Controller, table_view?.ColumnDescs);
            }
            else
            {
                Controller.Busy = false;
            }
            e.Handled = true;

        }

        private void SaveAs_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var table_view = e.Parameter as TableView;
            // We omit passing IColumnDesc[] object to column parameter of SaveAsCommand_CanExecute,
            // because creation of TableView.ColumnDescs requires some overhead, 
            // but CanExecute doesn't use it actually.
            e.CanExecute = table_view != null &&
                Controller.SaveAsCommand.CanExecute(null, -1, table_view?.Controller, null);
            e.Handled = true;
        }

        #endregion

        #region OpenAlt

        private void OpenAlt_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Controller.Busy = true;
            var tc = e.Parameter as ITableController;
            var dlg = new OriginChooserDialog();
            dlg.Owner = this;
            dlg.AllOrigins = tc.AltAssetOrigins;
            if (dlg.ShowDialog() == true)
            {
                Controller.OpenAltCommand.Execute(tc, dlg.SelectedOrigins, this);
            }
            else
            {
                Controller.Busy = false;
            }
            e.Handled = true;
        }

        private void OpenAlt_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var tc = e.Parameter as ITableController;
            e.CanExecute = Controller.OpenAltCommand.CanExecute(tc, null, this);
            e.Handled = true;
        }

        #endregion

        #region Font

        private void Font_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dlg = new ColorFontDialog(showColorPicker: false);
            dlg.Font = FontInfo.GetControlFont(tables.SelectedContent as Control);
            if (dlg.ShowDialog() == true)
            {
                foreach (var item in tables.Items)
                {
                    FontInfo.ApplyFont(item as Control, dlg.Font);
                }

                //FontInfo.ApplyFont(tables.SelectedContent as Control, dlg.Font);
                var fontConfig = FontConfiguration.GetFontConfiguration(dlg.Font);
                _configService.Font = fontConfig;
            }
            e.Handled = true;
        }

        private void Font_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (tables.SelectedContent as Control) != null;
            e.Handled = true;
        }

        #endregion

        #region About

        private void About_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            new AboutDialog() { Owner = this, PluginNames = Controller.PluginNames }.ShowDialog();
            e.Handled = true;
        }

        #endregion

        #region Debug

        private void Debug_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            throw new Exception("DEBUG!");
        }

        #endregion

        #endregion

        #region Drag-n-Drop

        private void this_PreviewDragEnter(object sender, DragEventArgs e)
        {
            DragEnterOver(e);
        }

        private void this_PreviewDragOver(object sender, DragEventArgs e)
        {
            DragEnterOver(e);
        }

        private void DragEnterOver(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // We can only handle FileDrop (CF_HDROP) clipboard format.
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else if (e.Data.GetDataPresent("FileGroupDescriptorW")
                || e.Data.GetDataPresent("Shell IDList Array"))
            {
                // Some apps try to pass us files in
                // CFSTR_FILEDESCRIPTOR ("FileGroupDescriptorW") and/or
                // CFSTR_SHELLIDLIST ("Shell IDList Array") clipboard formats,
                // which we can't handle.
                // I believe it's better to explicitly reject such drag-n-drop,
                // because the difference between CFSTR_FILEDESCRIPTOR and CF_HDROP
                // is not apparent to end users.
                // ... ah, yes, ideally, we SHOULD accept CFSTR_FILEDESCRIPTOR.  FIXME.
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void this_PreviewDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Handled = true;

                var single_tab = (e.KeyStates & DragDropKeyStates.ControlKey) != 0;
                var filenames = e.Data.GetData(DataFormats.FileDrop) as string[];

                Controller.Busy = true;
                Controller.OpenCommand.Execute(filenames, -1, single_tab, this);
            }
        }

        #endregion

        private void Tables_OnSelected(object sender, RoutedEventArgs e)
        {
            
        }

        private void Tables_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems != null && e.AddedItems.Count == 1)
            {
                var tabViiew = e.AddedItems[0] as TableView;
                if (tabViiew == null)
                {
                    return;
                }

                var service = ConfigService.Instance;

                tabViiew.QuickFilter = service.QuickFilter;
                (tabViiew.Controller as TableController).ShowLocalSerial = service.ShowLocalSerial;
                (tabViiew.Controller as TableController).ShowLongAssetName = service.ShowLongAssetName;
                bool showAll = service.ShowAll;
                (tabViiew.Controller as TableController).ShowAll = showAll;
                (tabViiew.Controller as TableController).ShowChanges = service.ShowChanges;
                (tabViiew.Controller as TableController).ShowSpecials = service.ShowSpecials;
                if (service.TagShowing != 0)
                {
                    (tabViiew.Controller as TableController).TagShowing = (TagShowing)service.TagShowing;
                }

            }
        }
    }
}
