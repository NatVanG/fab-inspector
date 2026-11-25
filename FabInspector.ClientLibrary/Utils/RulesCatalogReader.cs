using FabInspector.Core;
using FabInspector.Core.Exceptions;
using System.Text.Json;

namespace FabInspector.ClientLibrary.Utils
{
    internal sealed class RulesCatalogReader
    {
        private readonly ITokenProvider? _tokenProvider;
        private readonly Action<string>? _onProgress;
        private readonly HttpClient? _webClient;

        public RulesCatalogReader(ITokenProvider? tokenProvider, Action<string>? onProgress = null, HttpClient? webClient = null)
        {
            _tokenProvider = tokenProvider;
            _onProgress = onProgress;
            _webClient = webClient;
        }

        public async Task<IReadOnlyList<ResolvedRuleSet>> ReadResolvedRuleSetsAsync(string catalogPath)
        {
            if (string.IsNullOrWhiteSpace(catalogPath))
            {
                throw new PBIRInspectorException("Rules catalog path cannot be empty.");
            }

            var catalog = await ReadCatalogAsync(catalogPath).ConfigureAwait(false);
            if (catalog == null || catalog.RuleSets == null || catalog.RuleSets.Count == 0)
            {
                throw new PBIRInspectorException($"No ruleset references were found within rules catalog at '{catalogPath}'.");
            }

            var resolvedRuleSets = new List<ResolvedRuleSet>();
            foreach (var reference in catalog.RuleSets)
            {
                if (string.IsNullOrWhiteSpace(reference.Name))
                {
                    throw new PBIRInspectorException($"Rules catalog '{catalogPath}' contains a ruleset with no name.");
                }

                if (string.IsNullOrWhiteSpace(reference.Path))
                {
                    throw new PBIRInspectorException($"Ruleset '{reference.Name}' in catalog '{catalogPath}' has an empty path.");
                }

                if (reference.Disabled)
                {
                    _onProgress?.Invoke($"Skipping disabled ruleset '{reference.Name}' from catalog '{catalogPath}'.");
                    continue;
                }

                string type;
                try
                {
                    type = RuleSetReference.DetectType(reference.Type, reference.Path);
                    reference.Type = type;
                }
                catch (InvalidOperationException ex)
                {
                    throw new PBIRInspectorException($"Ruleset '{reference.Name}' in catalog '{catalogPath}' has an invalid type.", ex);
                }

                var resolvedPath = ResolveRuleSetPath(catalogPath, reference);
                var rules = await ReadRulesFromReferenceAsync(type, resolvedPath).ConfigureAwait(false);
                if (rules == null || rules.Rules == null || rules.Rules.Count == 0)
                {
                    throw new PBIRInspectorException($"No rule definitions were found in ruleset '{reference.Name}' at '{PublicRulesFileDownloader.DisplayPath(resolvedPath)}'.");
                }

                resolvedRuleSets.Add(new ResolvedRuleSet
                {
                    Name = reference.Name,
                    SourcePath = PublicRulesFileDownloader.DisplayPath(resolvedPath),
                    Rules = rules
                });
            }

            if (resolvedRuleSets.Count == 0)
            {
                throw new PBIRInspectorException($"All rulesets in catalog '{catalogPath}' are disabled or invalid.");
            }

            return resolvedRuleSets;
        }

