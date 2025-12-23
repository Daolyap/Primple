using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Primple.Desktop.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void NavigateToMaps_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.Navigate("Maps");
    }

    private void NavigateToImageTo3d_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.Navigate("ImageTo3d");
    }

    private void NavigateToTemplates_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        mainWindow?.Navigate("Templates");
    }

    private void ContactEmail_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "mailto:contact@daolyap.dev",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open mail client for contact email: {ex}");
            Clipboard.SetText("contact@daolyap.dev");
            MessageBox.Show("Email copied to clipboard: contact@daolyap.dev", "Contact");
        }
    }
}
