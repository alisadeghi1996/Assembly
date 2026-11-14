namespace Shop.Contracts;

/// <summary>
/// Contract shared between the host and every plugin.
/// This assembly must be loaded exactly once (in the default load context).
/// </summary>
public interface IPlugin
{
    string Name { get; }

    string Execute(string input);
}
