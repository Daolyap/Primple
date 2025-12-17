using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Primple.Desktop;

public partial class MainWindow : Window
{

    private readonly Dictionary<string, UserControl> _views = new Dictionary<string, UserControl>();

    public MainWindow()
    {
        InitializeComponent();
        
        // Setup Navigation
        _views.Add("Home", new Views.HomeView());
        _views.Add("StlEditor", new Views.StlEditorView());
        _views.Add("Maps", new Views.MapsView());
        _views.Add("ImageTo3d", new Views.ImageTo3dView());
        _views.Add("Templates", new Views.TemplatesView());

        Navigate("Home");
        SetupNavigation();
    }

    private void Navigate(string viewTag)
    {
        try
        {
            if (_views.TryGetValue(viewTag, out var view))
            {
                if (MainContent != null)
                {
                    MainContent.Content = view;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading view '{viewTag}': {ex.Message}");
        }
    }

    private void SetupNavigation()
    {
        this.Loaded += (s, e) => 
        {
            foreach (var rb in FindVisualChildren<RadioButton>(this))
            {
                rb.Click += (sender, args) =>
                {
                    if (sender is RadioButton btn && btn.Tag is string tag)
                    {
                        Navigate(tag);
                    }
                };
            }
        };
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
            if (child != null && child is T t)
            {
                yield return t;
            }

            foreach (T childOfChild in FindVisualChildren<T>(child))
            {
                yield return childOfChild;
            }
        }
    }
}