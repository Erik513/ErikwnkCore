using System.Reflection;

namespace ErikwnkCore.Updater;

/// <summary>
/// The net48 build references specific NuGet-shipped versions of BCL-facade
/// assemblies (System.Text.Json and friends) that a modern .NET host already ships
/// as part of its shared framework, usually at a different exact version - e.g.
/// ErikwnkCore.Updater's net48 build hard-requires System.Text.Json 8.0.0.5, but a
/// net8.0 host only ships 8.0.0.0. .NET's default assembly resolution won't
/// substitute one for the other on its own and consuming apps have no reason to
/// know this needs solving, so this acts as a lightweight stand-in for the classic
/// .NET Framework bindingRedirect: whenever a strict-version request for one of
/// these fails, ask for the assembly by its simple name instead and let the host
/// resolve whatever it actually has.
/// </summary>
internal static class AssemblyResolution
{
    private static readonly string[] UnifiableAssemblyNames =
    {
        "System.Text.Json",
        "System.Text.Encodings.Web",
        "System.Buffers",
        "System.Memory",
        "System.Runtime.CompilerServices.Unsafe",
        "System.Threading.Tasks.Extensions",
        "System.ValueTuple",
        "System.Numerics.Vectors",
    };

    private static readonly object Gate = new();
    private static bool registered;

    public static void EnsureRegistered()
    {
        if (registered)
        {
            return;
        }

        lock (Gate)
        {
            if (registered)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
            registered = true;
        }
    }

    private static Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
    {
        string requestedName = new AssemblyName(args.Name).Name ?? string.Empty;

        if (Array.IndexOf(UnifiableAssemblyNames, requestedName) < 0)
        {
            return null;
        }

        try
        {
            return Assembly.Load(requestedName);
        }
        catch
        {
            return null;
        }
    }
}
