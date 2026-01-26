using Microsoft.Extensions.DependencyInjection;
using Primple.App.ViewModels;
using System.Windows;

namespace Primple.App.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainViewModel>();
    }
}
