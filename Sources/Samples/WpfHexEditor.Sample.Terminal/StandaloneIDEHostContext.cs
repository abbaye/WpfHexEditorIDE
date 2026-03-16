// ==========================================================
// Project: WpfHexEditor.Sample.Terminal
// File: StandaloneIDEHostContext.cs
// Author: Auto
// Created: 2026-03-08
// Description:
//     Minimal no-op implementation of IIDEHostContext for use in the
//     standalone Terminal sample. All service calls that require an active
//     IDE document (HexEditor, SolutionExplorer, etc.) return safe defaults
//     so that HxTerminal built-in commands degrade gracefully with messages
//     like "no active document" rather than crashing.
//
// Architecture Notes:
//     Pattern: Null Object — all service implementations return empty/default
//     values and raise no events. No WPF dependency injection required.
//     Only IOutputService is wired to the TerminalPanel output so that
//     echo/info/warning messages from commands still appear in the terminal.
// ==========================================================

using System.Windows;
using WpfHexEditor.Core.Interfaces;
using WpfHexEditor.Editor.Core.Documents;
using WpfHexEditor.Events;
using WpfHexEditor.SDK.Contracts;
using WpfHexEditor.SDK.Contracts.Focus;
using WpfHexEditor.SDK.Contracts.Services;
using WpfHexEditor.SDK.Descriptors;
using WpfHexEditor.SDK.Models;

namespace WpfHexEditor.Sample.Terminal;

// ---------------------------------------------------------------------------
// Main context
// ---------------------------------------------------------------------------

/// <summary>
/// Standalone (no-IDE) implementation of <see cref="IIDEHostContext"/>.
/// All services return safe no-op defaults. Wire <see cref="OutputService"/>
/// to the active <see cref="WpfHexEditor.Core.Terminal.ITerminalOutput"/> after
/// construction if you want command output routed to the terminal.
/// </summary>
internal sealed class StandaloneIDEHostContext : IIDEHostContext
{
    public IDocumentHostService     DocumentHost     { get; } = new NullDocumentHostService();
    public ISolutionExplorerService SolutionExplorer { get; } = new NullSolutionExplorerService();
    public WpfHexEditor.Editor.Core.ISolutionManager? SolutionManager => null;
    public IHexEditorService        HexEditor        { get; } = new NullHexEditorService();
    public ICodeEditorService       CodeEditor       { get; } = new NullCodeEditorService();
    public IOutputService           Output           { get; } = new NullOutputService();
    public IParsedFieldService      ParsedField      { get; } = new NullParsedFieldService();
    public IErrorPanelService       ErrorPanel       { get; } = new NullErrorPanelService();
    public IFocusContextService     FocusContext     { get; } = new NullFocusContextService();
    public IPluginEventBus          EventBus         { get; } = new NullPluginEventBus();
    public IUIRegistry              UIRegistry       { get; } = new NullUIRegistry();
    public IThemeService            Theme            { get; } = new NullThemeService();
    public IPermissionService       Permissions      { get; } = new NullPermissionService();
    public ITerminalService         Terminal         { get; } = new NullTerminalService();
    public IIDEEventBus             IDEEvents        { get; } = new NullIDEEventBus();
    public IPluginCapabilityRegistry CapabilityRegistry { get; } = new NullCapabilityRegistry();
    public IExtensionRegistry       ExtensionRegistry  { get; } = new NullExtensionRegistryStub();
}

// ---------------------------------------------------------------------------
// Service stubs
// ---------------------------------------------------------------------------

file sealed class NullDocumentHostService : IDocumentHostService
{
    public IDocumentManager Documents { get; } = new NullDocumentManager();

    public void OpenDocument(string filePath, string? preferredEditorId = null) { }
    public void ActivateAndNavigateTo(string filePath, int line, int column)    { }
    public void SaveAll()                                                        { }

    private sealed class NullDocumentManager : IDocumentManager
    {
        public IReadOnlyList<DocumentModel> OpenDocuments => [];
        public DocumentModel?               ActiveDocument => null;

        public DocumentModel Register(string contentId, string? filePath, string? editorId, string? projectItemId)
            => new(contentId, filePath, projectItemId, editorId);
        public void AttachEditor(string contentId, WpfHexEditor.Editor.Core.IDocumentEditor editor) { }
        public void Unregister(string contentId) { }
        public void SetActive(string contentId)  { }

        public IReadOnlyList<DocumentModel> GetDirty() => [];

#pragma warning disable 67
        public event EventHandler<DocumentModel>?  DocumentRegistered;
        public event EventHandler<DocumentModel>?  DocumentUnregistered;
        public event EventHandler<DocumentModel?>? ActiveDocumentChanged;
        public event EventHandler<DocumentModel>?  DocumentDirtyChanged;
        public event EventHandler<DocumentModel>?  DocumentTitleChanged;
#pragma warning restore 67
    }
}

