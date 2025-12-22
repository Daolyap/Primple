using System.IO;
using System.IO.Compression;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using System.Text.Json;

namespace Primple.Desktop.Services;

public interface IFigService
{
    void SaveProject(string filePath, Model3DGroup model, string projectName);
    Model3DGroup LoadProject(string filePath);
}

public class FigService : IFigService
{
    private class ProjectMetadata
    {
        public string Name { get; set; } = "Untitled";
        public DateTime Created { get; set; }
        public string Version { get; set; } = "1.0";
    }

    public void SaveProject(string filePath, Model3DGroup model, string projectName)
    {
        // 1. Export Model to STL
        string tempPath = Path.GetTempFileName();
        try
        {
            var exporter = new StlExporter();
            using (var stream = File.Create(tempPath))
            {
                exporter.Export(model, stream);
            }

            // 2. Create Metadata
            var metadata = new ProjectMetadata 
            { 
                Name = projectName, 
                Created = DateTime.Now 
            };
            string json = JsonSerializer.Serialize(metadata);

            // 3. Create Zip (.FIG)
            if (File.Exists(filePath)) File.Delete(filePath);

            using (var archive = ZipFile.Open(filePath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(tempPath, "model.stl");
                
                var entry = archive.CreateEntry("project.json");
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write(json);
                }
            }
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public Model3DGroup LoadProject(string filePath)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            ZipFile.ExtractToDirectory(filePath, tempDir);

            string stlPath = Path.Combine(tempDir, "model.stl");
            if (!File.Exists(stlPath))
            {
                throw new FileNotFoundException("Invalid .fig file: missing model.stl");
            }

            var importer = new ModelImporter();
            var group = importer.Load(stlPath);
            
            // Optional: Apply default material if lost (STL doesn't keep color usually)
            if (group != null)
            {
               foreach (var child in group.Children)
               {
                   if (child is GeometryModel3D gm)
                   {
                        var mat = new MaterialGroup();
                        mat.Children.Add(new DiffuseMaterial(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204))));
                        mat.Children.Add(new SpecularMaterial(System.Windows.Media.Brushes.White, 100));
                        gm.Material = mat;
                        gm.BackMaterial = mat;
                   }
               }
            }

            return group ?? new Model3DGroup();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
