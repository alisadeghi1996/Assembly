using Newtonsoft.Json;

namespace Shop.PluginHost;

public static class HostInfo
{
    /// <summary>The Newtonsoft.Json version the HOST runs against (13.x).</summary>
    public static Version JsonVersion => typeof(JsonConvert).Assembly.GetName().Version!;
}
