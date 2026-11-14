using System.Xml.Linq;

namespace Shop.Tests;

public class Task1_PackageManagementTests
{
    private static readonly string[] SharedProperties = ["TargetFramework", "Nullable", "ImplicitUsings"];

    private static IEnumerable<string> ProjectFiles() =>
        Directory.EnumerateFiles(RepoPaths.Root, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    private static XDocument Load(string path) => XDocument.Load(path);

    private static string PropsPath(string name) => Path.Combine(RepoPaths.Root, name);

    [Fact]
    public void Central_package_management_is_enabled()
    {
        var path = PropsPath("Directory.Packages.props");
        Assert.True(File.Exists(path), "Directory.Packages.props is missing at the repo root");

        var enabled = Load(path).Descendants("ManagePackageVersionsCentrally").SingleOrDefault()?.Value;
        Assert.Equal("true", enabled?.Trim(), ignoreCase: true);
    }

    [Fact]
    public void Versions_are_declared_centrally()
    {
        var path = PropsPath("Directory.Packages.props");
        Assert.True(File.Exists(path));

        var versions = Load(path).Descendants("PackageVersion")
            .ToDictionary(e => (string)e.Attribute("Include")!, e => (string?)e.Attribute("Version"));

        Assert.Equal("13.0.3", versions.GetValueOrDefault("Newtonsoft.Json"));
        Assert.Equal("8.0.1", versions.GetValueOrDefault("Microsoft.Extensions.DependencyInjection"));
        Assert.Contains("xunit", versions.Keys);
    }

    [Fact]
    public void No_project_declares_a_package_version()
    {
        foreach (var project in ProjectFiles())
        {
            var withVersion = Load(project).Descendants("PackageReference")
                .Where(r => r.Attribute("Version") is not null || r.Element("Version") is not null)
                .Select(r => (string?)r.Attribute("Include"))
                .ToList();

            Assert.True(withVersion.Count == 0,
                $"{Path.GetFileName(project)} still declares versions for: {string.Join(", ", withVersion)}");
        }
    }

    [Fact]
    public void Plugin_overrides_newtonsoft_to_12()
    {
        var plugin = ProjectFiles().Single(p => p.EndsWith("Shop.Plugins.Discount.csproj"));
        var reference = Load(plugin).Descendants("PackageReference")
            .Single(r => (string?)r.Attribute("Include") == "Newtonsoft.Json");

        Assert.Equal("12.0.1", (string?)reference.Attribute("VersionOverride"));
    }

    [Fact]
    public void Shared_properties_live_in_directory_build_props()
    {
        var path = PropsPath("Directory.Build.props");
        Assert.True(File.Exists(path), "Directory.Build.props is missing at the repo root");

        var props = Load(path);
        Assert.Equal("net8.0", props.Descendants("TargetFramework").SingleOrDefault()?.Value.Trim());
        Assert.Equal("enable", props.Descendants("Nullable").SingleOrDefault()?.Value.Trim());
        Assert.Equal("enable", props.Descendants("ImplicitUsings").SingleOrDefault()?.Value.Trim());

        foreach (var project in ProjectFiles())
        {
            var duplicated = SharedProperties
                .Where(name => Load(project).Descendants(name).Any())
                .ToList();
            Assert.True(duplicated.Count == 0,
                $"{Path.GetFileName(project)} still declares: {string.Join(", ", duplicated)}");
        }
    }

    [Fact]
    public void Plugin_does_not_ship_its_own_copy_of_the_contracts()
    {
        var plugin = ProjectFiles().Single(p => p.EndsWith("Shop.Plugins.Discount.csproj"));
        var contracts = Load(plugin).Descendants("ProjectReference")
            .Single(r => ((string?)r.Attribute("Include"))?.Contains("Shop.Contracts") == true);

        string? Read(string name) =>
            (string?)contracts.Attribute(name) ?? contracts.Element(name)?.Value;

        Assert.Equal("false", Read("Private")?.Trim(), ignoreCase: true);
        Assert.Equal("runtime", Read("ExcludeAssets")?.Trim(), ignoreCase: true);
    }
}
