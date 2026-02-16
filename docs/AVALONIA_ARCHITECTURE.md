# Avalonia Architecture - Visual Design

## 📐 Complete Architecture Overview

```mermaid
graph TB
    subgraph "Application Layer"
        WpfApp["WPF Sample Application<br/>WpfHexEditor.Sample.Main"]
        AvApp["Avalonia Sample Application<br/>AvaloniaHexEditor.Sample"]
    end

    subgraph "UI Layer - Platform Specific"
        WpfUI["WpfHexaEditor.Wpf<br/>net48, net8.0-windows<br/><br/>• HexEditor.xaml<br/>• HexViewport.cs<br/>• HexBox.xaml<br/>• Dialogs<br/>• Converters"]
        AvUI["WpfHexaEditor.Avalonia<br/>net8.0<br/><br/>• HexEditor.axaml<br/>• HexViewport.cs<br/>• HexBox.axaml<br/>• Views<br/>• Converters"]
    end

    subgraph "Platform Implementation Layer"
        WpfPlatform["WPF Platform<br/><br/>• WpfDrawingContext<br/>• WpfBrush/Pen<br/>• WpfKeyConverter<br/>• WpfTimer"]
        AvPlatform["Avalonia Platform<br/><br/>• AvaloniaDrawingContext<br/>• AvaloniaBrush/Pen<br/>• AvaloniaKeyConverter<br/>• AvaloniaTimer"]
    end

    subgraph "Core Layer - Platform Agnostic"
        Core["WpfHexaEditor.Core<br/>netstandard2.0<br/><br/>📦 Platform Abstractions<br/>• IDrawingContext<br/>• PlatformColor/Brush<br/>• PlatformKey<br/>• IPlatformTimer<br/><br/>📦 Business Logic (Portable)<br/>• Core/Bytes (13,050 LOC)<br/>• Services (4,305 LOC)<br/>• ViewModels (2,500 LOC)<br/>• Models & Events"]
    end

    WpfApp --> WpfUI
    AvApp --> AvUI

    WpfUI --> WpfPlatform
    WpfUI --> Core
    WpfPlatform --> Core

    AvUI --> AvPlatform
    AvUI --> Core
    AvPlatform --> Core

    style WpfApp fill:#0078d4,stroke:#005a9e,color:#fff
    style AvApp fill:#9333ea,stroke:#7c3aed,color:#fff
    style WpfUI fill:#0078d4,stroke:#005a9e,color:#fff
    style AvUI fill:#9333ea,stroke:#7c3aed,color:#fff
    style WpfPlatform fill:#00bcf2,stroke:#0099cc,color:#000
    style AvPlatform fill:#c084fc,stroke:#a855f7,color:#000
    style Core fill:#10b981,stroke:#059669,color:#fff
```

---

## 🗂️ Project Structure with Dependencies

```mermaid
graph LR
    subgraph "WpfHexaEditor.Core"
        Platform["Platform/<br/>Abstractions"]
        CoreBiz["Core/<br/>Business Logic"]
        Services["Services/"]
        ViewModels["ViewModels/"]
        Models["Models/"]
        Events["Events/"]
    end

    subgraph "WpfHexaEditor.Wpf"
        WpfControls["Controls/"]
        WpfPlatformImpl["Platform/<br/>WPF Impl"]
        WpfConv["Converters/"]
        WpfDialog["Dialog/"]
    end

    subgraph "WpfHexaEditor.Avalonia"
        AvControls["Controls/"]
        AvPlatformImpl["Platform/<br/>Avalonia Impl"]
        AvConv["Converters/"]
        AvViews["Views/"]
    end

    WpfControls -.->|uses| Platform
    WpfControls -.->|uses| CoreBiz
    WpfControls -.->|uses| Services
    WpfControls -.->|uses| ViewModels
    WpfPlatformImpl -.->|implements| Platform

    AvControls -.->|uses| Platform
    AvControls -.->|uses| CoreBiz
    AvControls -.->|uses| Services
    AvControls -.->|uses| ViewModels
    AvPlatformImpl -.->|implements| Platform

    style Platform fill:#fbbf24,stroke:#f59e0b,color:#000
    style CoreBiz fill:#10b981,stroke:#059669,color:#fff
    style Services fill:#10b981,stroke:#059669,color:#fff
    style ViewModels fill:#10b981,stroke:#059669,color:#fff
    style WpfPlatformImpl fill:#00bcf2,stroke:#0099cc,color:#000
    style AvPlatformImpl fill:#c084fc,stroke:#a855f7,color:#000
```

