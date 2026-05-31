using System.Text;
using System.Text.Json;
using FabInspector.Web.Workload;
using FabInspector.Web.Workload.Auth;
using FabInspector.Web.Workload.Contracts;
using FabInspector.Web.Workload.Stores;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace FabInspector.Web.Controllers;

/// <summary>
/// Receives item-lifecycle notifications (Create / Update / Delete / Restore)
/// from Fabric for the custom item types declared in fabric-manifest.json.
/// Lifecycle POST/PATCH/PUT/DELETE require the Fabric SubjectAndAppToken1.0
/// scheme; the editor-facing GET additionally accepts the OIDC web-app
/// scheme so the React editor can hydrate from the workload's own cache.
/// </summary>
[ApiController]
[Route("api/workload/items/{itemType}/{workspaceId:guid}/{itemId:guid}")]
[Authorize(AuthenticationSchemes = SubjectAndAppTokenAuthHandler.SchemeName + "," + OpenIdConnectDefaults.AuthenticationScheme)]
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
    [Authorize(AuthenticationSchemes = SubjectAndAppTokenAuthHandler.SchemeName)]
    public async Task<IActionResult> Create(string itemType, Guid workspaceId, Guid itemId, [FromBody] ItemDefinitionEnvelope envelope, CancellationToken ct)
    {
        if (!ValidateItemType(itemType, out var problem)) return problem!;
        if (!ValidateDefinition(itemType, envelope, out problem)) return problem!;

        LogContext("create", itemType, workspaceId, itemId);
        try
        {
            // Fabric may retry — UpsertAsync is idempotent; no If-Match on create.
            await _store.UpsertAsync(itemType, workspaceId, itemId, envelope, ifMatch: null, ct).ConfigureAwait(false);
        }
        catch (ETagMismatchException ex)
        {
            return ETagMismatch(ex.Message);
        }
        return NoContent();
    }

    [HttpPatch]
    [HttpPut]
    [Authorize(AuthenticationSchemes = SubjectAndAppTokenAuthHandler.SchemeName)]
    public async Task<IActionResult> Update(string itemType, Guid workspaceId, Guid itemId, [FromBody] ItemDefinitionEnvelope envelope, CancellationToken ct)
    {
        if (!ValidateItemType(itemType, out var problem)) return problem!;
        if (!ValidateDefinition(itemType, envelope, out problem)) return problem!;

        LogContext("update", itemType, workspaceId, itemId);
        var ifMatch = Request.Headers[HeaderNames.IfMatch].ToString();
        if (string.IsNullOrEmpty(ifMatch)) ifMatch = null;
        try
        {
            await _store.UpsertAsync(itemType, workspaceId, itemId, envelope, ifMatch, ct).ConfigureAwait(false);
        }
        catch (ETagMismatchException ex)
        {
            return ETagMismatch(ex.Message);
        }
        return NoContent();
    }

    [HttpDelete]
    [Authorize(AuthenticationSchemes = SubjectAndAppTokenAuthHandler.SchemeName)]
    public async Task<IActionResult> Delete(string itemType, Guid workspaceId, Guid itemId, [FromBody] DeleteItemRequest? request, CancellationToken ct)
    {
        if (!ValidateItemType(itemType, out var problem)) return problem!;

        var hard = !string.Equals(request?.DeleteType, "Soft", StringComparison.OrdinalIgnoreCase);
        LogContext(hard ? "delete-hard" : "delete-soft", itemType, workspaceId, itemId);
        await _store.DeleteAsync(itemType, workspaceId, itemId, hard, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("restore")]
    [Authorize(AuthenticationSchemes = SubjectAndAppTokenAuthHandler.SchemeName)]
    public async Task<IActionResult> Restore(string itemType, Guid workspaceId, Guid itemId, [FromBody] ItemDefinitionEnvelope? envelope, CancellationToken ct)
    {
        if (!ValidateItemType(itemType, out var problem)) return problem!;

        LogContext("restore", itemType, workspaceId, itemId);
        await _store.RestoreAsync(itemType, workspaceId, itemId, envelope, ct).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>
    /// Read back the cached item definition. Not part of the Fabric lifecycle
    /// contract — used by the workload's own React editor pages to hydrate.
    /// Accepts either the SubjectAndAppToken (Fabric-embedded calls) or the
    /// OIDC web-app bearer (direct editor calls).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(string itemType, Guid workspaceId, Guid itemId, CancellationToken ct)
    {
        if (!ValidateItemType(itemType, out var problem)) return problem!;
        var stored = await _store.GetAsync(itemType, workspaceId, itemId, ct).ConfigureAwait(false);
        if (stored == null)
        {
            return NotFound(new ErrorResponse("NotFound", $"Item {itemType}/{workspaceId}/{itemId} not found."));
        }
        if (!string.IsNullOrEmpty(stored.Value.ETag))
        {
            Response.Headers[HeaderNames.ETag] = stored.Value.ETag;
        }
        return Ok(stored.Value.Envelope);
    }

    private bool ValidateItemType(string itemType, out IActionResult? problem)
    {
        if (!WorkloadItemTypes.IsKnown(itemType))
        {
            problem = BadRequest(new ErrorResponse("UnknownItemType", $"Unknown item type '{itemType}'."));
            return false;
        }
        problem = null;
        return true;
    }

    private bool ValidateDefinition(string itemType, ItemDefinitionEnvelope? envelope, out IActionResult? problem)
    {
        if (envelope?.Definition?.Parts == null || envelope.Definition.Parts.Count == 0)
        {
            problem = BadRequest(new ErrorResponse("InvalidDefinition", "Item definition must include at least one part."));
            return false;
        }

        var expectedPart = itemType == WorkloadItemTypes.RuleSet
            ? WorkloadItemTypes.Parts.RulesJson
            : WorkloadItemTypes.Parts.CatalogJson;

        var part = envelope.Definition.Parts.FirstOrDefault(p =>
            string.Equals(p.Path, expectedPart, StringComparison.OrdinalIgnoreCase));
        if (part == null)
        {
            problem = BadRequest(new ErrorResponse("InvalidDefinition", $"Item definition for '{itemType}' must contain a '{expectedPart}' part."));
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(part.Payload);
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
        }
        catch (FormatException)
        {
            problem = BadRequest(new ErrorResponse("InvalidPayload", $"'{expectedPart}' payload is not valid Base64."));
            return false;
        }
        catch (JsonException ex)
        {
            problem = BadRequest(new ErrorResponse("InvalidPayload", $"'{expectedPart}' payload is not valid JSON: {ex.Message}"));
            return false;
        }

        problem = null;
        return true;
    }

    private IActionResult ETagMismatch(string message) =>
        StatusCode(StatusCodes.Status412PreconditionFailed,
            new ErrorResponse("ETagMismatch", message, isPermanent: false));

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
