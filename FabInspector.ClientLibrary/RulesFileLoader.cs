using FabInspector.ClientLibrary.Utils;
using FabInspector.Core;
using FabInspector.Core.Exceptions;
using FabInspector.Core.Output;

namespace FabInspector.ClientLibrary
{
    /// <summary>
    /// Helper that deserialises a rules file from a local path or a OneLake DFS URL.
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
        {
            var isOneLakeRulesPath = OneLakeRulesFileDownloader.IsOneLakeDfsUrl(rulesPath);

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

                    using var rulesStream = OneLakeRulesFileDownloader
                        .DownloadFileToMemoryStreamAsync(rulesPath, tokenProvider.Credential,
                            onProgress: onProgress ?? (_ => { }))
                        .GetAwaiter()
                        .GetResult();
                    inspectionRules = JsonUtils.Deserialise<InspectionRules>(rulesStream);
                }
                else
                {
                    inspectionRules = JsonUtils.DeserialiseFromPath<InspectionRules>(rulesPath);
                }

                if (inspectionRules == null || inspectionRules.Rules == null || inspectionRules.Rules.Count == 0)
                {
                    throw new PBIRInspectorException(string.Format("No rule definitions were found within rules file at \"{0}\".", rulesPath));
                }

                return inspectionRules;
            }
            catch (System.IO.FileNotFoundException e)
            {
                throw new PBIRInspectorException(string.Format("Rules file with path \"{0}\" not found.", rulesPath), e);
            }
            catch (System.Text.Json.JsonException e)
            {
                throw new PBIRInspectorException(string.Format("Could not deserialise rules file with path \"{0}\". Check that the file is valid json and following the correct schema for Fab Inspector rules.", rulesPath), e);
            }
            catch (InvalidOperationException e) when (isOneLakeRulesPath)
            {
                throw new PBIRInspectorException(string.Format("Could not load rules file from OneLake URL \"{0}\".", rulesPath), e);
            }
        }
    }
}