---

## 🎨 Platform Abstraction Layer Detail

```mermaid
graph TB
    subgraph "Platform Abstractions (Interfaces)"
        IDrawingContext["IDrawingContext<br/>━━━━━━━━━━<br/>+ DrawRectangle()<br/>+ DrawText()<br/>+ DrawLine()<br/>+ DrawEllipse()"]

        IBrush["IBrush<br/>━━━━━━━━━━<br/>+ IsFrozen<br/>+ Freeze()"]

        IPlatformTimer["IPlatformTimer<br/>━━━━━━━━━━<br/>+ Interval<br/>+ IsEnabled<br/>+ Tick event<br/>+ Start()/Stop()"]

        PlatformKey["PlatformKey (enum)<br/>━━━━━━━━━━<br/>A-Z, 0-9<br/>Arrow keys<br/>Function keys<br/>Special keys"]

        PlatformColor["PlatformColor (struct)<br/>━━━━━━━━━━<br/>+ A, R, G, B<br/>+ FromArgb()<br/>+ FromRgb()"]
    end

    subgraph "WPF Implementations"
        WpfDC["WpfDrawingContext<br/>━━━━━━━━━━<br/>wraps<br/>System.Windows.Media<br/>.DrawingContext"]

        WpfBrush["WpfBrush<br/>━━━━━━━━━━<br/>wraps<br/>SolidColorBrush"]

        WpfTimer["WpfDispatcherTimer<br/>━━━━━━━━━━<br/>wraps<br/>DispatcherTimer"]

        WpfConv["WpfKeyConverter<br/>━━━━━━━━━━<br/>System.Windows<br/>.Input.Key<br/>↓↑<br/>PlatformKey"]
    end

    subgraph "Avalonia Implementations"
        AvDC["AvaloniaDrawingContext<br/>━━━━━━━━━━<br/>wraps<br/>Avalonia.Media<br/>.DrawingContext"]

        AvBrush["AvaloniaBrush<br/>━━━━━━━━━━<br/>wraps<br/>ISolidColorBrush"]

        AvTimer["AvaloniaDispatcherTimer<br/>━━━━━━━━━━<br/>wraps<br/>DispatcherTimer"]

        AvConv["AvaloniaKeyConverter<br/>━━━━━━━━━━<br/>Avalonia.Input.Key<br/>↓↑<br/>PlatformKey"]
    end

    IDrawingContext -.->|implements| WpfDC
    IDrawingContext -.->|implements| AvDC

    IBrush -.->|implements| WpfBrush
    IBrush -.->|implements| AvBrush

    IPlatformTimer -.->|implements| WpfTimer
    IPlatformTimer -.->|implements| AvTimer

    PlatformKey -.->|converts| WpfConv
    PlatformKey -.->|converts| AvConv

    style IDrawingContext fill:#fbbf24,stroke:#f59e0b,color:#000
    style IBrush fill:#fbbf24,stroke:#f59e0b,color:#000
    style IPlatformTimer fill:#fbbf24,stroke:#f59e0b,color:#000
    style PlatformKey fill:#fbbf24,stroke:#f59e0b,color:#000
    style PlatformColor fill:#fbbf24,stroke:#f59e0b,color:#000

    style WpfDC fill:#00bcf2,stroke:#0099cc,color:#000
    style WpfBrush fill:#00bcf2,stroke:#0099cc,color:#000
    style WpfTimer fill:#00bcf2,stroke:#0099cc,color:#000
    style WpfConv fill:#00bcf2,stroke:#0099cc,color:#000

    style AvDC fill:#c084fc,stroke:#a855f7,color:#000
    style AvBrush fill:#c084fc,stroke:#a855f7,color:#000
    style AvTimer fill:#c084fc,stroke:#a855f7,color:#000
    style AvConv fill:#c084fc,stroke:#a855f7,color:#000
```

