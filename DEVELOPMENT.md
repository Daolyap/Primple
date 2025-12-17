# Primple Development Guide

**For**: Humans and LLMs continuing development
**No context required**: This guide is complete and self-contained

---

## Quick Start for New Developers

### Prerequisites

- **Windows 10/11** (required for WPF development)
- **.NET SDK 10.0** or later
- **Visual Studio 2022** or **VS Code** with C# extension
- **Git** for version control

### First Time Setup

```bash
# Clone the repository
git clone https://github.com/Daolyap/Primple.git
cd Primple

# Restore dependencies
dotnet restore Primple.slnx

# Build the solution
dotnet build Primple.slnx --configuration Debug

# Run tests
dotnet test Primple.slnx

# Run the application
dotnet run --project Primple.Desktop/Primple.Desktop.csproj
```

### Building for Release

```bash
# Build MSI installer (Windows only)
# This is handled by GitHub Actions automatically on push to main

# Build portable EXE manually
dotnet publish Primple.Desktop/Primple.Desktop.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish/portable
```

---

## Project Rules (CRITICAL - READ FIRST)

### SFQA Model (Priority Order)

Every decision must follow this priority:

1. **Security (S)** - HIGHEST PRIORITY
   - Never compromise security for features
   - Validate all external input
   - No secrets in code
   - Secure file operations
   
2. **Functionality (F)** - SECOND PRIORITY
   - Core features must work reliably
   - Focus on 3D printing workflow
   - STL editor, Maps to 3D, Image to 3D, Templates
   
3. **Quality (Q)** - THIRD PRIORITY
   - Clean, readable code
   - Well-tested
   - Good performance
   
4. **Additions (A)** - LOWEST PRIORITY
   - Nice-to-haves only after S, F, Q are solid
   - Examples: Donation system, extra export formats

### Hard Rules (Never Break These)

❌ **NO EMOJIS** - Anywhere in the UI or code comments  
✅ **GLASS MORPHISM ONLY** - For all UI design  
✅ **ACCOUNTLESS** - No user accounts or login required  
✅ **OFFLINE-FIRST** - Works without internet (except Maps feature)  
✅ **FREE** - Core features always free  
✅ **CONTEXT-FREE CODE** - Any LLM/human can continue without context  

---

## Architecture Quick Reference

See [ARCHITECTURE.md](ARCHITECTURE.md) for full details.

### Layer Structure

```
Desktop (UI) → Core (Logic) → Infrastructure (External)
```

- **Primple.Desktop**: WPF UI, ViewModels, Controls
- **Primple.Core**: Business logic, Services, Models
- **Primple.Infrastructure**: File I/O, APIs, 3D libraries

### Key Interfaces

```csharp
// STL operations
IStlService - Load, save, transform, validate, repair STL files

// Maps to 3D
IMapsService - Fetch map data, generate 2D/3D models

// Image to 3D
IImageService - Lithophane, relief, contour extrusion

// Project management
IProjectService - Save/load .FIG projects

// Templates
ITemplateService - Load and generate from templates

// Tool modules (extensibility)
IToolModule - Plugin interface for new tools
```

---

## Common Development Tasks

### Adding a New Feature

**Example**: Adding a new 3D editing tool

1. **Define Interface** (Core)
   ```csharp
   // Primple.Core/Services/IMyNewTool.cs
   public interface IMyNewTool
   {
       /// <summary>
       /// Does something useful for 3D printing.
       /// </summary>
       Task<Result<Mesh>> ProcessMesh(Mesh input, MyToolConfig config);
   }
   ```

2. **Implement Service** (Infrastructure or Core)
   ```csharp
   // Primple.Infrastructure/Services/MyNewTool.cs
   public class MyNewTool : IMyNewTool
   {
       public async Task<Result<Mesh>> ProcessMesh(Mesh input, MyToolConfig config)
       {
           // Validation (Security)
           if (input == null)
               return Result<Mesh>.Failure("Input mesh cannot be null");
               
           // Implementation (Functionality)
           var processed = await ProcessMeshInternal(input, config);
           
           return Result<Mesh>.Success(processed);
       }
   }
   ```

3. **Register Service** (Desktop/Startup)
   ```csharp
   // App.xaml.cs or ServiceConfiguration
   services.AddSingleton<IMyNewTool, MyNewTool>();
   ```

