using System.Linq;

namespace FabInspector.ClientLibrary.Utils
{
    internal static class RulesCatalogAuthHelper
    {
        public static bool CatalogContainsEnabledOneLakeRuleSets(string? rulesCatalogPath)
        {
            if (string.IsNullOrWhiteSpace(rulesCatalogPath))
            {
                return false;
            }

            if (OneLakeRulesFileDownloader.IsOneLakeDfsUrl(rulesCatalogPath))
            {
                return true;
            }

            if (!File.Exists(rulesCatalogPath))
            {
                return false;
            }

            try
            {
                var catalog = JsonUtils.DeserialiseFromPath<RulesCatalog>(rulesCatalogPath);
                return catalog?.RuleSets?.Any(reference => !reference.Disabled && RuleSetReference.DetectType(reference.Type, reference.Path) == "onelake") == true;
            }
            catch
            {
                return false;
            }
        }
    }
}