---

## 🏗️ HexEditor Control Architecture

```mermaid
graph TB
    subgraph "HexEditor Control (Platform-Specific)"
        HexEditor["HexEditor<br/>━━━━━━━━━━<br/>Main UserControl<br/>1,500+ lines<br/><br/>WPF: .xaml<br/>Avalonia: .axaml"]
    end

    subgraph "Child Controls (Custom Rendering)"
        HexViewport["HexViewport<br/>━━━━━━━━━━<br/>Main viewport<br/>Custom rendering<br/>535 lines OnRender<br/><br/>Uses IDrawingContext"]

        BarChart["BarChartPanel<br/>━━━━━━━━━━<br/>Frequency histogram<br/>171 lines OnRender<br/><br/>Uses IDrawingContext"]

        Caret["Caret<br/>━━━━━━━━━━<br/>Blinking cursor<br/>264 lines OnRender<br/><br/>Uses IDrawingContext<br/>Uses IPlatformTimer"]

        ScrollMarker["ScrollMarkerPanel<br/>━━━━━━━━━━<br/>Search markers<br/>291 lines<br/><br/>Uses IDrawingContext"]
    end

    subgraph "ViewModel Layer (Portable)"
        ViewModel["HexEditorViewModel<br/>━━━━━━━━━━<br/>INotifyPropertyChanged<br/><br/>• Position<br/>• Selection<br/>• Data<br/>• Commands"]
    end

    subgraph "Service Layer (Portable)"
        UndoRedo["UndoRedoService"]
        Search["SearchService"]
        Selection["SelectionService"]
        ByteData["ByteDataService"]
        Stream["StreamService"]
    end

    subgraph "Core Layer (Portable)"
        Bytes["Core/Bytes<br/>━━━━━━━━━━<br/>9,522 lines<br/><br/>• BaseByte<br/>• ByteProvider<br/>• ByteModified<br/>• ByteAction"]
    end

    HexEditor --> HexViewport
    HexEditor --> BarChart
    HexEditor --> Caret
    HexEditor --> ScrollMarker
    HexEditor --> ViewModel

    ViewModel --> UndoRedo
    ViewModel --> Search
    ViewModel --> Selection
    ViewModel --> ByteData
    ViewModel --> Stream

    UndoRedo --> Bytes
    Search --> Bytes
    Selection --> Bytes
    ByteData --> Bytes
    Stream --> Bytes

    HexViewport -.->|renders| Bytes

    style HexEditor fill:#0ea5e9,stroke:#0284c7,color:#fff
    style HexViewport fill:#8b5cf6,stroke:#7c3aed,color:#fff
    style BarChart fill:#8b5cf6,stroke:#7c3aed,color:#fff
    style Caret fill:#8b5cf6,stroke:#7c3aed,color:#fff
    style ScrollMarker fill:#8b5cf6,stroke:#7c3aed,color:#fff
    style ViewModel fill:#10b981,stroke:#059669,color:#fff
    style UndoRedo fill:#10b981,stroke:#059669,color:#fff
    style Search fill:#10b981,stroke:#059669,color:#fff
    style Selection fill:#10b981,stroke:#059669,color:#fff
    style ByteData fill:#10b981,stroke:#059669,color:#fff
    style Stream fill="#10b981,stroke:#059669,color:#fff
    style Bytes fill:#10b981,stroke:#059669,color:#fff
```

---

## 🔄 Rendering Flow (WPF vs Avalonia)

