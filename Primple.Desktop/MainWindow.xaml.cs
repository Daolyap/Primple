using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Primple.Desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
#pragma warning disable CA1515 // UI classes are effectively public
public sealed partial class MainWindow : Window
#pragma warning restore CA1515
{
    public MainWindow()
    {
        InitializeComponent();
    }
}