4. **Create ViewModel** (Desktop)
   ```csharp
   // Primple.Desktop/ViewModels/MyToolViewModel.cs
   public class MyToolViewModel : ObservableObject
   {
       private readonly IMyNewTool _tool;
       
       public ICommand ProcessCommand { get; }
       
       // Implementation with MVVM pattern
   }
   ```

5. **Create View** (Desktop)
   ```xml
   <!-- Primple.Desktop/Views/Tools/MyToolView.xaml -->
   <UserControl x:Class="Primple.Desktop.Views.Tools.MyToolView">
       <!-- Glass morphism styled UI -->
   </UserControl>
   ```

6. **Write Tests** (Tests)
   ```csharp
   // Primple.Tests/CoreTests/MyNewToolTests.cs
   public class MyNewToolTests
   {
       [Fact]
       public async Task ProcessMesh_ValidInput_ReturnsSuccess()
       {
           // Arrange
           var tool = new MyNewTool();
           var input = CreateTestMesh();
           
           // Act
           var result = await tool.ProcessMesh(input, new MyToolConfig());
           
           // Assert
           Assert.True(result.IsSuccess);
       }
   }
   ```

### Adding a Tool Module (Plugin)

```csharp
// 1. Create module class
public class MyToolModule : IToolModule
{
    public string Name => "My Awesome Tool";
    public string Description => "Does something amazing for 3D printing";
    public ToolCategory Category => ToolCategory.Editing;
    
    public void Initialize(IServiceProvider services)
    {
        // Get required services
    }
    
    public async Task<Result> Execute(ToolContext context)
    {
        // Tool logic here
    }
}

// 2. Module is auto-discovered - no registration needed!
```

### Implementing Glass Morphism UI

```xml
<!-- Glass Panel -->
<Border Style="{StaticResource GlassPanel}">
    <StackPanel Margin="20">
        <TextBlock Text="My Tool" 
                   FontSize="18" 
                   FontWeight="SemiBold"
                   Foreground="White"/>
        <!-- Content -->
    </StackPanel>
</Border>

<!-- Glass Button -->
<Button Style="{StaticResource GlassButton}" 
        Content="Process"
        Command="{Binding ProcessCommand}"/>
```

---

## File Format: .FIG Projects

### Structure

```json
{
  "version": "1.0.0",
  "metadata": {
    "created": "2025-12-15T00:00:00Z",
    "modified": "2025-12-15T00:00:00Z",
    "author": "Optional username"
  },
  "models": [
    {
      "id": "unique-guid",
      "name": "My Model",
      "type": "stl",
      "source": "imported|created|maps|image|template",
      "data": "embedded-base64-or-relative-path",
      "transform": {
        "position": [0, 0, 0],
        "rotation": [0, 0, 0],
        "scale": [1, 1, 1]
      },
      "metadata": {
        "originalFile": "path/to/original.stl"
      }
    }
  ],
  "settings": {
    "printBed": {
      "width": 220,
      "depth": 220,
      "height": 250
    },
    "units": "mm"
  }
}
```

### Implementation

```csharp
public class FigProject
{
    public string Version { get; set; } = "1.0.0";
    public ProjectMetadata Metadata { get; set; }
    public List<Model3D> Models { get; set; }
    public ProjectSettings Settings { get; set; }
}

// Serialization
var json = JsonSerializer.Serialize(project, new JsonSerializerOptions 
{ 
    WriteIndented = true 
});
File.WriteAllText("project.fig", json);

// Deserialization
var json = File.ReadAllText("project.fig");
var project = JsonSerializer.Deserialize<FigProject>(json);
```

---

## Testing Guidelines

### Test Structure

```
Primple.Tests/
├── CoreTests/              # Unit tests for Core logic
├── InfrastructureTests/    # Tests for I/O and external dependencies
└── IntegrationTests/       # End-to-end tests
```

### Writing Good Tests

```csharp
[Fact]
public async Task ServiceMethod_WhenCondition_ExpectedBehavior()
{
    // Arrange: Set up test data and dependencies
    var service = new MyService();
    var input = CreateTestData();
    
    // Act: Execute the method being tested
    var result = await service.MethodUnderTest(input);
    
    // Assert: Verify the expected outcome
    Assert.True(result.IsSuccess);
    Assert.Equal(expectedValue, result.Value.SomeProperty);
}
```

