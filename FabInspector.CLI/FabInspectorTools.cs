using System.ComponentModel;
using System.Text.Json;
using FabInspector.ClientLibrary;
using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Output;
using ModelContextProtocol.Server;

[McpServerToolType]
public class FabInspectorTools
{
    private readonly IReportPageWireframeRenderer _pageRenderer;
    private readonly IEnumerable<JsonLogicOperatorRegistry> _registries;

    public FabInspectorTools(IReportPageWireframeRenderer pageRenderer, IEnumerable<JsonLogicOperatorRegistry> registries)
    {
        _pageRenderer = pageRenderer;
        _registries = registries;
    }
    
    [McpServerTool(Name = "inspect"), Description("Run Fabric Inspector rules against a Power BI / Fabric item and return structured inspection results as JSON.")]
    public async Task<string> Inspect(
        [Description("Path to a local folder containing Fabric item definitions (e.g. .pbip, .Report folder), or a Fabric item GUID when used with fabricWorkspaceId.")] string fabricItem,
        [Description("Path to the rules file (JSON) or a OneLake DFS URL pointing to the rules file. Provide exactly one of 'rules' or 'rulesCatalogPath'.")] string? rules = null,
        [Description("Path to the rules catalog file (JSON) or a OneLake DFS URL pointing to the rules catalog. Provide exactly one of 'rules' or 'rulesCatalogPath'.")] string? rulesCatalogPath = null,
        [Description("Enable verbose output to include passing results. Default: false.")] bool verbose = false,
        [Description("Authentication method. Valid: local, interactive, azurecli. Default: local.")] string authMethod = "local",
        [Description("Fabric workspace ID (GUID). Requires authentication.")] string? fabricWorkspaceId = null)
    {
        ValidateRulesInput(rules, rulesCatalogPath);

        var args = new Args
        {
            FabricItem = fabricItem,
            RulesFilePath = rules ?? string.Empty,
            RulesCatalogPath = rulesCatalogPath ?? string.Empty,
            VerboseString = verbose.ToString(),
            AuthMethod = authMethod,
            FabricWorkspaceId = fabricWorkspaceId,
            OutputPath = string.Empty,
            FormatsString = string.Empty
        };

        var testRun = await FabInspector.ClientLibrary.Main.RunAndReturnResultsAsync(args, _pageRenderer, _registries);

        return JsonSerializer.Serialize(testRun, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "discover_rules"), Description("Discover applicable Fabric Inspector guardrails for a Power BI / Fabric item and return planning metadata as JSON.")]
    public async Task<string> DiscoverRules(
        [Description("Path to a local folder containing Fabric item definitions (e.g. .pbip, .Report folder), or a Fabric item GUID when used with fabricWorkspaceId.")] string fabricItem,
        [Description("Path to the rules file (JSON) or a OneLake DFS URL pointing to the rules file. Provide exactly one of 'rules' or 'rulesCatalogPath'.")] string? rules = null,
        [Description("Path to the rules catalog file (JSON) or a OneLake DFS URL pointing to the rules catalog. Provide exactly one of 'rules' or 'rulesCatalogPath'.")] string? rulesCatalogPath = null,
        [Description("Optional comma-separated rule tags. When provided, returns rules containing any matching tag.")] string tags = "",
        [Description("Authentication method. Valid: local, interactive, azurecli. Default: local.")] string authMethod = "local",
        [Description("Fabric workspace ID (GUID). Requires authentication.")] string? fabricWorkspaceId = null)
    {
        ValidateRulesInput(rules, rulesCatalogPath);

        var args = new Args
        {
            FabricItem = fabricItem,
            RulesFilePath = rules ?? string.Empty,
            RulesCatalogPath = rulesCatalogPath ?? string.Empty,
            AuthMethod = authMethod,
            FabricWorkspaceId = fabricWorkspaceId,
            OutputPath = string.Empty,
            FormatsString = string.Empty
        };

        var discoveredRules = await FabInspector.ClientLibrary.Main.DiscoverRulesAsync(args, tags);

        return JsonSerializer.Serialize(discoveredRules, new JsonSerializerOptions { WriteIndented = true });
    }

    private static void ValidateRulesInput(string? rules, string? rulesCatalogPath)
    {
        var hasRules = !string.IsNullOrWhiteSpace(rules);
        var hasRulesCatalog = !string.IsNullOrWhiteSpace(rulesCatalogPath);

        if (hasRules == hasRulesCatalog)
        {
            throw new ArgumentException("Exactly one of 'rules' or 'rulesCatalogPath' must be provided.");
        }
    }
}
