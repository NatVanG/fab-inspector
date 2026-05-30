namespace FabInspector.Core
{
    /// <summary>
    /// Represents a single rules set reference inside a rules catalog.
    /// </summary>
    public interface IRuleSet
    {
        string Name { get; set; }

        bool Disabled { get; set; }

        string Path { get; set; }
    }
}
