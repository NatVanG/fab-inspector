using System.Text.Json.Nodes;

namespace PBIRInspectorLibrary.Output
{
    public class TestResult
    {
        public Guid Id { get; private set; }

        public string? RuleId { get; set; }

        public required string RuleName { get; set; }

        public string? RuleDescription { get; set; }

        public MessageTypeEnum LogType { get; set; }

        public string? RuleItemType { get; set; }

        public string? ItemPath { get; set; }

        public string? ParentName { get; set; }

        public string? ParentDisplayName { get; set; }

        public bool Pass { get; set; }

        public JsonNode? Expected { get; set; }

        public JsonNode? Actual { get; set; }

        public required string Message { get; set; }

        public TestResult()
        {
            Id = System.Guid.NewGuid();
        }
    }
}