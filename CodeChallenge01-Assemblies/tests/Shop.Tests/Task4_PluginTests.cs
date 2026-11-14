using System.Runtime.CompilerServices;
using Shop.Contracts;
using Shop.PluginHost;

namespace Shop.Tests;

public class Task4_PluginTests
{
    [Fact]
    public void Plugin_assembly_was_built()
    {
        Assert.True(File.Exists(RepoPaths.DiscountPlugin), $"Build the solution first: {RepoPaths.DiscountPlugin}");
    }

    [Fact]
    public void Concrete_plugins_are_discovered_and_abstract_ones_skipped()
    {
        var loaded = PluginLoader.Load(RepoPaths.DiscountPlugin);

        Assert.Equal(["discount", "holiday"], loaded.Plugins.Select(p => p.Name).Order());
    }

    [Fact]
    public void Plugin_runs()
    {
        var plugin = PluginLoader.Load(RepoPaths.DiscountPlugin).Plugins.Single(p => p.Name == "discount");

        Assert.Equal("""{"input":"x","discountPercent":10}""", plugin.Execute("x"));
    }

    [Fact]
    public void Contracts_are_shared_with_the_host()
    {
        var loaded = PluginLoader.Load(RepoPaths.DiscountPlugin);

        Assert.All(loaded.Plugins, p => Assert.IsAssignableFrom<IPlugin>(p));
        Assert.DoesNotContain(loaded.Context.Assemblies, a => a.GetName().Name == "Shop.Contracts");
    }

    [Fact]
    public void Plugin_uses_its_own_newtonsoft_version_side_by_side_with_the_host()
    {
        var loaded = PluginLoader.Load(RepoPaths.DiscountPlugin);
        loaded.Plugins.First().Execute("x");   // forces Newtonsoft.Json to load

        var pluginJson = Assert.Single(loaded.Context.Assemblies, a => a.GetName().Name == "Newtonsoft.Json");
        Assert.Equal(12, pluginJson.GetName().Version!.Major);
        Assert.Equal(13, HostInfo.JsonVersion.Major);
    }

    [Fact]
    public void Plugin_can_be_unloaded()
    {
        var contextRef = LoadRunAndUnload();

        for (var i = 0; contextRef.IsAlive && i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.False(contextRef.IsAlive, "The plugin load context is still alive after Unload()");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadRunAndUnload()
    {
        var loaded = PluginLoader.Load(RepoPaths.DiscountPlugin);
        Assert.True(loaded.Context.IsCollectible, "PluginLoadContext must be collectible");

        // Execute() is deliberately NOT called here: Newtonsoft.Json caches reflection data about the
        // plugin's types in the DEFAULT context, which keeps the collectible context alive (see README).
        foreach (var plugin in loaded.Plugins)
            _ = plugin.Name;

        loaded.Context.Unload();
        return new WeakReference(loaded.Context);
    }
}
