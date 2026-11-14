using Newtonsoft.Json;
using Shop.Contracts;

namespace Shop.Plugins.Discount;

public abstract class PluginBase : IPlugin
{
    public abstract string Name { get; }
    public abstract string Execute(string input);
}

public sealed class DiscountPlugin : PluginBase
{
    public override string Name => "discount";

    public override string Execute(string input) =>
        JsonConvert.SerializeObject(new { input, discountPercent = 10 });
}

public sealed class HolidayPlugin : PluginBase
{
    public override string Name => "holiday";

    public override string Execute(string input) =>
        JsonConvert.SerializeObject(new { input, holiday = true });
}