file sealed class NullSolutionExplorerService : ISolutionExplorerService
{
    public bool   HasActiveSolution   => false;
    public string? ActiveSolutionPath => null;
    public string? ActiveSolutionName => null;

    public IReadOnlyList<string> GetOpenFilePaths()     => [];
    public IReadOnlyList<string> GetSolutionFilePaths() => [];
    public IReadOnlyList<string> GetFilesInDirectory(string path) => [];

    public Task OpenFileAsync(string filePath, CancellationToken ct = default)       => Task.CompletedTask;
    public Task CloseFileAsync(string? fileName = null, CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveFileAsync(string? fileName = null, CancellationToken ct = default)  => Task.CompletedTask;
    public Task OpenFolderAsync(string path, CancellationToken ct = default)         => Task.CompletedTask;
    public Task OpenProjectAsync(string name, CancellationToken ct = default)        => Task.CompletedTask;
    public Task CloseProjectAsync(string name, CancellationToken ct = default)       => Task.CompletedTask;
    public Task OpenSolutionAsync(string path, CancellationToken ct = default)       => Task.CompletedTask;
    public Task CloseSolutionAsync(CancellationToken ct = default)                   => Task.CompletedTask;
    public Task ReloadSolutionAsync(CancellationToken ct = default)                  => Task.CompletedTask;

#pragma warning disable 67
    public event EventHandler? SolutionChanged;
#pragma warning restore 67
}

file sealed class NullHexEditorService : IHexEditorService
{
    public bool   IsActive              => false;
    public string? CurrentFilePath      => null;
    public long   FileSize              => 0;
    public long   CurrentOffset         => 0;
    public long   SelectionStart        => -1;
    public long   SelectionStop         => -1;
    public long   SelectionLength       => 0;
    public long   FirstVisibleByteOffset => 0;
    public long   LastVisibleByteOffset  => 0;

    public byte[] ReadBytes(long offset, int length) => [];
    public byte[] GetSelectedBytes()                 => [];
    public IReadOnlyList<long> SearchHex(string hexPattern)  => [];
    public IReadOnlyList<long> SearchText(string text)       => [];
    public void WriteBytes(long offset, byte[] data)         { }
    public void SetSelection(long start, long end)           { }
    public void ConnectParsedFieldsPanel(IParsedFieldsPanel panel)  { }
    public void DisconnectParsedFieldsPanel()                       { }
    public void AddCustomBackgroundBlock(WpfHexEditor.Core.CustomBackgroundBlock block) { }
    public void ClearCustomBackgroundBlockByTag(string tag)         { }

#pragma warning disable 67
    public event EventHandler? ViewportScrolled;
    public event EventHandler? SelectionChanged;
    public event EventHandler? FileOpened;
    public event EventHandler<FormatDetectedArgs>? FormatDetected;
    public event EventHandler? ActiveEditorChanged;
#pragma warning restore 67
}

file sealed class NullCodeEditorService : ICodeEditorService
{
    public bool   IsActive         => false;
    public string? CurrentLanguage => null;
    public string? CurrentFilePath => null;
    public int    CaretLine        => 1;
    public int    CaretColumn      => 1;

    public string? GetContent()      => null;
    public string  GetSelectedText() => string.Empty;

#pragma warning disable 67
    public event EventHandler? DocumentChanged;
#pragma warning restore 67
}

file sealed class NullOutputService : IOutputService
{
    public void Info(string message)                            { }
    public void Warning(string message)                         { }
    public void Error(string message)                           { }
    public void Debug(string message)                           { }
    public void Write(string category, string message)          { }
    public void Clear()                                         { }
    public IReadOnlyList<string> GetRecentLines(int count)      => [];
}

file sealed class NullParsedFieldService : IParsedFieldService
{
    public bool HasParsedFields => false;

    public IReadOnlyList<ParsedFieldEntry> GetParsedFields()       => [];
    public ParsedFieldEntry? GetFieldAtOffset(long offset)         => null;

#pragma warning disable 67
    public event EventHandler? ParsedFieldsChanged;
#pragma warning restore 67
}

file sealed class NullErrorPanelService : IErrorPanelService
{
    public void PostDiagnostic(DiagnosticSeverity severity, string message,
        string source = "", int line = -1, int column = -1)        { }
    public void ClearPluginDiagnostics(string pluginId)            { }
    public IReadOnlyList<string> GetRecentErrors(int count)        => [];
}

file sealed class NullFocusContextService : IFocusContextService
{
    public IDocument? ActiveDocument => null;
    public IPanel?    ActivePanel    => null;

#pragma warning disable 67
    public event EventHandler<FocusChangedEventArgs>? FocusChanged;
#pragma warning restore 67
}

file sealed class NullPluginEventBus : IPluginEventBus
{
    public void        Publish<TEvent>(TEvent evt) where TEvent : class                      { }
    public Task        PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default)
        where TEvent : class                                                                  => Task.CompletedTask;
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class        => NullDisposable.Instance;
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class    => NullDisposable.Instance;

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}

