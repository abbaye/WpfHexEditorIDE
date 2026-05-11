// ==========================================================
// Project: WpfHexEditor.SDK
// File: Contracts/IServiceContainer.cs
// Description:
//     Plugin-facing service container. Wraps the host's IServiceProvider
//     with a small lifecycle-aware API so plugins can resolve services
//     and create scoped sub-containers without taking a direct dependency
//     on Microsoft.Extensions.DependencyInjection.
// ==========================================================

namespace WpfHexEditor.SDK.Contracts;

/// <summary>Service lifetime hint used when registering plugin-contributed services.</summary>
public enum ServiceLifetime
{
    /// <summary>Single instance shared across the entire process.</summary>
    Singleton,
    /// <summary>One instance per scope (e.g. per document open session).</summary>
    Scoped,
    /// <summary>A new instance is created on every Resolve call.</summary>
    Transient,
}

/// <summary>
/// Lightweight service-locator facade exposed by the IDE host to plugins.
/// Backed by Microsoft.Extensions.DependencyInjection on the host side.
/// </summary>
public interface IServiceContainer
{
    /// <summary>Resolves a service of type <typeparamref name="T"/>; returns null if not registered.</summary>
    T? Resolve<T>() where T : class;

    /// <summary>Resolves a service of type <typeparamref name="T"/>; throws if not registered.</summary>
    T Require<T>() where T : class;

    /// <summary>
    /// Creates a child scope. Dispose the returned scope to release any
    /// <see cref="ServiceLifetime.Scoped"/> instances created within it.
    /// </summary>
    IServiceScope CreateScope();
}

/// <summary>Disposable scope produced by <see cref="IServiceContainer.CreateScope"/>.</summary>
public interface IServiceScope : IDisposable
{
    /// <summary>The container scoped to this lifetime.</summary>
    IServiceContainer Container { get; }
}
