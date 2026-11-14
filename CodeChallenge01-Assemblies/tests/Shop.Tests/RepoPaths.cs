namespace Shop.Tests;

internal static class RepoPaths
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    public static string Root { get; } = FindRoot();

    public static string DiscountPlugin => Path.Combine(
        Root, "plugins", "Shop.Plugins.Discount", "bin", Configuration, "net8.0", "Shop.Plugins.Discount.dll");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CodeChallenge01.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("CodeChallenge01.sln not found");
    }
}