        private async Task<RulesCatalog?> ReadCatalogAsync(string catalogPath)
        {
            try
            {
                if (OneLakeRulesFileDownloader.IsOneLakeDfsUrl(catalogPath))
                {
                    if (_tokenProvider == null)
                    {
                        throw new InvalidOperationException("OneLake rules catalog URL requires authentication.");
                    }

                    using var stream = await OneLakeRulesFileDownloader.DownloadFileToMemoryStreamAsync(
                        catalogPath,
                        _tokenProvider.Credential,
                        onProgress: _onProgress).ConfigureAwait(false);

                    return JsonUtils.Deserialise<RulesCatalog>(stream);
                }

                return JsonUtils.DeserialiseFromPath<RulesCatalog>(catalogPath);
            }
            catch (FileNotFoundException ex)
            {
                throw new PBIRInspectorException($"Rules catalog path '{catalogPath}' was not found.", ex);
            }
            catch (JsonException ex)
            {
                throw new PBIRInspectorException($"Could not deserialize rules catalog at '{catalogPath}'. Check the JSON format.", ex);
            }
            catch (InvalidOperationException ex) when (OneLakeRulesFileDownloader.IsOneLakeDfsUrl(catalogPath))
            {
                throw new PBIRInspectorException($"Could not load rules catalog from OneLake URL '{catalogPath}'.", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new PBIRInspectorException($"Rules catalog at '{catalogPath}' contains an invalid ruleset definition.", ex);
            }
        }

        private async Task<InspectionRules?> ReadRulesFromReferenceAsync(string type, string resolvedPath)
        {
            return type switch
            {
                "onelake" => await ReadOneLakeRulesAsync(resolvedPath).ConfigureAwait(false),
                "url" => await ReadUrlRulesAsync(resolvedPath).ConfigureAwait(false),
                _ => JsonUtils.DeserialiseFromPath<InspectionRules>(resolvedPath)
            };
        }

        private async Task<InspectionRules?> ReadOneLakeRulesAsync(string resolvedPath)
        {
            if (_tokenProvider == null)
            {
                throw new PBIRInspectorException(
                    $"OneLake ruleset URL '{resolvedPath}' requires authentication. Use -authmethod interactive, azurecli, clientsecret, certificate, federatedtoken, or managedidentity.");
            }

            using var rulesStream = await OneLakeRulesFileDownloader.DownloadFileToMemoryStreamAsync(
                resolvedPath,
                _tokenProvider.Credential,
                onProgress: _onProgress).ConfigureAwait(false);

            return JsonUtils.Deserialise<InspectionRules>(rulesStream);
        }

        private async Task<InspectionRules?> ReadUrlRulesAsync(string resolvedPath)
        {
            try
            {
                using var stream = await PublicRulesFileDownloader.DownloadAsync(resolvedPath, _onProgress, _webClient).ConfigureAwait(false);
                return JsonUtils.Deserialise<InspectionRules>(stream);
            }
            catch (JsonException ex)
            {
                throw new PBIRInspectorException($"Could not deserialize ruleset at '{PublicRulesFileDownloader.DisplayPath(resolvedPath)}'. Check the JSON format.", ex);
            }
        }

        private static string ResolveRuleSetPath(string catalogPath, RuleSetReference reference)
        {
            var rawPath = reference.Path.Trim();

            if (reference.Type == "onelake")
            {
                if (OneLakeRulesFileDownloader.IsOneLakeDfsUrl(rawPath))
                {
                    return rawPath;
                }

                if (OneLakeRulesFileDownloader.IsOneLakeDfsUrl(catalogPath))
                {
                    return CombineOneLakePath(catalogPath, rawPath);
                }

                throw new PBIRInspectorException($"Ruleset '{reference.Name}' is marked as OneLake but path '{rawPath}' is not a valid OneLake DFS URL.");
            }

            if (reference.Type == "url")
            {
                return rawPath;
            }

            if (Path.IsPathRooted(rawPath))
            {
                return rawPath;
            }

            if (Uri.TryCreate(rawPath, UriKind.Absolute, out _))
            {
                return rawPath;
            }

            if (OneLakeRulesFileDownloader.IsOneLakeDfsUrl(catalogPath))
            {
                throw new PBIRInspectorException($"Ruleset '{reference.Name}' uses a relative local path '{rawPath}', but the catalog is hosted on OneLake. Use absolute paths or OneLake URLs for remote catalogs.");
            }

            var catalogDirectory = Path.GetDirectoryName(catalogPath) ?? string.Empty;
            return Path.GetFullPath(Path.Combine(catalogDirectory, rawPath));
        }

        private static string CombineOneLakePath(string catalogPath, string childPath)
        {
            var baseUri = new Uri(catalogPath);
            var absoluteChild = new Uri(baseUri, childPath);
            return absoluteChild.ToString();
        }
    }
}
