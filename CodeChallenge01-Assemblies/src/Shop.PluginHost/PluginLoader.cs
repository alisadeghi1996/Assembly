using Shop.Contracts;

namespace Shop.PluginHost;

public sealed record LoadedPlugin(PluginLoadContext Context, IReadOnlyList<IPlugin> Plugins);

public static class PluginLoader
{
    /// <summary>
    /// Loads the plugin assembly into a new <see cref="PluginLoadContext"/> and creates one instance
    /// of every concrete IPlugin type in it.
    /// </summary>
    public static LoadedPlugin Load(string pluginAssemblyPath)
    {
        // The resolver and LoadFromAssemblyPath both need an absolute path.
        var fullPath = Path.GetFullPath(pluginAssemblyPath);

        var context = new PluginLoadContext(fullPath);
        var assembly = context.LoadFromAssemblyPath(fullPath);

        var plugins = assembly.GetTypes()
            // typeof(IPlugin) is the HOST's type. The check only succeeds because the plugin's
            // Shop.Contracts came from the default context too (same assembly = same type identity).
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IPlugin).IsAssignableFrom(type))
            .Select(type => (IPlugin)Activator.CreateInstance(type)!)
            .ToList();

        return new LoadedPlugin(context, plugins);
    }
}
