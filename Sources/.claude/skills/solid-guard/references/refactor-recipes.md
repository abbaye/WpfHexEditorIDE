# Refactor recipes

Standard fix for each `solid-guard` rule. **None is automatic** — these are
templates, the human owns the decision.

## srp-mixed-concerns → introduce an Adapter

**Before** (UI class doing IO):
```cs
public class FileSettingsPanel : UserControl
{
    public void Save()
    {
        File.WriteAllText("settings.json", JsonSerializer.Serialize(_data));
    }
}
```

**After**:
```cs
public interface ISettingsStore { void Save(SettingsData data); }

public class FileSettingsStore : ISettingsStore
{
    public void Save(SettingsData data) =>
        File.WriteAllText("settings.json", JsonSerializer.Serialize(data));
}

public class FileSettingsPanel : UserControl
{
    private readonly ISettingsStore _store;
    public FileSettingsPanel(ISettingsStore store) => _store = store;
    public void Save() => _store.Save(_data);
}
```

## srp-class-too-broad → cohesion split

1. Compute LCOM (Lack of Cohesion of Methods) — done by
   `WpfHexEditor.App/Analysis/LcomCalculator`.
2. Identify clusters: groups of methods sharing the same fields.
3. Extract each cluster into its own class with a clear single
   responsibility.
4. The original class becomes a façade or a coordinator.

## ocp-massive-switch → polymorphic dispatch

**Before**:
```cs
public string Render(Shape s) => s.Kind switch
{
    ShapeKind.Circle    => RenderCircle((Circle)s),
    ShapeKind.Square    => RenderSquare((Square)s),
    ShapeKind.Triangle  => RenderTriangle((Triangle)s),
    // ... 15 more cases
};
```

**After**:
```cs
public abstract class Shape { public abstract string Render(); }
public class Circle    : Shape { public override string Render() => "..."; }
public class Square    : Shape { public override string Render() => "..."; }
// dispatch:
public string Render(Shape s) => s.Render();
```

If subclassing is impossible, use Strategy pattern: a `Dictionary<Kind,
IRenderer>` populated at composition root.

## dip-newing-services → constructor injection

**Before**:
```cs
public class ReportBuilder
{
    private readonly DataLoader _loader = new DataLoader();   // SOLID flagged
    private readonly Renderer   _render = new Renderer();
}
```

**After**:
```cs
public class ReportBuilder
{
    private readonly IDataLoader _loader;
    private readonly IRenderer   _render;
    public ReportBuilder(IDataLoader loader, IRenderer render)
    {
        _loader = loader;
        _render = render;
    }
}
```

Register at composition root:
```cs
services.AddSingleton<IDataLoader, DataLoader>();
services.AddSingleton<IRenderer,   Renderer>();
services.AddTransient<ReportBuilder>();
```

In WpfHexEditor specifically, the composition root is typically a `*Module.cs`
class (e.g., `CodeAnalysisModule.cs`, `DebugModule.cs`). Newing services
inside a `*Module.cs` is exempted by `solid-guard`.

## dip-static-deps → instance + injection

**Before**:
```cs
ConfigManager.Instance.Verbose = true;
```

**After**:
```cs
public interface IConfig { bool Verbose { get; set; } }
public class Config : IConfig { public bool Verbose { get; set; } }

// composition root:
services.AddSingleton<IConfig, Config>();

// consumer:
public class MyService(IConfig config) { ... config.Verbose = true; }
```

If the singleton is read-only (lookup table, immutable settings), keep it
static — `dip-static-deps` only flags writes (`= ...`), not reads.

## isp-fat-interface → role interfaces

**Before**:
```cs
public interface IFileService
{
    Stream Open(string path);
    void   Save(string path, Stream content);
    bool   Exists(string path);
    void   Delete(string path);
    void   Move(string src, string dst);
    void   Copy(string src, string dst);
    string[] List(string dir);
    DateTime GetLastWriteTime(string path);
    long   GetSize(string path);
    void   SetReadOnly(string path, bool ro);
    string ComputeHash(string path);
    // 11+ members — flagged
}
```

**After**:
```cs
public interface IFileReader   { Stream Open(string path); bool Exists(string path); }
public interface IFileWriter   { void Save(string path, Stream content); void Delete(string path); }
public interface IFileMetadata { DateTime GetLastWriteTime(string path); long GetSize(string path); }
public interface IFileHasher   { string ComputeHash(string path); }
```

Compose only the interfaces a consumer actually needs. `NotImplementedException`
stubs disappear because consumers never see members they don't use.
