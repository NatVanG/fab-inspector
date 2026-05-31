using System.Text;
using System.Text.Json;
using FabInspector.Web.Workload;
using FabInspector.Web.Workload.Auth;
using FabInspector.Web.Workload.Contracts;
using FabInspector.Web.Workload.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FabInspector.Web.Controllers;

/// <summary>
/// Receives item-lifecycle notifications (Create / Update / Delete / Restore)
/// from Fabric for the custom item types declared in fabric-manifest.json.
/// </summary>
[ApiController]
[Route("api/workload/items/{itemType}/{workspaceId:guid}/{itemId:guid}")]
[Authorize(AuthenticationSchemes = SubjectAndAppTokenAuthHandler.SchemeName)]
public sealed class ItemLifecycleController : ControllerBase
{
    private readonly IItemDefinitionStore _store;
    private readonly ILogger<ItemLifecycleController> _logger;

    public ItemLifecycleController(IItemDefinitionStore store, ILogger<ItemLifecycleController> logger)
    {
        _store = store;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Create(string itemType, Guid workspaceId, Guid itemId, [FromBody] ItemDefinitionEnvelope envelope, CancellationToken ct)
    {
        if (!ValidateItemType(itemType, out var problem)) return problem!;
        if (!ValidateDefinition(itemType, envelope, out problem)) return problem!;

        LogContext("create", itemType, workspaceId, itemId);
        await _store.UpsertAsync(itemType, workspaceId, itemId, envelope, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPatch]
    [HttpPut]
    public async Task<IActionResult> Update(string itemType, Guid workspaceId, Guid itemId, [FromBody] ItemDefinitionEnvelope envelope, CancellationToken ct)
    {
        if (!ValidateItemType(itemType, out var problem)) return problem!;
        if (!ValidateDefinition(itemType, envelope, out problem)) return problem!;

        LogContext("update", itemType, workspaceId, itemId);
        await _store.UpsertAsync(itemType, workspaceId, itemId, envelope, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(string itemType, Guid workspaceId, Guid itemId, [FromBody] DeleteItemRequest? request, CancellationToken ct)
    {
        if (!ValidateItemType(itemType, out var problem)) return problem!;

        var hard = !string.Equals(request?.DeleteType, "Soft", StringComparison.OrdinalIgnoreCase);
        LogContext(hard ? "delete-hard" : "delete-soft", itemType, workspaceId, itemId);
        await _store.DeleteAsync(itemType, workspaceId, itemId, hard, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("restore")]
    public async Task<IActionResult> Restore(string itemType, Guid workspaceId, Guid itemId, [FromBody] ItemDefinitionEnvelope? envelope, CancellationToken ct)
    {
        if (!ValidateItemType(itemType, out var problem)) return problem!;

        LogContext("restore", itemType, workspaceId, itemId);
        await _store.RestoreAsync(itemType, workspaceId, itemId, envelope, ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Read back the cached item definition. Not part of the Fabric lifecycle
    /// contract — used by the workload's own editor pages to hydrate.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(string itemType, Guid workspaceId, Guid itemId, CancellationToken ct)
    {
        if (!ValidateItemType(itemType, out var problem)) return problem!;
        var envelope = await _store.GetAsync(itemType, workspaceId, itemId, ct).ConfigureAwait(false);
        if (envelope == null) return NotFound();
        return Ok(envelope);
    }

    private bool ValidateItemType(string itemType, out IActionResult? problem)
    {
        if (!WorkloadItemTypes.IsKnown(itemType))
        {
            problem = BadRequest(new { error = $"Unknown item type '{itemType}'." });
            return false;
        }
        problem = null;
        return true;
    }

    private bool ValidateDefinition(string itemType, ItemDefinitionEnvelope? envelope, out IActionResult? problem)
    {
        if (envelope?.Definition?.Parts == null || envelope.Definition.Parts.Count == 0)
        {
            problem = BadRequest(new { error = "Item definition must include at least one part." });
            return false;
        }

        var expectedPart = itemType == WorkloadItemTypes.RuleSet
            ? WorkloadItemTypes.Parts.RulesJson
            : WorkloadItemTypes.Parts.CatalogJson;

        var part = envelope.Definition.Parts.FirstOrDefault(p =>
            string.Equals(p.Path, expectedPart, StringComparison.OrdinalIgnoreCase));
        if (part == null)
        {
            problem = BadRequest(new { error = $"Item definition for '{itemType}' must contain a '{expectedPart}' part." });
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(part.Payload);
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
        }
        catch (FormatException)
        {
            problem = BadRequest(new { error = $"'{expectedPart}' payload is not valid Base64." });
            return false;
        }
        catch (JsonException ex)
        {
            problem = BadRequest(new { error = $"'{expectedPart}' payload is not valid JSON: {ex.Message}" });
            return false;
        }

        problem = null;
        return true;
    }

    private void LogContext(string op, string itemType, Guid workspaceId, Guid itemId)
    {
        var activityId = Request.Headers["ActivityId"].ToString();
        var requestId = Request.Headers["RequestId"].ToString();
        var tenantId = Request.Headers["x-ms-client-tenant-id"].ToString();
        _logger.LogInformation(
            "Workload lifecycle {Op} itemType={ItemType} workspaceId={WorkspaceId} itemId={ItemId} activityId={ActivityId} requestId={RequestId} tenantId={TenantId}",
            op, itemType, workspaceId, itemId, activityId, requestId, tenantId);
    }
}
