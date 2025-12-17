# Primple Architecture Documentation

**Version**: 1.0.0  
**Last Updated**: 2025-12-15  
**Purpose**: Ultimate STL maker and 3D printing tool for Windows

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Core Principles](#core-principles)
3. [Architecture Overview](#architecture-overview)
4. [Module System](#module-system)
5. [Technology Stack](#technology-stack)
6. [Project Structure](#project-structure)
7. [Core Features](#core-features)
8. [Development Guidelines](#development-guidelines)
9. [Future Development](#future-development)

---

## Project Overview

### What is Primple?

Primple is a **free, accountless, desktop STL maker** and 3D printing platform designed for:
- Creating and editing STL files with precision
- Converting Google Maps areas into 3D printable models
- Converting 2D images to 3D models
- Professional 3D printing workflow tools
- Template-based rapid prototyping

### Key Differentiators

- **No Account Required**: Full functionality without sign-up
- **Offline-First**: Works completely offline (except Google Maps feature)
- **Free Forever**: Optional donation system (Addition tier)
- **3D Printer Focused**: Specialized tools for 3D printing community
- **Modern UI**: Glass morphism design language

---

## Core Principles

### SFQA Development Model

All development follows this strict priority order:

1. **Security (S)**: Security comes first, always
   - No user data collection without explicit consent
   - Secure file operations
   - Input validation on all external data
   - No tracking, no telemetry by default

2. **Functionality (F)**: Core features that work reliably
   - STL editor with precision tools
   - Google Maps to 3D conversion
   - Image to 3D conversion
   - Project save/load (.FIG format)

3. **Quality (Q)**: Well-tested, maintainable code
   - Clear, self-documenting code
   - Comprehensive error handling
   - Performance optimization
   - LLM/human readable without context

4. **Additions (A)**: Nice-to-have features
   - Donation system
   - Advanced templates
   - Export to other formats
   - Plugins/extensions

### Code Clarity Principle

> **"Any LLM or human must be able to pick up development without any prior context"**

This means:
- ✅ Self-documenting code with clear variable/method names
- ✅ XML documentation comments on all public APIs
- ✅ Architecture documentation (this file)
- ✅ Module documentation in each module
- ✅ Development guidelines clearly stated
- ✅ No implicit assumptions or "tribal knowledge"

---

## Architecture Overview

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   Primple.Desktop (WPF)                 │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────┐ │
│  │  Glass UI   │  │  3D Viewport │  │  Tool Panels  │ │
│  └─────────────┘  └──────────────┘  └───────────────┘ │
└─────────────────────────────────────────────────────────┘
                          ▼
┌─────────────────────────────────────────────────────────┐
│              Primple.Core (Business Logic)              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │ STL Service  │  │ Maps Service │  │ Image Service│ │
│  ├──────────────┤  ├──────────────┤  ├──────────────┤ │
│  │ FIG Projects │  │ Templates    │  │ 3D Rendering │ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────┘
                          ▼
┌─────────────────────────────────────────────────────────┐
│          Primple.Infrastructure (I/O & External)        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │ File I/O     │  │ Google Maps  │  │ 3D Libraries │ │
│  │ STL Parser   │  │ API Client   │  │ (HelixToolkit│ │
│  └──────────────┘  └──────────────┘  └──────────────┘ │
└─────────────────────────────────────────────────────────┘
```

### Layer Responsibilities

**Primple.Desktop** (Presentation Layer)
- WPF UI with glass morphism design
- 3D viewport rendering
- User interaction handling
- MVVM pattern implementation

**Primple.Core** (Business Logic Layer)
- Core algorithms (STL manipulation, 3D conversions)
- Business rules and validation
- Domain models
- Service interfaces

**Primple.Infrastructure** (Data/External Layer)
- File I/O operations
- External API integrations (Google Maps)
- 3D library integrations
- Persistence (.FIG format)

---

## Module System

### Plugin Architecture

Primple uses a **modular plugin system** for extensibility:

```csharp
public interface IToolModule
{
    string Name { get; }
    string Description { get; }
    ToolCategory Category { get; }
    
    void Initialize(IServiceProvider services);
    Task<Result> Execute(ToolContext context);
}
```

### Core Modules

1. **STL Editor Module**
   - Precision editing tools
   - Vertex/edge/face manipulation
   - Measurement tools
   - Export/import

2. **Maps to 3D Module**
   - Google Maps integration
   - 2D map generation (base plane + details)
   - 3D terrain generation
   - Configurable colors

3. **Image to 3D Module**
   - 2D image processing (no AI)
   - Height map generation
   - Lithophane creation
   - Depth extrusion

4. **Template Module**
   - Pre-built 3D printer templates
   - Customizable parameters
   - Category system (functional, artistic, educational)

5. **Project Module**
   - .FIG format save/load
   - Project metadata
   - Asset management
   - Version control friendly format (JSON-based)

### Adding New Modules

To add a new tool module:

1. Create class implementing `IToolModule`
2. Add to `Primple.Core/Modules/` directory
3. Register in `ModuleRegistry` (auto-discovery)
4. Add UI panel to `Primple.Desktop/Views/Tools/`

No core code changes required - **plug and play!**

---

## Technology Stack

### Frontend (Desktop)
- **Framework**: WPF (.NET 10)
- **3D Rendering**: HelixToolkit.Wpf (or similar)
- **UI Design**: Glass morphism (custom controls)
- **Architecture**: MVVM with CommunityToolkit.Mvvm

### Backend (Core Logic)
- **.NET**: 10.0 (latest LTS)
- **Language**: C# 13
- **STL Processing**: Custom parser + library (e.g., g3sharp)
- **3D Math**: System.Numerics.Vectors

### Infrastructure
- **File Format**: Custom .FIG (JSON-based)
- **Maps API**: Google Maps Platform API
- **Image Processing**: System.Drawing / ImageSharp
- **HTTP Client**: HttpClient with Polly (resilience)

### Development Tools
- **Version Control**: Git + GitHub
- **CI/CD**: GitHub Actions
- **Package Manager**: NuGet
- **Code Analysis**: Roslyn analyzers + SonarLint
- **Testing**: xUnit + FluentAssertions

---

## Project Structure

```
Primple/
├── Primple.Desktop/              # WPF UI Application
│   ├── Views/                    # XAML views
│   │   ├── MainWindow.xaml       # Main application window
│   │   ├── Tools/                # Tool-specific views
│   │   └── Dialogs/              # Dialog windows
│   ├── ViewModels/               # View models (MVVM)
│   ├── Controls/                 # Custom UI controls
│   │   └── GlassMorphism/        # Glass effect controls
│   ├── Converters/               # Value converters
│   ├── Behaviors/                # Attached behaviors
│   └── Assets/                   # Images, icons, resources
│
├── Primple.Core/                 # Business logic
│   ├── Models/                   # Domain models
│   │   ├── Mesh.cs               # 3D mesh representation
│   │   ├── STLModel.cs           # STL-specific model
│   │   ├── FigProject.cs         # Project file structure
│   │   └── Template.cs           # Template model
│   ├── Services/                 # Core services
│   │   ├── IStlService.cs        # STL operations
│   │   ├── IMapsService.cs       # Maps to 3D
│   │   ├── IImageService.cs      # Image to 3D
│   │   ├── IProjectService.cs    # Project management
│   │   └── ITemplateService.cs   # Template system
│   ├── Modules/                  # Tool modules
│   │   ├── IToolModule.cs        # Module interface
│   │   ├── StlEditorModule.cs    # STL editor
│   │   ├── MapsModule.cs         # Maps tool
│   │   └── ImageModule.cs        # Image tool
│   └── Common/                   # Shared utilities
│       ├── Result.cs             # Result pattern
│       ├── Extensions/           # Extension methods
│       └── Helpers/              # Helper classes
│
├── Primple.Infrastructure/       # External integrations
│   ├── FileSystem/               # File I/O
│   │   ├── StlParser.cs          # STL file parser
│   │   ├── FigSerializer.cs      # .FIG serialization
│   │   └── FileService.cs        # Generic file operations
│   ├── GoogleMaps/               # Maps integration
│   │   ├── MapsApiClient.cs      # API client
│   │   ├── TerrainGenerator.cs   # 3D terrain from maps
│   │   └── MapRenderer.cs        # 2D map rendering
│   ├── ImageProcessing/          # Image to 3D
│   │   ├── HeightMapGenerator.cs # Height map creation
│   │   └── MeshGenerator.cs      # Mesh from image
│   └── ThreeD/                   # 3D library integration
│       ├── MeshProcessor.cs      # Mesh operations
│       └── Renderer.cs           # 3D rendering
│
├── Primple.Tests/                # Unit tests
│   ├── CoreTests/                # Core logic tests
│   ├── InfrastructureTests/      # Infrastructure tests
│   └── IntegrationTests/         # Integration tests
│
├── .github/workflows/            # CI/CD
│   ├── ci.yml                    # Build and test
│   └── release.yml               # Release builds
│
├── docs/                         # Documentation
│   ├── ARCHITECTURE.md           # This file
│   ├── DEVELOPMENT.md            # Development guide
│   ├── API.md                    # API documentation
│   └── USER_GUIDE.md             # User documentation
│
├── GitVersion.yml                # Versioning config
├── Directory.Build.props         # Common build props
└── Primple.slnx                  # Solution file
```

---

## Core Features

### 1. STL Editor (F-Tier: Functionality)

**Purpose**: Precision editing of STL files for 3D printing

**Key Tools**:
- **Precision Movement**: Move vertices/faces by exact measurements
- **Scaling**: Uniform and non-uniform scaling with dimensions
- **Rotation**: Precise rotation with angle input
- **Measurement**: Measure distances, angles, volumes
- **Repair**: Fix non-manifold geometry, holes, inverted normals
- **Slicing Preview**: Preview how model will print (layer view)

**Implementation**:
```csharp
public interface IStlService
{
    Task<Mesh> LoadStl(string filePath);
    Task SaveStl(Mesh mesh, string filePath);
    Mesh Transform(Mesh mesh, Matrix4x4 transformation);
    ValidationResult Validate(Mesh mesh);
    Mesh Repair(Mesh mesh);
}
```

### 2. Google Maps to 3D (F-Tier: Functionality)

**Purpose**: Convert real-world locations into 3D printable models

**Modes**:
1. **2D Map Mode**
   - Base plane (one color)
   - Roads, buildings, landmarks (contrasting color)
   - Flat, suitable for laser engraving or two-color printing

2. **3D Terrain Mode**
   - Real elevation data from Google Maps
   - Configurable vertical exaggeration
   - Buildings in 3D (if available)
   - Water bodies handled appropriately

**Configuration**:
- Color selection for each element type
- Height scaling factor
- Detail level (resolution)
- Area selection (bounding box)

**Implementation**:
```csharp
public interface IMapsService
{
    Task<MapData> FetchMapData(GeoCoordinates center, double radiusKm);
    Mesh Generate2DMap(MapData data, MapConfig config);
    Mesh Generate3DMap(MapData data, MapConfig config);
}
```

**Fallback**: If Google Maps API is unavailable or too complex:
- Use OpenStreetMap data
- Simplified elevation from SRTM data
- Focus on 2D mode first, 3D as enhancement

### 3. Image to 3D (F-Tier: Functionality)

**Purpose**: Convert 2D images into 3D printable models (NO AI)

**Conversion Types**:
1. **Lithophane**: Brightness to depth mapping
2. **Relief**: Create raised/embossed version
3. **Extrusion**: Trace contours and extrude

**Parameters**:
- Depth range (min/max thickness)
- Base thickness
- Smoothing level
- Inversion (negative lithophane)

**Implementation**:
```csharp
public interface IImageService
{
    Mesh CreateLithophane(Image image, LithophaneConfig config);
    Mesh CreateRelief(Image image, ReliefConfig config);
    Mesh ExtrudeContours(Image image, ExtrusionConfig config);
}
```

### 4. Templates (F-Tier: Functionality)

**Purpose**: Quick-start templates for common 3D printing needs

**Categories**:
- **Functional**: Brackets, enclosures, organizers
- **Calibration**: Test prints, calibration cubes
- **Replacement Parts**: Common parts (knobs, clips)
- **Educational**: Geometric shapes, puzzles
- **Artistic**: Bases, frames, decorative

**Template System**:
```csharp
public class Template
{
    public string Name { get; set; }
    public TemplateCategory Category { get; set; }
    public List<Parameter> Parameters { get; set; }
    public Func<ParameterValues, Mesh> Generator { get; set; }
}
```

### 5. .FIG Project Format (F-Tier: Functionality)

**Purpose**: Save and load projects with all settings

**Format**: JSON-based for version control friendliness

**Structure**:
```json
{
  "version": "1.0.0",
  "metadata": {
    "created": "2025-12-15T00:00:00Z",
    "modified": "2025-12-15T00:00:00Z",
    "author": "Optional"
  },
  "models": [
    {
      "id": "uuid",
      "name": "Model 1",
      "type": "stl",
      "data": "embedded or path",
      "transform": { "position": [...], "rotation": [...], "scale": [...] }
    }
  ],
  "settings": {
    "printBed": { "width": 220, "depth": 220, "height": 250 },
    "units": "mm"
  }
}
```

---

## Development Guidelines

### Code Style

1. **Naming Conventions**
   - PascalCase: Classes, methods, properties, public fields
   - camelCase: Local variables, parameters, private fields
   - _camelCase: Private instance fields (with underscore)
   - Descriptive names over abbreviations

2. **Documentation**
   ```csharp
   /// <summary>
   /// Converts a 2D image into a 3D lithophane mesh.
   /// </summary>
   /// <param name="image">Source image for conversion</param>
   /// <param name="config">Configuration for depth mapping and sizing</param>
   /// <returns>A mesh suitable for 3D printing as a lithophane</returns>
   /// <exception cref="ArgumentNullException">If image or config is null</exception>
   public Mesh CreateLithophane(Image image, LithophaneConfig config)
   {
       // Implementation
   }
   ```

3. **Error Handling**
   - Use Result pattern for expected failures
   - Exceptions only for exceptional situations
   - Always log errors with context
   - Never expose internal details to users

4. **Async/Await**
   - All I/O operations must be async
   - Use `ConfigureAwait(false)` in library code
   - Provide cancellation token support for long operations

### Testing

1. **Unit Tests**: All business logic
2. **Integration Tests**: File I/O, API calls
3. **UI Tests**: Critical user flows (optional)

### Security Checklist

Before committing:
- [ ] No hardcoded credentials or API keys
- [ ] All user input validated
- [ ] File paths sanitized (no path traversal)
- [ ] External data validated before processing
- [ ] Error messages don't leak internal details
- [ ] Sensitive data not logged

### Git Commit Messages

Follow conventional commits:
```
feat: Add lithophane generation to image service
fix: Correct STL normal calculation for inverted faces
docs: Update architecture with plugin system
refactor: Extract mesh validation into separate service
test: Add integration tests for .FIG serialization
```

---

## Future Development

### Phase 1: Foundation (Current)
- ✅ Project structure
- ✅ Security foundation
- ✅ Build system (GitHub Actions)
- ⏳ Architecture documentation

### Phase 2: Core Features
- ⏳ STL editor implementation
- ⏳ Basic 3D viewport
- ⏳ .FIG project format
- ⏳ File I/O (STL import/export)

### Phase 3: Advanced Tools
- ⏳ Google Maps integration
- ⏳ Image to 3D conversion
- ⏳ Template system
- ⏳ Glass morphism UI

### Phase 4: Quality & Polish
- ⏳ Performance optimization
- ⏳ Error handling refinement
- ⏳ User experience improvements
- ⏳ Comprehensive testing

### Phase 5: Additions
- ⏳ Donation system
- ⏳ Export to other formats (OBJ, 3MF)
- ⏳ Plugin system for community modules
- ⏳ Preferences/settings system

---

## Design System: Glass Morphism

### Visual Language

**No Emojis**: Text and icons only

**Glass Effect Characteristics**:
- Semi-transparent backgrounds (80-95% opacity)
- Backdrop blur (10-20px)
- Subtle border (1px, 10-20% white)
- Soft shadows
- Smooth animations

**Color Palette**:
- Primary: #2D2D2D (Dark base)
- Secondary: #FFFFFF (White glass)
- Accent: #007ACC (Blue - for actions)
- Success: #4CAF50
- Warning: #FF9800
- Error: #F44336

**Typography**:
- Font: Segoe UI (Windows standard)
- Sizes: 10pt (small), 12pt (body), 14pt (heading), 18pt (title)

### WPF Implementation

```xml
<Style x:Key="GlassPanel" TargetType="Border">
    <Setter Property="Background">
        <Setter.Value>
            <SolidColorBrush Color="White" Opacity="0.1"/>
        </Setter.Value>
    </Setter>
    <Setter Property="BorderBrush" Value="#33FFFFFF"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="10"/>
    <Setter Property="Effect">
        <Setter.Value>
            <DropShadowEffect BlurRadius="20" ShadowDepth="0" 
                             Opacity="0.3" Color="Black"/>
        </Setter.Value>
    </Setter>
</Style>
```

---

## Questions for Future Developers

**"I don't understand X"** → Check this file first
**"How do I add a new tool?"** → See Module System section
**"What's the priority?"** → SFQA model (Security → Functionality → Quality → Additions)
**"Can I use emoji?"** → NO, never
**"Where's the UI design guide?"** → See Design System section
**"How do I test?"** → See Development Guidelines → Testing

---

**End of Architecture Documentation**

_This document is a living guide. Update it when architecture changes._
_Future you (or future LLM) will thank you._
