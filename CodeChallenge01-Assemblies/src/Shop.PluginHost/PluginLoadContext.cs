using System.Reflection;
using System.Runtime.Loader;

namespace Shop.PluginHost;

/// <summary>
/// An isolated, unloadable load context for one plugin.
///  - the plugin's own dependencies (e.g. Newtonsoft.Json 12) load INSIDE this context
///  - anything the plugin does not ship (e.g. Shop.Contracts) falls back to the default context
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    // Reads the plugin's .deps.json and maps an assembly name to a file next to the plugin.
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginAssemblyPath)
        // isCollectible: true is what makes Unload() possible.
        : base(name: Path.GetFileNameWithoutExtension(pluginAssemblyPath), isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);

        // The plugin ships this assembly: load it here, isolated from the host's copy.
        if (path is not null)
            return LoadFromAssemblyPath(path);

        // Not shipped by the plugin (Shop.Contracts, the framework): returning null hands the request
        // to the default context, so host and plugin share ONE IPlugin type.
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}
