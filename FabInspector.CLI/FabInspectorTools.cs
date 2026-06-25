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

    //[McpServerTool(Name = "inspect"), Description("Run Fabric Inspector rules against a Power BI / Fabric item and return structured inspection results as JSON.")]
    //public async Task<string> Inspect(
    //    [Description("Path to a local folder containing Fabric item definitions (e.g. .pbip, .Report folder), or a Fabric item GUID when used with fabricWorkspaceId.")] string fabricItem,
    //    [Description("Path to the rules file (JSON) or a OneLake DFS URL pointing to the rules file.")] string rules,
    //    [Description("Enable verbose output to include passing results. Default: false.")] bool verbose = false,
    //    [Description("Enable parallel rule processing. Not supported with remote authentication. Default: false.")] bool parallel = false,
    //    [Description("Authentication method. Valid: local, interactive, azurecli, clientsecret, certificate, federatedtoken, managedidentity. Default: local.")] string authMethod = "local",
    //    [Description("Azure tenant ID. Required for clientsecret, certificate, and federatedtoken auth methods.")] string? tenantId = null,
    //    [Description("Azure AD application (client) ID. Required for clientsecret, certificate, and federatedtoken auth methods.")] string? clientId = null,
    //    [Description("Azure AD client secret. Required for clientsecret auth method.")] string? clientSecret = null,
    //    [Description("Fabric workspace ID (GUID). Requires authentication.")] string? fabricWorkspaceId = null)
    //{
    //    var args = new Args
    //    {
    //        FabricItem = fabricItem,
    //        RulesFilePath = rules,
    //        VerboseString = verbose.ToString(),
    //        ParallelString = parallel.ToString(),
    //        AuthMethod = authMethod,
    //        TenantId = tenantId ?? Environment.GetEnvironmentVariable("FABRIC_TENANT_ID"),
    //        ClientId = clientId ?? Environment.GetEnvironmentVariable("FABRIC_CLIENT_ID"),
    //        ClientSecret = clientSecret ?? Environment.GetEnvironmentVariable("FABRIC_CLIENT_SECRET"),
    //        FabricWorkspaceId = fabricWorkspaceId,
    //        OutputPath = string.Empty,
    //        FormatsString = string.Empty
    //    };

    //    var testRun = await FabInspector.ClientLibrary.Main.RunAndReturnResultsAsync(args, _pageRenderer, _registries);

    //    return JsonSerializer.Serialize(testRun, new JsonSerializerOptions { WriteIndented = true });
    //}

    [McpServerTool(Name = "inspect"), Description("Run Fabric Inspector rules against a Power BI / Fabric item and return structured inspection results as JSON.")]
    public async Task<string> Inspect(
        [Description("Path to a local folder containing Fabric item definitions (e.g. .pbip, .Report folder), or a Fabric item GUID when used with fabricWorkspaceId.")] string fabricItem,
        [Description("Path to the rules file (JSON) or a OneLake DFS URL pointing to the rules file.")] string rules,
        [Description("Enable verbose output to include passing results. Default: false.")] bool verbose = false,
        [Description("Authentication method. Valid: local, interactive, azurecli. Default: local.")] string authMethod = "local",
        [Description("Fabric workspace ID (GUID). Requires authentication.")] string? fabricWorkspaceId = null)
    {
        var args = new Args
        {
            FabricItem = fabricItem,
            RulesFilePath = rules,
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
        [Description("Path to the rules file (JSON) or a OneLake DFS URL pointing to the rules file.")] string rules,
        [Description("Optional comma-separated rule tags. When provided, returns rules containing any matching tag.")] string tags = "",
        [Description("Authentication method. Valid: local, interactive, azurecli. Default: local.")] string authMethod = "local",
        [Description("Fabric workspace ID (GUID). Requires authentication.")] string? fabricWorkspaceId = null)
    {
        var args = new Args
        {
            FabricItem = fabricItem,
            RulesFilePath = rules,
            AuthMethod = authMethod,
            FabricWorkspaceId = fabricWorkspaceId,
            OutputPath = string.Empty,
            FormatsString = string.Empty
        };

        var discoveredRules = await FabInspector.ClientLibrary.Main.DiscoverRulesAsync(args, tags);

        return JsonSerializer.Serialize(discoveredRules, new JsonSerializerOptions { WriteIndented = true });
    }
}