### Test Categories

- **Unit Tests**: Test single method/class in isolation
- **Integration Tests**: Test multiple components working together
- **Security Tests**: Test input validation, sanitization
- **Performance Tests**: Test with large models (10K+ triangles)

---

## Security Guidelines

### Input Validation Pattern

```csharp
public Result<T> ProcessUserInput(string input)
{
    // 1. Null/empty check
    if (string.IsNullOrWhiteSpace(input))
        return Result<T>.Failure("Input cannot be empty");
    
    // 2. Length validation
    if (input.Length > MAX_LENGTH)
        return Result<T>.Failure($"Input too long (max {MAX_LENGTH})");
    
    // 3. Format validation
    if (!IsValidFormat(input))
        return Result<T>.Failure("Invalid format");
    
    // 4. Sanitization
    var sanitized = SanitizeInput(input);
    
    // 5. Process
    var result = ProcessInternal(sanitized);
    
    return Result<T>.Success(result);
}
```

### File Path Sanitization

```csharp
public Result<string> ValidateFilePath(string userPath, string allowedDirectory)
{
    // 1. Get absolute path
    var fullPath = Path.GetFullPath(userPath);
    var allowedPath = Path.GetFullPath(allowedDirectory);
    
    // 2. Check path traversal
    if (!fullPath.StartsWith(allowedPath, StringComparison.OrdinalIgnoreCase))
        return Result<string>.Failure("Path outside allowed directory");
    
    // 3. Check file extension
    var extension = Path.GetExtension(fullPath).ToLowerInvariant();
    if (!AllowedExtensions.Contains(extension))
        return Result<string>.Failure($"File type {extension} not allowed");
    
    return Result<string>.Success(fullPath);
}
```

### Error Handling

```csharp
// ❌ WRONG - Exposes internal details
catch (Exception ex)
{
    MessageBox.Show($"Error: {ex.Message}\n{ex.StackTrace}");
}

// ✅ CORRECT - Logs details, shows friendly message
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to load STL file");
    MessageBox.Show("Failed to load file. Please check the file format.");
}
```

---

## Performance Guidelines

### Large Mesh Handling

```csharp
// For meshes with 100K+ triangles, use async and progress reporting
public async Task<Mesh> ProcessLargeMesh(
    Mesh input, 
    IProgress<int> progress, 
    CancellationToken cancellationToken)
{
    var totalTriangles = input.Triangles.Count;
    
    for (int i = 0; i < totalTriangles; i++)
    {
        // Check cancellation
        cancellationToken.ThrowIfCancellationRequested();
        
        // Process triangle
        ProcessTriangle(input.Triangles[i]);
        
        // Report progress every 1000 triangles
        if (i % 1000 == 0)
            progress?.Report((int)(i * 100.0 / totalTriangles));
    }
    
    return input;
}
```

### Memory Efficiency

```csharp
// Use streaming for large files
public async Task<Mesh> LoadLargeStl(string path)
{
    using var stream = File.OpenRead(path);
    using var reader = new StreamReader(stream);
    
    var mesh = new Mesh();
    
    while (!reader.EndOfStream)
    {
        var line = await reader.ReadLineAsync();
        ParseAndAddToMesh(line, mesh);
    }
    
    return mesh;
}
```

---

## Git Workflow

### Branch Naming

- `feature/stl-editor` - New feature
- `fix/normal-calculation` - Bug fix
- `docs/update-architecture` - Documentation
- `refactor/mesh-service` - Code refactoring

### Commit Messages

```
<type>: <subject>

<optional body>

<optional footer>
```

Types:
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation only
- `refactor`: Code restructuring
- `test`: Adding tests
- `perf`: Performance improvement
- `security`: Security fix

Example:
```
feat: Add lithophane generation to image service

Implemented height map conversion from grayscale images.
Supports configurable depth range and base thickness.

Closes #123
```

---

## Common Issues & Solutions

### Issue: "Can't build on Linux/Mac"

**Solution**: This is a Windows-only WPF application. Development requires Windows. Use Windows VM or WSL with Windows GUI support.

### Issue: "Missing Logo.png"

**Solution**: This is expected. See `Primple.Desktop/Assets/README.md`. App works without it.

### Issue: "GitVersion not calculating version"

