<div align="center">
  <img src="../Images/Logo2026.png" alt="WPF HexEditor" width="800"/>
</div>

---

# WPF HexEditor - Source Code

This directory contains all the source code for the WPF HexEditor project.

## 📁 Directory Structure

### Main Library
- **[WPFHexaEditor/](WPFHexaEditor/)** - Main hex editor control library
  - **[Services/](WPFHexaEditor/Services/)** - Business logic services (10 services)
  - **[Core/](WPFHexaEditor/Core/)** - Core infrastructure (ByteProvider, interfaces, converters)
  - **Dialog/** - Dialog windows (Find, Replace, etc.)
  - Project: `WpfHexEditorCore.csproj`

### Testing
- **[WPFHexaEditor.Tests/](WPFHexaEditor.Tests/)** - Unit test project
  - Framework: xUnit
  - 80+ tests for service layer
  - Project: `WPFHexaEditor.Tests.csproj`

### Sample Applications
- **[Samples/](Samples/)** - Demonstration applications
  - 8 sample projects showcasing different features
  - See [Samples/README.md](Samples/README.md) for details

### Tools
- **[Tools/](Tools/)** - Development and benchmarking tools
  - ByteProviderBench - Performance benchmarking with BenchmarkDotNet

## 🏗️ Project Architecture

```
Sources/
├── WPFHexaEditor/           # Main library (.NET 4.8 + .NET 8.0)
│   ├── Services/            # 10 business logic services
│   ├── Core/                # Core infrastructure
│   ├── Dialog/              # UI dialogs
│   └── Controls/            # UI controls
├── WPFHexaEditor.Tests/     # xUnit test project
├── Samples/                 # 8 sample applications
└── Tools/                   # Development tools
```

## 🎯 Supported Frameworks

- **.NET Framework 4.8**
- **.NET 8.0-windows**

## 🚀 Building the Solution

### Build all projects:
```bash
dotnet build WpfHexEditorControl.sln --configuration Release
```

### Build main library only:
```bash
dotnet build WPFHexaEditor/WpfHexEditorCore.csproj
```

### Run tests:
```bash
dotnet test WPFHexaEditor.Tests/WPFHexaEditor.Tests.csproj
```

## 📚 Documentation

- **[Main README](../README.md)** - Project overview and features
- **[Architecture Guide](ARCHITECTURE.md)** - Detailed architecture documentation
- **[Services Documentation](WPFHexaEditor/Services/README.md)** - Service layer details
- **[Core Components](WPFHexaEditor/Core/README.md)** - Core infrastructure
- **[Samples Guide](Samples/README.md)** - Sample applications

## 🔗 Quick Links

- **NuGet Package**: [WPFHexaEditor](https://www.nuget.org/packages/WPFHexaEditor/)
- **GitHub Repository**: [WpfHexEditorControl](https://github.com/abbaye/WpfHexEditorControl)
- **License**: Apache 2.0

## 📝 Notes

- The solution file is located at the root: `WpfHexEditorControl.sln`
- All projects use SDK-style project format
- Multi-targeting support for .NET Framework 4.8 and .NET 8.0-windows