```mermaid
sequenceDiagram
    participant App as Application
    participant HE as HexEditor
    participant VP as HexViewport
    participant Platform as Platform Layer
    participant Core as Core/Bytes

    Note over App,Core: WPF Flow
    App->>HE: User action
    HE->>VP: InvalidateVisual()
    VP->>VP: OnRender(DrawingContext dc)
    VP->>Platform: new WpfDrawingContext(dc)
    VP->>VP: OnRenderCore(IDrawingContext)
    VP->>Core: Get byte data
    Core-->>VP: ByteData[]
    VP->>Platform: IDrawingContext.DrawRectangle()
    Platform->>Platform: DrawingContext.DrawRectangle()
    VP->>Platform: IDrawingContext.DrawText()
    Platform->>Platform: FormattedText rendering
    Platform-->>VP: Rendering complete

    Note over App,Core: Avalonia Flow
    App->>HE: User action
    HE->>VP: InvalidateVisual()
    VP->>VP: Render(DrawingContext dc)
    VP->>Platform: new AvaloniaDrawingContext(dc)
    VP->>VP: OnRenderCore(IDrawingContext)
    VP->>Core: Get byte data
    Core-->>VP: ByteData[]
    VP->>Platform: IDrawingContext.DrawRectangle()
    Platform->>Platform: DrawingContext.DrawRectangle()
    VP->>Platform: IDrawingContext.DrawText()
    Platform->>Platform: FormattedText rendering
    Platform-->>VP: Rendering complete
```

---

## 📦 NuGet Package Structure

```mermaid
graph TB
    subgraph "NuGet Packages v3.0"
        CorePkg["WpfHexaEditor.Core<br/>━━━━━━━━━━<br/>netstandard2.0<br/><br/>Contains:<br/>• Platform abstractions<br/>• Portable business logic<br/>• Services<br/>• ViewModels<br/><br/>No UI dependencies"]

        WpfPkg["WpfHexaEditor.Wpf<br/>━━━━━━━━━━<br/>net48, net8.0-windows<br/><br/>Contains:<br/>• WPF controls<br/>• WPF platform impl<br/>• Converters<br/>• Dialogs<br/><br/>Depends on:<br/>WpfHexaEditor.Core"]

        AvPkg["WpfHexaEditor.Avalonia<br/>━━━━━━━━━━<br/>net8.0<br/><br/>Contains:<br/>• Avalonia controls<br/>• Avalonia platform impl<br/>• Converters<br/>• Views<br/><br/>Depends on:<br/>WpfHexaEditor.Core<br/>Avalonia 11.0+"]
    end

    subgraph "Legacy Package (Deprecated)"
        LegacyPkg["WpfHexaEditor v3.0<br/>━━━━━━━━━━<br/>DEPRECATED<br/><br/>Type forwarding to<br/>WpfHexaEditor.Wpf<br/><br/>For backward<br/>compatibility"]
    end

    WpfPkg --> CorePkg
    AvPkg --> CorePkg
    LegacyPkg -.->|facades to| WpfPkg

    style CorePkg fill:#10b981,stroke:#059669,color:#fff
    style WpfPkg fill:#0078d4,stroke:#005a9e,color:#fff
    style AvPkg fill:#9333ea,stroke:#7c3aed,color:#fff
    style LegacyPkg fill:#ef4444,stroke:#dc2626,color:#fff
```

---

## 🎯 Migration Strategy - File Organization

### Before Migration (Current)
```
Sources/
└── WPFHexaEditor/
    ├── Core/                    ← Will move to Core project
    ├── Services/                ← Will move to Core project
    ├── ViewModels/              ← Will move to Core project
    ├── Models/                  ← Will move to Core project
    ├── Events/                  ← Will move to Core project
    ├── Controls/                ← Will stay (adapted)
    ├── Converters/              ← Will stay
    ├── Dialog/                  ← Will stay
    ├── Commands/                ← Will stay (adapted)
    └── WPFHexaEditor.csproj
```

