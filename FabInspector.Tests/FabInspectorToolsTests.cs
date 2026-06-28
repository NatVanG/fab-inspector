using System.Text;
using System.Text.Json;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Output;

namespace FabInspector.Tests;

[TestFixture]
[NonParallelizable]
public class FabInspectorToolsTests
{
    [Test]
    public void Inspect_Throws_WhenRulesInputsAreMissing()
    {
        var sut = CreateSut();

        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await sut.Inspect(fabricItem: "C:/fake/item"));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("Exactly one of 'rules' or 'rulesCatalogPath' must be provided."));
    }

    [Test]
    public void DiscoverRules_Throws_WhenBothRulesInputsAreProvided()
    {
        var sut = CreateSut();

        var ex = Assert.ThrowsAsync<ArgumentException>(async () =>
            await sut.DiscoverRules(
                fabricItem: "C:/fake/item",
                rules: "rules.json",
                rulesCatalogPath: "rules-catalog.json"));

        Assert.That(ex, Is.Not.Null);
        Assert.That(ex!.Message, Does.Contain("Exactly one of 'rules' or 'rulesCatalogPath' must be provided."));
    }

    [Test]
    public async Task Inspect_ReturnsSerializedTestRunJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fab-inspector-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var itemDir = Path.Combine(tempDir, "Sales.Report");
        Directory.CreateDirectory(itemDir);
        File.WriteAllText(Path.Combine(itemDir, ".platform"), "{\"metadata\":{\"type\":\"Report\",\"displayName\":\"Sales\"}}", Encoding.UTF8);

        var rulesPath = Path.Combine(tempDir, "inspect-rules.json");
        File.WriteAllText(rulesPath, BuildInspectRulesJson(), Encoding.UTF8);

        try
        {
            var sut = CreateSut();

            var json = await sut.Inspect(
                fabricItem: itemDir,
                rules: rulesPath,
                verbose: true,
                authMethod: "local");

            var testRun = JsonSerializer.Deserialize<TestRun>(json);

            Assert.That(testRun, Is.Not.Null);
            Assert.That(testRun!.TestedFilePath, Is.EqualTo(itemDir));
            Assert.That(testRun.RulesFilePath, Is.EqualTo(rulesPath));
            Assert.That(testRun.Results, Is.Not.Null);
            Assert.That(testRun.Results.Any(), Is.True);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Test]
    public async Task DiscoverRules_ReturnsSerializedDiscoverRulesJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fab-inspector-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var itemDir = Path.Combine(tempDir, "Sales.Report");
        Directory.CreateDirectory(itemDir);
        File.WriteAllText(Path.Combine(itemDir, ".platform"), "{\"metadata\":{\"type\":\"Report\",\"displayName\":\"Sales\"}}", Encoding.UTF8);

        var rulesPath = Path.Combine(tempDir, "discover-rules.json");
        File.WriteAllText(rulesPath, BuildDiscoverRulesJson(), Encoding.UTF8);

        try
        {
            var sut = CreateSut();

            var json = await sut.DiscoverRules(
                fabricItem: itemDir,
                rules: rulesPath,
                tags: "governance",
                authMethod: "local");

            var discovered = JsonSerializer.Deserialize<DiscoverRulesResponse>(json);

            Assert.That(discovered, Is.Not.Null);
            Assert.That(discovered!.FabricItem, Is.EqualTo(itemDir));
            Assert.That(discovered.RulesFilePath, Is.EqualTo(rulesPath));
            Assert.That(discovered.Rules.Any(rule => string.Equals(rule.Name, "Report Rule", StringComparison.Ordinal)), Is.True);
            Assert.That(discovered.Rules.Any(rule => string.Equals(rule.Name, "Json Rule", StringComparison.Ordinal)), Is.False);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Test]
    public void JsonUtils_DeserialiseFromPath_DeserialisesRuleTagsAsStringList()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fab-inspector-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var rulesPath = Path.Combine(tempDir, "discover-rules.json");
        File.WriteAllText(rulesPath, BuildDiscoverRulesJson(), Encoding.UTF8);

        try
        {
            var rules = JsonUtils.DeserialiseFromPath<InspectionRules>(rulesPath);

            Assert.That(rules, Is.Not.Null);

            var reportRule = rules!.Rules.Single(rule => string.Equals(rule.Name, "Report Rule", StringComparison.Ordinal));

            Assert.That(reportRule.Tags, Is.EqualTo(new[] { "governance", "security" }));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Test]
    public async Task Inspect_WithRulesCatalogPath_ReturnsSerializedTestRunJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fab-inspector-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var itemDir = Path.Combine(tempDir, "Sales.Report");
        Directory.CreateDirectory(itemDir);
        File.WriteAllText(Path.Combine(itemDir, ".platform"), "{\"metadata\":{\"type\":\"Report\",\"displayName\":\"Sales\"}}", Encoding.UTF8);

        var rulesPath = Path.Combine(tempDir, "inspect-catalog-rules.json");
        File.WriteAllText(rulesPath, BuildInspectRulesJson(), Encoding.UTF8);

        var catalogPath = Path.Combine(tempDir, "rules-catalog.json");
        File.WriteAllText(catalogPath, BuildRulesCatalogJson("Inspect Catalog", "inspect-catalog-rules.json"), Encoding.UTF8);

        try
        {
            var sut = CreateSut();

            var json = await sut.Inspect(
                fabricItem: itemDir,
                rulesCatalogPath: catalogPath,
                verbose: true,
                authMethod: "local");

            var testRun = JsonSerializer.Deserialize<TestRun>(json);

            Assert.That(testRun, Is.Not.Null);
            Assert.That(testRun!.TestedFilePath, Is.EqualTo(itemDir));
            Assert.That(testRun.RulesCatalogPath, Is.EqualTo(catalogPath));
            Assert.That(testRun.Results, Is.Not.Null);
            Assert.That(testRun.Results.Any(), Is.True);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Test]
    public async Task DiscoverRules_WithRulesCatalogPath_ReturnsSerializedDiscoverRulesJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "fab-inspector-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var itemDir = Path.Combine(tempDir, "Sales.Report");
        Directory.CreateDirectory(itemDir);
        File.WriteAllText(Path.Combine(itemDir, ".platform"), "{\"metadata\":{\"type\":\"Report\",\"displayName\":\"Sales\"}}", Encoding.UTF8);

        var rulesPath = Path.Combine(tempDir, "discover-catalog-rules.json");
        File.WriteAllText(rulesPath, BuildDiscoverRulesJson(), Encoding.UTF8);

        var catalogPath = Path.Combine(tempDir, "rules-catalog.json");
        File.WriteAllText(catalogPath, BuildRulesCatalogJson("Discover Catalog", "discover-catalog-rules.json"), Encoding.UTF8);

        try
        {
            var sut = CreateSut();

            var json = await sut.DiscoverRules(
                fabricItem: itemDir,
                rulesCatalogPath: catalogPath,
                tags: "governance",
                authMethod: "local");

            var discovered = JsonSerializer.Deserialize<DiscoverRulesResponse>(json);

            Assert.That(discovered, Is.Not.Null);
            Assert.That(discovered!.FabricItem, Is.EqualTo(itemDir));
            Assert.That(discovered.RulesCatalogPath, Is.EqualTo(catalogPath));
            Assert.That(discovered.Rules.Any(rule => string.Equals(rule.Name, "Report Rule", StringComparison.Ordinal)), Is.True);
            Assert.That(discovered.Rules.Any(rule => string.Equals(rule.Name, "Json Rule", StringComparison.Ordinal)), Is.False);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private static FabInspectorTools CreateSut()
    {
        return new FabInspectorTools(new NoOpPageRenderer(), []);
    }

    private static string BuildInspectRulesJson()
    {
        return """
{
  "rules": [
    {
      "name": "Always True",
      "itemType": "none",
      "test": [ { "==": [1, 1] }, true ]
    }
  ]
}
""";
    }

    private static string BuildDiscoverRulesJson()
    {
        return """
{
  "rules": [
    {
      "name": "Report Rule",
      "itemType": "report",
      "tags": ["governance", "security"],
      "test": [ { "==": [1, 1] }, true ]
    },
    {
      "name": "Json Rule",
      "itemType": "json",
      "tags": ["ops"],
      "test": [ { "==": [1, 1] }, true ]
    }
  ]
}
""";
    }

        private static string BuildRulesCatalogJson(string ruleSetName, string relativeRulesPath)
        {
                return $$"""
{
    "name": "Test Catalog",
    "ruleSets": [
        { "name": "{{ruleSetName}}", "path": "{{relativeRulesPath}}" }
    ]
}
""";
        }

    private sealed class NoOpPageRenderer : IReportPageWireframeRenderer
    {
        public void DrawReportPages(IEnumerable<TestResult> fieldMapResults, IEnumerable<TestResult> testResults, string outputDir)
        {
        }

        public string ConvertBitmapToBase64(string bitmapPath)
        {
            return string.Empty;
        }
    }
}
