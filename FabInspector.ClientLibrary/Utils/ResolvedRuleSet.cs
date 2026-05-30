using FabInspector.Core;

namespace FabInspector.ClientLibrary.Utils
{
    public sealed class ResolvedRuleSet
    {
        public required string Name { get; init; }

        public required string SourcePath { get; init; }

        public required InspectionRules Rules { get; init; }
    }
}