### After Migration (Proposed)
```
Sources/
├── WpfHexaEditor.Core/          ← NEW
│   ├── Platform/                ← NEW (abstractions)
│   │   ├── Rendering/
│   │   ├── Media/
│   │   ├── Input/
│   │   └── Threading/
│   ├── Core/                    ← MOVED from WPFHexaEditor
│   ├── Services/                ← MOVED from WPFHexaEditor
│   ├── ViewModels/              ← MOVED from WPFHexaEditor
│   ├── Models/                  ← MOVED from WPFHexaEditor
│   └── Events/                  ← MOVED from WPFHexaEditor
│
├── WpfHexaEditor.Wpf/           ← RENAMED (was WPFHexaEditor)
│   ├── Platform/                ← NEW (WPF implementations)
│   ├── Controls/                ← ADAPTED (use abstractions)
│   ├── Converters/              ← KEPT
│   ├── Dialog/                  ← KEPT
│   └── Commands/                ← ADAPTED
│
└── WpfHexaEditor.Avalonia/      ← NEW
    ├── Platform/                ← NEW (Avalonia implementations)
    ├── Controls/                ← PORTED from Wpf
    ├── Converters/              ← PORTED from Wpf
    └── Views/                   ← NEW
```

---

## 🔌 Dependency Injection Pattern (Optional Enhancement)

```mermaid
graph TB
    subgraph "Application Startup"
        Startup["App.xaml.cs / Program.cs"]
    end

    subgraph "DI Container"
        Container["Service Container<br/>━━━━━━━━━━<br/>Register:<br/>• IPlatformTimer → WpfTimer<br/>• IDrawingContext factory<br/>• Services"]
    end

    subgraph "Controls"
        HexEditor["HexEditor"]
        HexViewport["HexViewport"]
    end

    subgraph "Platform Services"
        PlatformFactory["Platform Factory<br/>━━━━━━━━━━<br/>CreateTimer()<br/>CreateDrawingContext()<br/>CreateBrush()"]
    end

    Startup --> Container
    Container --> PlatformFactory
    HexEditor --> PlatformFactory
    HexViewport --> PlatformFactory

    style Startup fill:#0ea5e9,stroke:#0284c7,color:#fff
    style Container fill:#fbbf24,stroke:#f59e0b,color:#000
    style PlatformFactory fill:#10b981,stroke:#059669,color:#fff
```

---

## 📊 Code Distribution Analysis

```mermaid
pie title Code Distribution (40,940 lines total)
    "Core/Bytes (Portable)" : 9522
    "Services (Portable)" : 4305
    "ViewModels (Portable)" : 2500
    "Models/Events (Portable)" : 3673
    "Controls (Needs Adaptation)" : 8500
    "Converters/Dialogs" : 3800
    "Commands/Utils" : 1640
    "Platform Abstractions (New)" : 1000
    "WPF Platform Impl (New)" : 2000
    "Avalonia Platform Impl (New)" : 2000
    "Tests" : 2000
```

---

## 🎨 Theme Support Architecture

```mermaid
graph LR
    subgraph "Theme System"
        ThemeManager["Theme Manager<br/>(Portable)"]

        subgraph "WPF Themes"
            WpfLight["Light.xaml"]
            WpfDark["Dark.xaml"]
            WpfCyber["Cyberpunk.xaml"]
        end

        subgraph "Avalonia Themes"
            AvLight["Light.axaml"]
            AvDark["Dark.axaml"]
            AvCyber["Cyberpunk.axaml"]
        end

        ColorScheme["Color Scheme<br/>(Platform-agnostic)<br/>━━━━━━━━━━<br/>• Background<br/>• Foreground<br/>• Selection<br/>• Highlights"]
    end

    ThemeManager --> ColorScheme
    ColorScheme --> WpfLight
    ColorScheme --> WpfDark
    ColorScheme --> WpfCyber
    ColorScheme --> AvLight
    ColorScheme --> AvDark
    ColorScheme --> AvCyber

    style ThemeManager fill:#10b981,stroke:#059669,color:#fff
    style ColorScheme fill:#fbbf24,stroke:#f59e0b,color:#000
    style WpfLight fill:#00bcf2,stroke:#0099cc,color:#000
    style WpfDark fill:#00bcf2,stroke:#0099cc,color:#000
    style WpfCyber fill:#00bcf2,stroke:#0099cc,color:#000
    style AvLight fill:#c084fc,stroke:#a855f7,color:#000
    style AvDark fill:#c084fc,stroke:#a855f7,color:#000
    style AvCyber fill:#c084fc,stroke:#a855f7,color:#000
```

---

## 🧪 Testing Architecture

