using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Exceptions;
using FabInspector.Core.Output;

namespace FabInspector.ClientLibrary
{
    /// <summary>
    /// Helper that deserialises a rules file from a local path, OneLake, or anonymous HTTPS URL.
    /// Extracted from the legacy <see cref="Main.DeserialiseRulesFromPath"/> so the
    /// per-run <see cref="InspectionEngine"/> can call it with its own
    /// <see cref="ITokenProvider"/> without depending on process-wide static state.
    /// </summary>
    internal static class RulesFileLoader
    {
        public static InspectionRules DeserialiseRulesFromPath(
            string rulesPath,
            ITokenProvider? tokenProvider,
            Action<string>? onProgress = null)
            => DeserialiseRulesFromPathAsync(rulesPath, tokenProvider, onProgress).GetAwaiter().GetResult();

        internal static async Task<InspectionRules> DeserialiseRulesFromPathAsync(
            string rulesPath,
            ITokenProvider? tokenProvider,
            Action<string>? onProgress = null,
            HttpClient? webClient = null)
        {
            var isOneLakeRulesPath = OneLakeRulesFileDownloader.IsOneLakeDfsUrl(rulesPath);
            var isUrl = IsUrlLike(rulesPath);
            var displayPath = PublicRulesFileDownloader.DisplayPath(rulesPath);

            try
            {
                InspectionRules? inspectionRules;
                if (isOneLakeRulesPath)
                {
                    if (tokenProvider == null)
                    {
                        throw new InvalidOperationException(
                            "OneLake rules URL requires authentication. Use -authmethod interactive, azurecli, clientsecret, certificate, federatedtoken, or managedidentity.");
                    }

                    using var rulesStream = await OneLakeRulesFileDownloader
                        .DownloadFileToMemoryStreamAsync(rulesPath, tokenProvider.Credential,
                            onProgress: onProgress ?? (_ => { }))
                        .ConfigureAwait(false);
                    inspectionRules = JsonUtils.Deserialise<InspectionRules>(rulesStream);
                }
                else if (isUrl)
                {
                    using var rulesStream = await PublicRulesFileDownloader.DownloadAsync(rulesPath, onProgress, webClient).ConfigureAwait(false);
                    inspectionRules = JsonUtils.Deserialise<InspectionRules>(rulesStream);
                }
                else
                {
                    inspectionRules = JsonUtils.DeserialiseFromPath<InspectionRules>(rulesPath);
                }

                if (inspectionRules == null || inspectionRules.Rules == null || inspectionRules.Rules.Count == 0)
                {
                    throw new PBIRInspectorException(string.Format("No rule definitions were found within rules file at \"{0}\".", displayPath));
                }

                return inspectionRules;
            }
            catch (System.IO.FileNotFoundException e)
            {
                throw new PBIRInspectorException(string.Format("Rules file with path \"{0}\" not found.", displayPath), e);
            }
            catch (System.Text.Json.JsonException e)
            {
                throw new PBIRInspectorException(string.Format("Could not deserialise rules file with path \"{0}\". Check that the file is valid json and following the correct schema for Fab Inspector rules.", displayPath), e);
            }
            catch (InvalidOperationException e) when (isOneLakeRulesPath)
            {
                throw new PBIRInspectorException(string.Format("Could not load rules file from OneLake URL \"{0}\".", displayPath), e);
            }
        }

        private static bool IsUrlLike(string path)
        {
            var colon = path.IndexOf(':');
            if (colon <= 0 || (colon == 1 && char.IsAsciiLetter(path[0])))
                return false;
            return char.IsAsciiLetter(path[0])
                && path.AsSpan(1, colon - 1).ToString().All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.');
        }
    }
}
