using System.Windows;
using System.Windows.Controls;

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
}
