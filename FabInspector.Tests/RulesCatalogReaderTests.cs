using FabInspector.ClientLibrary.Utils;
using FabInspector.Core.Exceptions;

namespace FabInspector.Tests
{
    public class RulesCatalogReaderTests
    {
        [Test]
        public async Task ReadResolvedRuleSetsAsync_LoadsEnabledRuleSetsAndSkipsDisabled()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fab-inspector-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var baseRulesPath = Path.Combine(tempDir, "base-rules.json");
                var localRulesPath = Path.Combine(tempDir, "local-rules.json");
                var disabledRulesPath = Path.Combine(tempDir, "disabled-rules.json");
                var catalogPath = Path.Combine(tempDir, "rules-catalog.json");

                File.WriteAllText(baseRulesPath, BuildSimpleRulesJson("Base Rule"));
                File.WriteAllText(localRulesPath, BuildSimpleRulesJson("Local Rule"));
                File.WriteAllText(disabledRulesPath, BuildSimpleRulesJson("Disabled Rule"));

                File.WriteAllText(catalogPath, @"{
  ""name"": ""Enterprise Catalog"",
  ""ruleSets"": [
    { ""name"": ""Base"", ""path"": ""base-rules.json"" },
    { ""name"": ""Disabled"", ""path"": ""disabled-rules.json"", ""disabled"": true },
    { ""name"": ""Local"", ""type"": ""local"", ""path"": ""local-rules.json"" }
  ]
}");

                var reader = new RulesCatalogReader(null, new HttpClient());
                var resolved = await reader.ReadResolvedRuleSetsAsync(catalogPath);

                Assert.That(resolved.Count, Is.EqualTo(2));
                Assert.That(resolved[0].Name, Is.EqualTo("Base"));
                Assert.That(resolved[1].Name, Is.EqualTo("Local"));
                Assert.That(resolved[0].SourcePath, Is.EqualTo(baseRulesPath));
                Assert.That(resolved[1].SourcePath, Is.EqualTo(localRulesPath));
                Assert.That(resolved.All(_ => _.Rules.Rules.Count == 1));
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
        public void ReadResolvedRuleSetsAsync_ThrowsForUnsupportedRuleSetType()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "fab-inspector-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var catalogPath = Path.Combine(tempDir, "rules-catalog.json");
                File.WriteAllText(catalogPath, @"{
  ""name"": ""Enterprise Catalog"",
  ""ruleSets"": [
    { ""name"": ""InvalidType"", ""type"": ""blob"", ""path"": ""rules.json"" }
  ]
}");

                var reader = new RulesCatalogReader(null, new HttpClient());

                var ex = Assert.ThrowsAsync<PBIRInspectorException>(async () => await reader.ReadResolvedRuleSetsAsync(catalogPath));
                Assert.That(ex!.Message.Contains("has an invalid type"));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private static string BuildSimpleRulesJson(string ruleName)
        {
            return $@"{{
  ""rules"": [
    {{
      ""name"": ""{ruleName}"",
      ""itemType"": ""none"",
      ""test"": [
        {{ ""=="": [1, 1] }},
        true
      ]
    }}
  ]
}}";
        }
    }
}
