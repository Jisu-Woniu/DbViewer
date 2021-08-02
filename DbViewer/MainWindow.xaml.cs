using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DbViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();

        private readonly HashSet<SqliteConnection> _activeConnections = new();
        private string _header;

        private void AboutMenu_Click(object sender, RoutedEventArgs e) => new AboutWindow()
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        }.ShowDialog();

        private void NavigatorItem_Selected(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource == e.Source)
            {
                _header = null;
                e.Handled = true;
                return;
            }

            if (e.OriginalSource is TreeViewItem tvi && e.Source is TreeViewItem parent)
            {
                _header = tvi.Header as string;
                SqliteCommand command = new()
                {
                    CommandText = $"SELECT * FROM \'{tvi.Header}\'",
                    Connection = parent.DataContext as SqliteConnection
                };
                if (command.Connection == null)
                {
                    MessageBox.Show("Invalid database connection.");
                    return;
                }

                try
                {
                    command.Connection.Open();
                    DataViewer.DataContext = command;
                    using SqliteDataReader reader = command.ExecuteReader();
                    DataViewer.ItemsSource = reader.OfType<object>().ToList();
                }
                finally
                {
                    command.Connection.Close();
                }
                SqliteCommand commandKey = new()
                {
                    Connection = command.Connection,
                    CommandText = $"SELECT name FROM pragma_table_info('{tvi.Header}') WHERE pk != 0;"
                };
                try
                {
                    commandKey.Connection.Open();
                    using SqliteDataReader reader = commandKey.ExecuteReader();
                    List<string> key = reader.OfType<IDataRecord>().Select(r => r.GetString(0)).ToList();
                    foreach (DataGridColumn column in DataViewer.Columns)
                    {
                        string header = column.Header.ToString();
                        if (key.Contains(header))
                        {
                            column.Header = new StackPanel()
                            {
                                Orientation = Orientation.Horizontal,
                                Children =
                                    {
                                        new TextBlock() {Text = header},
                                        new Image()
                                        {
                                            Height = 15,
                                            Margin = new Thickness(5, 0, 0, 0),
                                            Stretch = Stretch.Uniform,
                                            Source = new BitmapImage(new Uri("/Imageres_dll_077.png",
                                                UriKind.Relative))
                                        }
                                    }
                            };
                        }
                    }
                }
                finally
                {
                    command.Connection.Close();
                }
            }

            e.Handled = true;
        }

        private void CloseConnection_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem node = ((FrameworkElement)sender).DataContext as TreeViewItem;
            SqliteConnection connection = node?.DataContext as SqliteConnection;
            _activeConnections.Remove(connection);
            connection?.Dispose();
            Navigator.Items.Remove(node);
            DataViewer.DataContext = DataViewer.ItemsSource = null;
            e.Handled = true;
        }

        private void ExitMenu_Click(object sender, RoutedEventArgs e) => Close();

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            foreach (SqliteConnection connection in _activeConnections)
            {
                connection.Dispose();
            }
        }

        private void OpenCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new() { Filter = "SQLite 数据库文件 (*.db)|*.db" };

            if (openFileDialog.ShowDialog(this) != true)
                return;

            SqliteConnectionStringBuilder connectionStringBuilder = new() { DataSource = openFileDialog.FileName };
            SqliteConnection connection = new() { ConnectionString = connectionStringBuilder.ToString() };
            using SqliteCommand command = new()
            {
                CommandText = "SELECT sm.name FROM sqlite_master sm WHERE sm.type='table';",
                Connection = connection
            };
            try
            {
                connection.Open();
                _activeConnections.Add(connection);
                SqliteDataReader reader = command.ExecuteReader();
                List<string> tableList = reader.OfType<IDataRecord>()
                    .Select(r => r.GetString(0))
                    .OrderBy(s => s).ToList();
                TreeViewItem node = new()
                {
                    Header = openFileDialog.SafeFileName,
                    DataContext = connection,
                    ItemsSource = tableList,
                    ContextMenu = new()
                };
                if (node.ContextMenu != null)
                {
                    MenuItem item = new() { Header = "关闭连接" };
                    item.Click += CloseConnection_Click;
                    node.ContextMenu.Items.Add(item);
                    node.ContextMenu.DataContext = node;
                }

                _ = Navigator.Items.Add(node);
            }
            catch (SqliteException exception)
            {
                _ = MessageBox.Show(this,
                    $"SQLite 错误：\n{exception.Message}\n{exception.StackTrace}",
                    "SQLite 错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception exception)
            {
                _ = MessageBox.Show(this,
                    $"未知错误：\n{exception.Message}\n{exception.StackTrace}",
                    "未知错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                connection.Close();
            }
        }

        private void RefreshCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (DataViewer.ItemsSource != null)
            {
                DataViewer.ItemsSource = null;
                if (DataViewer.DataContext is not SqliteCommand command)
                    return;
                try
                {
                    command.Connection.Open();
                    using SqliteDataReader reader = command.ExecuteReader();
                    DataViewer.ItemsSource = reader.OfType<object>().ToList();
                }
                finally
                {
                    command.Connection.Close();
                }
                SqliteCommand commandKey = new()
                {
                    Connection = command.Connection,
                    CommandText = $"SELECT name FROM pragma_table_info('{_header}') WHERE pk != 0;"
                };
                try
                {
                    commandKey.Connection.Open();
                    using SqliteDataReader reader = commandKey.ExecuteReader();
                    List<string> key = reader.OfType<IDataRecord>().Select(r => r.GetString(0)).ToList();
                    foreach (DataGridColumn column in DataViewer.Columns)
                    {
                        string header = column.Header.ToString();
                        if (key.Contains(header))
                        {
                            column.Header = new StackPanel()
                            {
                                Orientation = Orientation.Horizontal,
                                Children =
                                    {
                                        new TextBlock() {Text = header},
                                        new Image()
                                        {
                                            Height = 15,
                                            Margin = new Thickness(5, 0, 0, 0),
                                            Stretch = Stretch.Uniform,
                                            Source = new BitmapImage(new Uri("/Imageres_dll_077.png",
                                                UriKind.Relative))
                                        }
                                    }
                            };
                        }
                    }
                }
                finally
                {
                    command.Connection.Close();
                }
            }

            e.Handled = true;
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Any(s => s.EndsWith(".db", StringComparison.InvariantCultureIgnoreCase)))
                {
                    e.Effects = DragDropEffects.Link;
                }
            }

            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] data = (string[])e.Data.GetData(DataFormats.FileDrop);
                //MessageBox.Show(string.Join('\n', data));
                IEnumerable<string> files = data.Where(x =>
                    x.EndsWith(".db", StringComparison.InvariantCultureIgnoreCase));
                foreach (string file in files)
                {
                    SqliteConnectionStringBuilder connectionStringBuilder = new() { DataSource = file };
                    SqliteConnection connection = new() { ConnectionString = connectionStringBuilder.ToString() };
                    using SqliteCommand command = new()
                    {
                        CommandText = "SELECT sm.name FROM sqlite_master sm WHERE sm.type='table';",
                        Connection = connection
                    };
                    try
                    {
                        connection.Open();
                        _activeConnections.Add(connection);
                        SqliteDataReader reader = command.ExecuteReader();
                        List<string> tableList = reader.OfType<IDataRecord>()
                            .Select(r => r.GetString(0))
                            .OrderBy(s => s).ToList();
                        TreeViewItem node = new()
                        {
                            Header = file[(file.LastIndexOf('\\') + 1)..],
                            DataContext = connection,
                            ItemsSource = tableList,
                            ContextMenu = new()
                        };
                        if (node.ContextMenu != null)
                        {
                            MenuItem item = new() { Header = "关闭连接" };
                            item.Click += CloseConnection_Click;
                            node.ContextMenu.Items.Add(item);
                            node.ContextMenu.DataContext = node;
                        }

                        _ = Navigator.Items.Add(node);
                    }
                    catch (SqliteException exception)
                    {
                        _ = MessageBox.Show(this,
                            $"SQLite 错误：\n{exception.Message}\n{exception.StackTrace}",
                            "SQLite 错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch (Exception exception)
                    {
                        _ = MessageBox.Show(this,
                            $"未知错误：\n{exception.Message}\n{exception.StackTrace}",
                            "未知错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        connection.Close();
                    }
                }
            }
        }
    }
}