file sealed class NullUIRegistry : IUIRegistry
{
    public string GenerateUIId(string pluginId, string elementType, string elementName) => $"{pluginId}.{elementType}.{elementName}";
    public bool   Exists(string uiId) => false;

    public void RegisterPanel(string uiId, UIElement panel, string pluginId, PanelDescriptor descriptor)   { }
    public void UnregisterPanel(string uiId)                                                               { }
    public void RegisterMenuItem(string uiId, string pluginId, MenuItemDescriptor descriptor)              { }
    public void UnregisterMenuItem(string uiId)                                                            { }
    public void RegisterToolbarItem(string uiId, string pluginId, ToolbarItemDescriptor descriptor)        { }
    public void UnregisterToolbarItem(string uiId)                                                         { }
    public void RegisterDocumentTab(string uiId, UIElement content, string pluginId, DocumentDescriptor descriptor) { }
    public void UnregisterDocumentTab(string uiId)                                                         { }
    public void RegisterStatusBarItem(string uiId, string pluginId, StatusBarItemDescriptor descriptor)    { }
    public void UnregisterStatusBarItem(string uiId)                                                       { }
    public void ShowPanel(string uiId)                                                                     { }
    public void HidePanel(string uiId)                                                                     { }
    public void TogglePanel(string uiId)                                                                   { }
    public void FocusPanel(string uiId)                                                                    { }
    public bool IsPanelVisible(string uiId)                                                               => true;
    public void UnregisterAllForPlugin(string pluginId)                                                    { }
}

file sealed class NullThemeService : IThemeService
{
    public string CurrentTheme => "Dark";

    public ResourceDictionary GetThemeResources()                          => new();
    public void RegisterThemeAwareControl(FrameworkElement element)        { }
    public void UnregisterThemeAwareControl(FrameworkElement element)      { }

#pragma warning disable 67
    public event EventHandler? ThemeChanged;
#pragma warning restore 67
}

file sealed class NullPermissionService : IPermissionService
{
    public bool             IsGranted(string pluginId, PluginPermission permission) => true;
    public PluginPermission GetGranted(string pluginId)                             => (PluginPermission)long.MaxValue;
    public void             Grant(string pluginId, PluginPermission permission)     { }
    public void             Revoke(string pluginId, PluginPermission permission)    { }

#pragma warning disable 67
    public event EventHandler<PermissionChangedEventArgs>? PermissionChanged;
#pragma warning restore 67
}

file sealed class NullTerminalService : ITerminalService
{
    public void WriteLine(string text)          { }
    public void WriteInfo(string text)          { }
    public void WriteWarning(string text)       { }
    public void WriteError(string text)         { }
    public void Clear()                         { }
    public void OpenSession(string shellType)   { }
    public void CloseActiveSession()            { }
}

file sealed class NullIDEEventBus : IIDEEventBus
{
    private sealed class EmptyDisposable : IDisposable { public void Dispose() { } }
    private sealed class EmptyRegistry : IEventRegistry
    {
        public IReadOnlyList<EventRegistryEntry> GetAllEntries() => [];
        public int GetSubscriberCount(Type eventType) => 0;
        public void Register(Type eventType, string displayName, string producerLabel) { }
        public void UpdateSubscriberCount(Type eventType, int delta) { }
    }
    public IEventRegistry EventRegistry { get; } = new EmptyRegistry();
    public void Publish<TEvent>(TEvent evt) where TEvent : class { }
    public Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct = default) where TEvent : class => Task.CompletedTask;
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class => new EmptyDisposable();
    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : class => new EmptyDisposable();
    public IDisposable Subscribe<TEvent>(Action<IDEEventContext, TEvent> handler) where TEvent : class => new EmptyDisposable();
    public IDisposable Subscribe<TEvent>(Func<IDEEventContext, TEvent, Task> handler) where TEvent : class => new EmptyDisposable();
}

file sealed class NullCapabilityRegistry : IPluginCapabilityRegistry
{
    public IReadOnlyList<string> FindPluginsWithFeature(string feature) => [];
    public bool PluginHasFeature(string pluginId, string feature) => false;
    public IReadOnlyList<string> GetFeaturesForPlugin(string pluginId) => [];
    public IReadOnlyList<string> GetAllRegisteredFeatures() => [];
}

file sealed class NullExtensionRegistryStub : IExtensionRegistry
{
    public IReadOnlyList<T> GetExtensions<T>() where T : class => [];
    public void Register<T>(string pluginId, T implementation) where T : class { }
    public void Register(string pluginId, Type contractType, object implementation) { }
    public void UnregisterAll(string pluginId) { }
    public IReadOnlyList<ExtensionRegistryEntry> GetAllEntries() => [];
}
