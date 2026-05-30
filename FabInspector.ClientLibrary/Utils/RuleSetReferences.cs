using FabInspector.Core;

namespace FabInspector.ClientLibrary.Utils
{
    public sealed class RuleSetReference : IRuleSet
    {
        public string Name { get; set; } = string.Empty;

        public bool Disabled { get; set; } = false;

        public string Path { get; set; } = string.Empty;

        public string? Type { get; set; }
        public static string DetectType(string? type, string path)
        {
            if (!string.IsNullOrWhiteSpace(type))
            {
                var normalisedType = type.Trim().ToLowerInvariant();
                return normalisedType switch
                {
                    "local" or "file" => "local",
                    "onelake" => "onelake",
                    "url" or "http" or "https" => "url",
                    _ => throw new InvalidOperationException($"Unsupported ruleset type '{type}'.")
                };
            }

            if (OneLakeRulesFileDownloader.IsOneLakeDfsUrl(path))
            {
                return "onelake";
            }

            if (Uri.TryCreate(path, UriKind.Absolute, out var uri)
                && (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                    || uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
            {
                return "url";
            }

            return "local";
        }
    }
}
