# Primple

**The Ultimate STL Maker and 3D Printing Tool**

> Free • Accountless • Offline-First • Windows Desktop

---

## What is Primple?

Primple is a modern, free desktop application for creating and editing STL files for 3D printing. Built with security and user privacy as top priorities, Primple offers powerful 3D modeling tools without requiring an account or internet connection (except for map features).

### Key Features

🔧 **Precision STL Editor**
- Edit STL files with exact measurements
- Mesh validation and repair tools
- Volume calculations for material estimation
- Slicing preview

🗺️ **Google Maps to 3D**
- Convert real-world locations into 3D printable models
- 2D map mode for laser engraving
- 3D terrain mode with elevation data
- Customizable colors and detail levels

🖼️ **Image to 3D Conversion**
- Create lithophanes from photos
- Relief and embossed designs
- Contour extrusion (no AI - pure algorithmic)

📐 **Template System**
- Quick-start templates for common prints
- Calibration models
- Functional parts (brackets, organizers, etc.)
- Educational shapes

💾 **Project Management**
- Save projects as .FIG files (JSON-based)
- Version control friendly
- No vendor lock-in

---

## Core Principles

### SFQA Development Model

1. **Security** - User privacy and data security first
2. **Functionality** - Core features that work reliably  
3. **Quality** - Clean, maintainable, well-tested code
4. **Additions** - Nice-to-haves after S, F, Q are solid

### Why Primple?

- ✅ **No Account Required** - Full functionality, zero sign-up
- ✅ **Free Forever** - Core features always free
- ✅ **Offline-First** - Works without internet (except Maps)
- ✅ **Privacy Focused** - No tracking, no telemetry
- ✅ **Modern UI** - Glass morphism design (no emojis!)
- ✅ **Open Development** - Source-available for transparency

---

## Installation

### Download

**Latest Release**: [Download from GitHub Releases](https://github.com/Daolyap/Primple/releases)

Choose your preferred format:
- **MSI Installer**: Standard installation with Start Menu shortcuts
- **Portable ZIP**: Single-file executable, no installation needed

> **Note**: Automatic releases are created on every push to the main branch. For development builds from CI runs, check the [Actions tab](https://github.com/Daolyap/Primple/actions) and download artifacts from the latest CI workflow run.

### Requirements

- Windows 10 or Windows 11
- .NET 10 Runtime (included in installer)
- 4GB RAM minimum (8GB+ recommended for large models)
- GPU with OpenGL support (for 3D viewport)

### Building from Source

```bash
# Prerequisites: Windows, .NET SDK 10.0+
git clone https://github.com/Daolyap/Primple.git
cd Primple
dotnet restore
dotnet build Primple.slnx --configuration Release
dotnet run --project Primple.Desktop/Primple.Desktop.csproj
```

See [DEVELOPMENT.md](DEVELOPMENT.md) for detailed build instructions.

---

## Quick Start

1. **Launch Primple**
2. **Import STL** or **Start from Template**
3. **Edit** with precision tools
4. **Export** ready-to-print STL
5. **Save Project** as .FIG for later

### Example Workflows

**Create a Lithophane from Photo**:
1. File → Image to 3D
2. Select your photo
3. Configure depth range
4. Export as STL
5. Print with white filament

**Generate 3D Map of Your City**:
1. Tools → Maps to 3D
2. Enter location or coordinates
3. Select area and detail level
4. Choose 2D or 3D mode
5. Customize colors
6. Export and print

---

## Documentation

- **[Architecture Guide](ARCHITECTURE.md)** - System design and structure
- **[Development Guide](DEVELOPMENT.md)** - For contributors and developers
- **[Security Policy](SECURITY.md)** - Security features and reporting
- **[Security Audit](SECURITY_AUDIT_REPORT.md)** - Latest security assessment

---

## Roadmap

### Current: Phase 1 - Foundation ✅
- [x] Project structure
- [x] Security framework
- [x] Build & release automation
- [x] Comprehensive documentation

### Next: Phase 2 - Core Features ⏳
- [ ] STL editor implementation
- [ ] 3D viewport with HelixToolkit
- [ ] .FIG project format
- [ ] File I/O (import/export STL)
- [ ] Basic glass morphism UI

### Future: Phase 3 - Advanced Tools ⏳
- [ ] Google Maps integration
- [ ] Image to 3D conversion
- [ ] Template library
- [ ] Full UI polish

### Later: Phase 4+ - Quality & Additions ⏳
- [ ] Performance optimization
- [ ] Additional export formats (OBJ, 3MF)
- [ ] Plugin system
- [ ] Optional donation system

---

## Contributing

Contributions are welcome! Please follow these guidelines:

1. **Read** [DEVELOPMENT.md](DEVELOPMENT.md) first
2. **Follow SFQA** priority model
3. **No emojis** in code or UI
4. **Glass morphism** design only
5. **Security first** - validate all inputs
6. **Write tests** for new features
7. **Document** thoroughly

### Development Workflow

```bash
# 1. Fork and clone
git clone https://github.com/YOUR_USERNAME/Primple.git

# 2. Create feature branch
git checkout -b feature/my-awesome-feature

# 3. Make changes, test, commit
git commit -m "feat: Add awesome feature"

# 4. Push and create PR
git push origin feature/my-awesome-feature
```

---

## Support

- **Issues**: [GitHub Issues](https://github.com/Daolyap/Primple/issues)
- **Security**: See [SECURITY.md](SECURITY.md) for responsible disclosure
- **Contact**: contact@daolyap.dev

---

## License

**Source-Available License** (Not Open Source)

This software is free for:
- ✅ Personal use
- ✅ Educational use
- ✅ Internal business use

You **may NOT**:
- ❌ Sell this software
- ❌ Host as paid SaaS
- ❌ Distribute in commercial products without permission

See [LICENSE.md](LICENSE.md) for full details.

For commercial licensing: contact@daolyap.dev

---

## Acknowledgments

Built with:
- .NET 10 and WPF
- Open source libraries (see dependencies)
- Passion for 3D printing and privacy

**No AI used** for image to 3D conversion (by design).

---

**Primple** - Printing Simple, but Powerful.

[![CI](https://github.com/Daolyap/Primple/workflows/CI/badge.svg)](https://github.com/Daolyap/Primple/actions)
[![License](https://img.shields.io/badge/license-Source--Available-blue.svg)](LICENSE.md)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)