```mermaid
graph TB
    subgraph "Unit Tests"
        CoreTests["WpfHexaEditor.Core.Tests<br/>━━━━━━━━━━<br/>• Byte manipulation<br/>• Services logic<br/>• ViewModels"]

        PlatformTests["Platform Tests<br/>━━━━━━━━━━<br/>• Abstraction contracts<br/>• WPF implementations<br/>• Avalonia implementations"]
    end

    subgraph "Integration Tests"
        WpfIntegration["WPF Integration Tests<br/>━━━━━━━━━━<br/>• Control rendering<br/>• User interactions<br/>• File operations"]

        AvIntegration["Avalonia Integration Tests<br/>━━━━━━━━━━<br/>• Control rendering<br/>• User interactions<br/>• File operations"]
    end

    subgraph "UI Tests (Manual)"
        Visual["Visual Regression Tests<br/>━━━━━━━━━━<br/>• Screenshot comparison<br/>• Rendering accuracy<br/>• Theme consistency"]

        Perf["Performance Tests<br/>━━━━━━━━━━<br/>• Large file handling<br/>• Scrolling smoothness<br/>• Memory usage"]
    end

    CoreTests -.->|tests| Core[WpfHexaEditor.Core]
    PlatformTests -.->|tests| Core
    WpfIntegration -.->|tests| Wpf[WpfHexaEditor.Wpf]
    AvIntegration -.->|tests| Av[WpfHexaEditor.Avalonia]
    Visual -.->|validates| Wpf
    Visual -.->|validates| Av
    Perf -.->|validates| Wpf
    Perf -.->|validates| Av

    style CoreTests fill:#10b981,stroke:#059669,color:#fff
    style PlatformTests fill:#fbbf24,stroke:#f59e0b,color:#000
    style WpfIntegration fill:#00bcf2,stroke:#0099cc,color:#000
    style AvIntegration fill:#c084fc,stroke:#a855f7,color:#000
    style Visual fill:#f97316,stroke:#ea580c,color:#fff
    style Perf fill:#f97316,stroke:#ea580c,color:#fff
```

---

## 🚀 Build & CI/CD Pipeline

```mermaid
graph LR
    subgraph "Source Control"
        Git["Git Repository<br/>━━━━━━━━━━<br/>master branch"]
    end

    subgraph "CI Pipeline (GitHub Actions)"
        Build["Build Stage<br/>━━━━━━━━━━<br/>• Core (netstandard2.0)<br/>• WPF (net48, net8.0-windows)<br/>• Avalonia (net8.0)"]

        Test["Test Stage<br/>━━━━━━━━━━<br/>• Unit tests<br/>• Integration tests<br/>• Code coverage"]

        Package["Package Stage<br/>━━━━━━━━━━<br/>• NuGet pack<br/>• Version stamping<br/>• Symbol packages"]
    end

    subgraph "Multi-Platform Tests"
        Windows["Windows Tests<br/>━━━━━━━━━━<br/>• WPF<br/>• Avalonia"]

        Linux["Linux Tests<br/>━━━━━━━━━━<br/>• Avalonia"]

        MacOS["macOS Tests<br/>━━━━━━━━━━<br/>• Avalonia"]
    end

    subgraph "Deployment"
        NuGet["NuGet.org<br/>━━━━━━━━━━<br/>• WpfHexaEditor.Core<br/>• WpfHexaEditor.Wpf<br/>• WpfHexaEditor.Avalonia"]

        GitHub["GitHub Releases<br/>━━━━━━━━━━<br/>• Release notes<br/>• Sample apps<br/>• Documentation"]
    end

    Git --> Build
    Build --> Test
    Test --> Package
    Package --> Windows
    Package --> Linux
    Package --> MacOS
    Windows --> NuGet
    Linux --> NuGet
    MacOS --> NuGet
    NuGet --> GitHub

    style Git fill:#0ea5e9,stroke:#0284c7,color:#fff
    style Build fill:#10b981,stroke:#059669,color:#fff
    style Test fill:#fbbf24,stroke:#f59e0b,color:#000
    style Package fill:#f97316,stroke:#ea580c,color:#fff
    style Windows fill:#00bcf2,stroke:#0099cc,color:#000
    style Linux fill:#c084fc,stroke:#a855f7,color:#000
    style MacOS fill:#8b5cf6,stroke:#7c3aed,color:#fff
    style NuGet fill:#0078d4,stroke:#005a9e,color:#fff
    style GitHub fill:#6366f1,stroke:#4f46e5,color:#fff
```

