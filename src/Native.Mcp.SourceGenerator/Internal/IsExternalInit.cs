// Polyfill enabling C# 'init' accessors and records on netstandard2.0.
// The compiler requires this type to exist; it is never referenced at runtime.

namespace System.Runtime.CompilerServices
{
    using System.ComponentModel;

    /// <summary>Reserved for compiler use (init-only setters on netstandard2.0).</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}
