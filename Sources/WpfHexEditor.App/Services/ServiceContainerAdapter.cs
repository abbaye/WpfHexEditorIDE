// ==========================================================
// Project: WpfHexEditor.App
// File: Services/ServiceContainerAdapter.cs
// Description:
//     Adapter exposing the host IServiceProvider (Microsoft.Extensions.DI)
//     to plugins through the SDK's IServiceContainer facade. Supports
//     scope creation so plugins can isolate Scoped lifetimes.
// ==========================================================

using Microsoft.Extensions.DependencyInjection;
using WpfHexEditor.SDK.Contracts;

namespace WpfHexEditor.App.Services;

/// <summary>Host implementation of <see cref="IServiceContainer"/>.</summary>
internal sealed class ServiceContainerAdapter : IServiceContainer
{
    private readonly IServiceProvider _provider;

    public ServiceContainerAdapter(IServiceProvider provider) => _provider = provider;

    public T? Resolve<T>() where T : class => _provider.GetService<T>();

    public T Require<T>() where T : class => _provider.GetRequiredService<T>();

    public IServiceScope CreateScope()
        => new ServiceScopeAdapter(_provider.CreateScope());
}

internal sealed class ServiceScopeAdapter : IServiceScope
{
    private readonly Microsoft.Extensions.DependencyInjection.IServiceScope _inner;

    public ServiceScopeAdapter(Microsoft.Extensions.DependencyInjection.IServiceScope inner)
    {
        _inner    = inner;
        Container = new ServiceContainerAdapter(inner.ServiceProvider);
    }

    public IServiceContainer Container { get; }

    public void Dispose() => _inner.Dispose();
}
