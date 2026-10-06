using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static Compiler.HistoryParser;

namespace Compiler
{
    public class HistoryCompiler : BaseCompiler
    {
        // Parsed history data provided by the parser
        public HistoryParser.ParsedHistoryFile PassedData { get; set; }

        public override void Compile()
        {
            if (PassedData?.Historys == null) return;

            foreach (var historys in PassedData.Historys)
            {
                if (historys?.Countrys == null) continue;

                foreach (var country in historys.Countrys)
                {
                    if (string.IsNullOrEmpty(country.Tag))
                        continue;

                    string fileName = $"{country.Tag} - {country.Name}";

                    // Write history file
                    WriteFile("history/countries/", fileName, ".txt", (sw, created) =>
                    {
                        WriteCountryHistoryFile(sw, country);
                    });

                    // Write conditional country names to localisation
                    if (country.ConditionalNames != null && country.ConditionalNames.Count > 0)
                    {
                        WriteConditionalNamesLocalisation(country);
                    }

                    // Write conditional party names to localisation
                    if (country.ConditionalPartyNames != null && country.ConditionalPartyNames.Count > 0)
                    {
                        WriteConditionalPartyNamesLocalisation(country);
                    }
                }
            }
        }

        private void WriteCountryHistoryFile(StreamWriter sw, HistoryParser.Country country)
        {
            // Write capital if present
            if (!string.IsNullOrEmpty(country.Capital))
            {
                sw.WriteLine($"capital = {country.Capital}");
            }

            // Write unconditional base name
            if (!string.IsNullOrEmpty(country.Name))
            {
                sw.WriteLine($"name = \"{country.Name}\"");
            }

            // Write raw lines (already at the correct indentation depth for country level)
            if (country.RawLines != null && country.RawLines.Count > 0)
            {
                WriteAllowedWithConversions(sw, country.RawLines, r => r.depth, r => r.trimmedLine);
            }
        }

        private void WriteConditionalNamesLocalisation(HistoryParser.Country country)
        {
            string fileName = country.Tag;

            WriteFile("localisation/english/", fileName, ".yml", (sw, created) =>
            {
                if (created)
                {
                    sw.WriteLine("l_english:");
                }

                // Group conditional names by their condition combinations
                var groupedNames = GroupConditions(country.ConditionalNames);

                foreach (var group in groupedNames)
                {
                    if (string.IsNullOrEmpty(group.Key) || group.Any(c => string.IsNullOrEmpty(c.Name)))
                        continue;

                    string locKey = BuildLocalisationKey(country.Tag, group.Key);
                    string nameValue = group.First().Name; // All names in the group should be the same

                    sw.WriteLine($" {locKey}: \"{nameValue}\"");
                }
            });
        }

        private void WriteConditionalPartyNamesLocalisation(HistoryParser.Country country)
        {
            string fileName = country.Tag;

            WriteFile("localisation/english/", fileName, ".yml", (sw, created) =>
            {
                if (created)
                {
                    sw.WriteLine("l_english:");
                }

                // Group conditional party names by their condition combinations
                var groupedPartyNames = GroupConditions(country.ConditionalPartyNames);

                foreach (var group in groupedPartyNames)
                {
                    if (string.IsNullOrEmpty(group.Key) || group.Any(c => string.IsNullOrEmpty(c.Name)))
                        continue;

                    string partyName = group.First().Name;
                    string conditionKey = group.Key;

                    // Build party name keys: TAG_ideology_party and TAG_ideology_party_long
                    string baseKey = BuildLocalisationKey(country.Tag, $"{conditionKey}_party");
                    string longKey = $"{baseKey}_long";

                    sw.WriteLine($" {baseKey}: \"{partyName}\"");
                    sw.WriteLine($" {longKey}: \"{partyName}\"");
                }
            });
        }

        /// Groups conditional names/party names by their combined condition keys
        /// Returns a list of groups where each group contains items with the same condition combination
        private List<IGrouping<string, T>> GroupConditions<T>(List<T> items) where T : class
        {
            if (items == null || items.Count == 0)
                return new List<IGrouping<string, T>>();

            var groupDict = new Dictionary<string, List<T>>();

            foreach (var item in items)
            {
                string conditionKey = ExtractConditionKey(item);

                if (!groupDict.ContainsKey(conditionKey))
                {
                    groupDict[conditionKey] = new List<T>();
                }
                groupDict[conditionKey].Add(item);
            }

            return groupDict.Select(kvp => new ConditionGrouping<T>(kvp.Key, kvp.Value)).Cast<IGrouping<string, T>>().ToList();
        }

        /// Extracts and normalizes the condition key from a ConditionalName or ConditionalPartyName
        private string ExtractConditionKey(object item)
        {
            if (item == null) return string.Empty;

            var conditionalName = item as HistoryParser.ConditionalName;
            var conditionalPartyName = item as HistoryParser.ConditionalPartyName;

            var condition = conditionalName?.Condition ?? conditionalPartyName?.Condition;

            if (condition == null || string.IsNullOrEmpty(condition.Type))
                return string.Empty;

            // Normalize the condition value to camelCase/lowercase (e.g., "integrated_puppet" for autonomy values)
            string normalizedValue = NormalizeConditionValue(condition.ConditionValue);

            return $"{condition.Type}_{normalizedValue}";
        }

        /// Normalizes condition values to match HOI4 localisation key conventions
        /// e.g., "autonomy_integrated_puppet" -> "autonomy_integrated_puppet"
        private string NormalizeConditionValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.ToLower().Replace(" ", "_").Replace("-", "_");
        }

        /// Builds a localisation key from tag and condition key
        /// e.g., BuildLocalisationKey("SCO", "liberalism") -> "SCO_liberalism"
        private string BuildLocalisationKey(string tag, string conditionKey)
        {
            if (string.IsNullOrEmpty(conditionKey))
                return tag;

            return $"{tag}_{conditionKey}";
        }
    }

    /// Helper class to group conditional items by their condition key
    internal class ConditionGrouping<T> : IGrouping<string, T> where T : class
    {
        private readonly string _key;
        private readonly List<T> _items;

        public ConditionGrouping(string key, List<T> items)
        {
            _key = key;
            _items = items;
        }

        public string Key => _key;

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _items.GetEnumerator();
    }
}