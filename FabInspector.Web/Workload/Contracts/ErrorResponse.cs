using System.Text.Json.Serialization;

namespace FabInspector.Web.Workload.Contracts;

/// <summary>
/// Structured error contract returned by the workload backend, modelled on the
/// Fabric Workload <c>ErrorResponse</c> shape so the portal can surface
/// blocking decisions and toast strings consistently.
/// </summary>
/// <remarks>
/// Canonical <c>ErrorCode</c> values used by this workload:
/// <list type="bullet">
/// <item><c>UnknownItemType</c> — request targeted an item type not declared by this workload.</item>
/// <item><c>InvalidDefinition</c> — item definition envelope is malformed or missing required parts.</item>
/// <item><c>InvalidPayload</c> — base64 / JSON decoding of a definition part failed.</item>
/// <item><c>Unauthenticated</c> — request did not present a valid SubjectAndAppToken / OIDC bearer.</item>
/// <item><c>Forbidden</c> — caller authenticated but lacks permission on the target item.</item>
/// <item><c>ConsentRequired</c> — OBO exchange failed because the user has not consented to a downstream scope.</item>
/// <item><c>ETagMismatch</c> — supplied <c>If-Match</c> ETag does not match the stored definition.</item>
/// <item><c>Conflict</c> — duplicate create / job instance id.</item>
/// <item><c>NotFound</c> — item or job instance does not exist.</item>
/// <item><c>Internal</c> — unhandled server-side failure.</item>
/// </list>
/// </remarks>
public sealed class ErrorResponse
{
    public ErrorResponse() { }

    public ErrorResponse(string errorCode, string message, string source = "FabInspector.Workload", bool isPermanent = true, string? moreDetails = null)
    {
        ErrorCode = errorCode;
        Message = message;
        Source = source;
        IsPermanent = isPermanent;
        MoreDetails = moreDetails;
    }

    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = "Internal";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = "FabInspector.Workload";

    [JsonPropertyName("isPermanent")]
    public bool IsPermanent { get; set; }

    [JsonPropertyName("moreDetails")]
    public string? MoreDetails { get; set; }
}