**Solution**: Ensure you have commit history. First commit might show 1.0.0. Subsequent commits will increment.

### Issue: "Glass effect not showing"

**Solution**: 
1. Check `GlassPanel` style is defined in App.xaml or ResourceDictionary
2. Ensure Background has opacity < 1.0
3. Verify Effect (DropShadow) is applied

---

## Dependencies

### Current NuGet Packages

- **Microsoft.Extensions.DependencyInjection** (9.0.0) - DI container
- **Microsoft.Extensions.Hosting** (9.0.0) - Application hosting
- **xUnit** (2.9.2) - Unit testing

### Planned Additions

When implementing features, add these:

**For 3D Rendering**:
- HelixToolkit.Wpf or similar WPF 3D library
- System.Numerics.Vectors (built-in)

**For STL Processing**:
- geometry3Sharp (MIT license, good for mesh operations)
- Or custom STL parser

**For Image Processing**:
- SixLabors.ImageSharp (Apache 2.0)
- System.Drawing (built-in, but consider ImageSharp for cross-platform future)

**For Google Maps**:
- GoogleApi NuGet package (if available)
- Or custom HttpClient wrapper

**For UI**:
- CommunityToolkit.Mvvm (MVVM helpers)
- ModernWpfUI or custom controls for glass morphism

---

## Release Process

### Manual Release

```bash
# 1. Update version in GitVersion.yml if needed
# 2. Commit changes
git add .
git commit -m "feat: Implement XYZ feature"

# 3. Push to main
git push origin main

# 4. GitHub Actions automatically:
#    - Builds MSI installer
#    - Builds portable EXE
#    - Creates GitHub release
#    - Uploads artifacts
```

### Version Numbering

Follows **Semantic Versioning 2.0.0**:
- **Major** (X.0.0): Breaking changes
- **Minor** (0.X.0): New features (backward compatible)
- **Patch** (0.0.X): Bug fixes

Controlled by:
- Commit messages (`feat:` = minor, `fix:` = patch)
- GitVersion.yml configuration
- Manual override in GitVersion.yml `next-version`

---

## Roadmap Quick Reference

1. **Phase 1 - Foundation** ✅ (Current)
   - Project structure
   - Security & build system
   - Architecture documentation

2. **Phase 2 - Core Features** ⏳ (Next)
   - STL editor
   - 3D viewport
   - .FIG format
   - Basic UI

3. **Phase 3 - Advanced Tools** ⏳
   - Maps to 3D
   - Image to 3D
   - Templates
   - Full glass morphism UI

4. **Phase 4 - Quality** ⏳
   - Performance optimization
   - Error handling
   - UX improvements

5. **Phase 5 - Additions** ⏳
   - Donation system
   - Extra export formats
   - Plugin system

---

## Resources & References

- **Architecture**: See [ARCHITECTURE.md](ARCHITECTURE.md)
- **Security**: See [SECURITY.md](SECURITY.md)
- **Security Audit**: See [SECURITY_AUDIT_REPORT.md](SECURITY_AUDIT_REPORT.md)
- **.NET Docs**: https://docs.microsoft.com/dotnet/
- **WPF Guide**: https://docs.microsoft.com/dotnet/desktop/wpf/
- **STL Format**: https://en.wikipedia.org/wiki/STL_(file_format)

---

## FAQ

**Q: Can I use a different UI framework?**  
A: No. Requirements specify WPF desktop application with glass morphism.

**Q: Why no emojis?**  
A: Design requirement. Professional, clean interface. Text and icons only.

**Q: Can I add user accounts?**  
A: No. Application must be accountless. Optional donation system doesn't require accounts.

**Q: What if Google Maps API is too expensive?**  
A: Use free alternatives (OpenStreetMap + SRTM elevation data). Simplify if needed.

**Q: How do I know what to prioritize?**  
A: SFQA model. Security → Functionality → Quality → Additions. Always.

**Q: Can I use AI for image to 3D?**  
A: No. Requirements explicitly state "no AI, 2D image". Use height maps, tracing, extrusion.

**Q: What license is this?**  
A: See LICENSE.md. Source-available, free for personal/educational/internal business use.

---

**End of Development Guide**

_Start with ARCHITECTURE.md, then refer to this guide for practical development._
_Follow SFQA. No emojis. Glass morphism. Accountless. Context-free code._
