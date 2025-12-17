using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Primple.Desktop;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, Func<UserControl>> _viewFactories;
    private readonly Dictionary<string, UserControl> _views = new Dictionary<string, UserControl>();

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize view factories (Lazy loading)
        _viewFactories = new Dictionary<string, Func<UserControl>>
        {
            { "Home", () => new Views.HomeView() },
            { "Editor", () => new Views.StlEditorView() },
            { "Maps", () => new Views.MapsView() },
            { "Image", () => new Views.ImageTo3dView() },
            { "Templates", () => new Views.TemplatesView() }
        };

        // Navigate to Home by default
        NavigateTo("Home");
        
        SetupNavigation();
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
                        NavigateTo(tag);
                    }
                };
            }
        };
    }

    private void NavigateTo(string viewTag)
    {
        try
        {
            UserControl? view = null;

            if (_views.TryGetValue(viewTag, out var existingView))
            {
                view = existingView;
            }
            else if (_viewFactories.TryGetValue(viewTag, out var factory))
            {
                view = factory();
                _views[viewTag] = view;
            }

            if (view != null && MainContent != null)
            {
                MainContent.Content = view;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading view '{viewTag}': {ex.Message}", "Navigation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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