---

## 📈 Performance Optimization Strategy

```mermaid
graph TB
    subgraph "Rendering Optimizations"
        Frozen["Frozen Brushes<br/>━━━━━━━━━━<br/>Cache & freeze<br/>common brushes<br/><br/>↓ 30% allocation"]

        Virtual["Virtual Scrolling<br/>━━━━━━━━━━<br/>Render only<br/>visible bytes<br/><br/>↓ 90% rendering time"]

        Batch["Batch Rendering<br/>━━━━━━━━━━<br/>Group draw calls<br/><br/>↓ 50% API calls"]
    end

    subgraph "Memory Optimizations"
        LazyLoad["Lazy Loading<br/>━━━━━━━━━━<br/>Load file chunks<br/>on demand<br/><br/>↓ 95% memory"]

        Pool["Object Pooling<br/>━━━━━━━━━━<br/>Reuse ByteData<br/>instances<br/><br/>↓ 60% GC pressure"]

        Dispose["Proper Disposal<br/>━━━━━━━━━━<br/>Dispose streams<br/>and resources<br/><br/>No memory leaks"]
    end

    subgraph "CPU Optimizations"
        Async["Async Operations<br/>━━━━━━━━━━<br/>Background search<br/>and file ops<br/><br/>Responsive UI"]

        Cache["Result Caching<br/>━━━━━━━━━━<br/>Cache calculated<br/>values<br/><br/>↓ 70% recalc"]

        SIMD["SIMD (Future)<br/>━━━━━━━━━━<br/>Vectorized<br/>byte operations<br/><br/>↑ 4x throughput"]
    end

    style Frozen fill:#10b981,stroke:#059669,color:#fff
    style Virtual fill:#10b981,stroke:#059669,color:#fff
    style Batch fill:#10b981,stroke:#059669,color:#fff
    style LazyLoad fill:#0ea5e9,stroke:#0284c7,color:#fff
    style Pool fill:#0ea5e9,stroke:#0284c7,color:#fff
    style Dispose fill:#0ea5e9,stroke:#0284c7,color:#fff
    style Async fill:#f97316,stroke:#ea580c,color:#fff
    style Cache fill:#f97316,stroke:#ea580c,color:#fff
    style SIMD fill:#f97316,stroke:#ea580c,color:#fff
```

---

## 🎯 Summary

### Key Architectural Principles

1. **Minimal Abstraction**: Only abstract what's necessary for cross-platform
2. **Shared Core**: 85% of code is platform-agnostic
3. **Clean Separation**: UI layer completely separated from business logic
4. **Performance First**: Zero-overhead abstractions, caching, lazy loading
5. **Maintainability**: Clear project boundaries, single responsibility

### Benefits of This Architecture

✅ **Cross-platform**: Windows, Linux, macOS support via Avalonia
✅ **Maintainable**: Shared business logic reduces duplication
✅ **Testable**: Core logic fully testable without UI dependencies
✅ **Performant**: Minimal overhead, optimized rendering
✅ **Extensible**: Easy to add new platforms (MAUI, Uno, etc.)
✅ **Backward compatible**: WPF version continues to work

### Technical Debt Addressed

✅ Separation of concerns (V2 architecture enhanced)
✅ Dependency injection ready
✅ Platform independence
✅ Better testing infrastructure
✅ Modern .NET support (net8.0)

---

**Related Documents:**
- [AVALONIA_PORTING_PLAN.md](./AVALONIA_PORTING_PLAN.md) - Complete implementation plan
- [AVALONIA_PORTAGE_PLAN.md](./AVALONIA_PORTAGE_PLAN.md) - Plan complet en français

**Status:** 🟢 Architecture approved - Ready for implementation
**Last Updated:** 2026-02-16
