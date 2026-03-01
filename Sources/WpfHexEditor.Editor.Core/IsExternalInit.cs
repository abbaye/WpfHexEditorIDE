// Polyfill for .NET Framework 4.8: required for records and init-only setters.
// The compiler emits a reference to this type when compiling C# 9+ features
// targeting frameworks that don't ship it natively.
#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif
