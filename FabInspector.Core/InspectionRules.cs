using System.ComponentModel;
using System.Runtime;
using System.Text.Json.Serialization;

namespace FabInspector.Core
{
    /// <summary>
    /// Deserialises inspection rules from json 
    /// </summary>
    public sealed class InspectionRules
    {
        public List<Rule> Rules { get; set; } = [];
    }

    public class Rule
    {
        public string? Id { get; set; }

        public string ItemType { get; set; } = "report_deprecated";

        public required string Name { get; set; }

        public string? Description { get; set; }

        public List<string>? Tags { get; set; }

        public bool Disabled { get; set; }

        public string? LogType { get; set; }

        public string? Part { get; set; }

        public required Test Test { get; set; }

        public bool ApplyPatch { get; set; }

        public Patch? Patch { get; set; }

        public bool PathErrorWhenNoMatch { get; set; }
    }
}
