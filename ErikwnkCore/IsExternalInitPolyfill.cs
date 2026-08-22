#if NET48
namespace System.Runtime.CompilerServices
{
    // The record/init syntax needs this marker type, which only ships in modern .NET.
    internal static class IsExternalInit
    {
    }
}
#endif
