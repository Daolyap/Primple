using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using Primple.Core.Services;

namespace Primple.Desktop.Views;

/// <summary>
/// Converter to color log entries based on their level
/// </summary>
public class LogLevelColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string logText)
        {
            if (logText.Contains("[ERROR]")) return new SolidColorBrush(Color.FromRgb(255, 100, 100));
            if (logText.Contains("[WARN]")) return new SolidColorBrush(Color.FromRgb(255, 200, 100));
            if (logText.Contains("[DEBUG]")) return new SolidColorBrush(Color.FromRgb(150, 150, 150));
            return new SolidColorBrush(Color.FromRgb(200, 200, 200));
        }
        return new SolidColorBrush(Colors.White);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public partial class LogViewerWindow : Window
{
    private readonly ILogService _logService;
    private readonly ObservableCollection<string> _displayLogs = new();

    public LogViewerWindow(ILogService logService)
    {
        _logService = logService;
        
        // Add the converter to resources before InitializeComponent
        Resources.Add("LogLevelColorConverter", new LogLevelColorConverter());
        
        InitializeComponent();
        
        // Initialize display logs from existing entries
        foreach (var log in _logService.Logs)
        {
            _displayLogs.Add(log.ToString());
        }
        
        // Bind to our local observable collection for efficient updates
        LogListBox.ItemsSource = _displayLogs;
        UpdateLogCount();

        // Subscribe to collection changes
        _logService.Logs.CollectionChanged += Logs_CollectionChanged;
        
        Closed += (s, e) => _logService.Logs.CollectionChanged -= Logs_CollectionChanged;
    }

    private void Logs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Handle incremental updates instead of rebuilding entire list
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems != null)
                    {
                        foreach (LogEntry item in e.NewItems)
                        {
                            _displayLogs.Add(item.ToString());
                        }
                    }
                    break;
                    
                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems != null)
                    {
                        foreach (LogEntry item in e.OldItems)
                        {
                            _displayLogs.Remove(item.ToString());
                        }
                    }
                    break;
                    
                case NotifyCollectionChangedAction.Reset:
                    _displayLogs.Clear();
                    break;
                    
                default:
                    // For other actions, rebuild the list
                    _displayLogs.Clear();
                    foreach (var log in _logService.Logs)
                    {
                        _displayLogs.Add(log.ToString());
                    }
                    break;
            }
            
            UpdateLogCount();
            
            // Auto-scroll to bottom
            if (LogListBox.Items.Count > 0)
            {
                LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
            }
        });
    }

    private void UpdateLogCount()
    {
        LogCountText.Text = $" ({_logService.Logs.Count} entries)";
    }

    private void ExportLogs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text Files (*.txt)|*.txt|Log Files (*.log)|*.log",
            DefaultExt = ".txt",
            FileName = $"Primple_Debug_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            _logService.Export(dialog.FileName);
            MessageBox.Show($"Logs exported to:\n{dialog.FileName}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        _logService.Clear();
        _displayLogs.Clear();
        UpdateLogCount();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
