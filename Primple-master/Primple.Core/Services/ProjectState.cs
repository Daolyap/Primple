using System.ComponentModel;
using System.Windows.Media.Media3D;

namespace Primple.Core.Services;

public interface IProjectState : INotifyPropertyChanged
{
    Model3DGroup CurrentModel { get; set; }
    string CurrentProjectName { get; set; }
    void UpdateModel(Model3DGroup model);
}

public class ProjectState : IProjectState
{
    private Model3DGroup _currentModel = new Model3DGroup();
    private string _currentProjectName = "Untitled";

    public event PropertyChangedEventHandler? PropertyChanged;

    public Model3DGroup CurrentModel
    {
        get => _currentModel;
        set
        {
            _currentModel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentModel)));
        }
    }

    public string CurrentProjectName
    {
        get => _currentProjectName;
        set
        {
            _currentProjectName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentProjectName)));
        }
    }

    public void UpdateModel(Model3DGroup model)
    {
        CurrentModel = model;
    }
}
