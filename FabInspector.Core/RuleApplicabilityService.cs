namespace FabInspector.Core
{
    public static class RuleApplicabilityService
    {
        private const string DeprecatedSuffix = "_deprecated";

        public static IEnumerable<Rule> FilterDisabledRules(IEnumerable<Rule> rules)
        {
            return rules.Where(rule => !rule.Disabled);
        }

        public static IEnumerable<string>? GetScopedItemTypes(IEnumerable<Rule> rules)
        {
            var scopedItemTypes = rules
                .SelectMany(rule => SplitItemTypes(rule.ItemType))
                .Where(itemType => !string.IsNullOrWhiteSpace(itemType))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return scopedItemTypes.Count == 0 ? null : scopedItemTypes;
        }

        public static bool MatchesTags(Rule rule, HashSet<string> requestedTags)
        {
            if (requestedTags.Count == 0)
            {
                return true;
            }

            if (rule.Tags == null || rule.Tags.Count == 0)
            {
                return false;
            }

            return rule.Tags.Any(tag => requestedTags.Contains(tag));
        }

        public static bool IsApplicableToTargetItemTypes(Rule rule, IReadOnlySet<string> targetItemTypes)
        {
            return TryDescribeInclusionReason(rule, targetItemTypes, out _);
        }

        public static bool TryDescribeInclusionReason(Rule rule, IReadOnlySet<string> targetItemTypes, out string inclusionReason)
        {
            var itemTypes = SplitItemTypes(rule.ItemType).ToList();

            if (itemTypes.Count == 0)
            {
                inclusionReason = "Rule has no item type constraint.";
                return false;
            }

            if (itemTypes.Any(itemType => itemType.Equals("*", StringComparison.OrdinalIgnoreCase)))
            {
                inclusionReason = "Rule applies to all item types via wildcard '*'.";
                return true;
            }

            foreach (var itemType in itemTypes)
            {
                if (targetItemTypes.Contains(itemType))
                {
                    inclusionReason = itemTypes.Count == 1
                        ? $"Rule matches explicit item type '{itemType}'."
                        : $"Rule matches item type '{itemType}' from union '{rule.ItemType}'.";
                    return true;
                }

                if (itemType.EndsWith(DeprecatedSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    var normalized = itemType[..^DeprecatedSuffix.Length];
                    if (targetItemTypes.Contains(normalized))
                    {
                        inclusionReason = $"Rule matches deprecated item type '{itemType}' via target type '{normalized}'.";
                        return true;
                    }
                }
            }

            inclusionReason = $"Rule item types '{rule.ItemType}' do not match target item types '{string.Join(", ", targetItemTypes)}'.";
            return false;
        }

        public static IReadOnlyList<string> SplitItemTypes(string? itemType)
        {
            return (itemType ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}