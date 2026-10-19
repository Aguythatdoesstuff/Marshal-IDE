using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Compiler.history
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

                    // Write country tag definition
                    WriteCountryTagDefinition(country);

                    // Write country file with graphical culture and color
                    WriteCountryFile(country);
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

            // Write raw lines (already at the correct indentation depth for country level)
            if (country.RawLines != null && country.RawLines.Count > 0)
            {
                WriteAllowedWithConversions(sw, country.RawLines, r => r.depth - 1, r => r.trimmedLine);
            }
        }

        private void WriteConditionalNamesLocalisation(HistoryParser.Country country)
        {
            string fileName = country.Tag;

            WriteFile("localisation/english/countries/", fileName, ".yml", (sw, created) =>
            {
                if (created)
                {
                    sw.WriteLine("l_english:");
                }

                // Always write the base country name first with safe uppercase tag
                if (!string.IsNullOrEmpty(country.Name))
                {
                    sw.WriteLine($" {country.Tag.ToUpperInvariant()}:0 \"{country.Name}\"");
                }

                // Output all conditional names without filtering
                var groupedNames = country.ConditionalNames
                    .GroupBy(c => FormatConditionKey(country.Tag, c.Conditions, c.NameType));

                foreach (var group in groupedNames)
                {
                    string locKey = group.Key;
                    string nameValue = group.First().Name;

                    if (!string.IsNullOrEmpty(locKey) && !string.IsNullOrEmpty(nameValue))
                    {
                        sw.WriteLine($" {locKey}:0 \"{nameValue}\"");
                    }
                }
            });
        }

        private void WriteConditionalPartyNamesLocalisation(HistoryParser.Country country)
        {
            string fileName = country.Tag;

            WriteFile("localisation/english/countries/", fileName, ".yml", (sw, created) =>
            {
                if (created)
                {
                    sw.WriteLine("l_english:");
                }

                // Group conditional party names by their formatted condition keys
                var groupedPartyNames = country.ConditionalPartyNames
                    .GroupBy(c => FormatConditionKey(country.Tag, c.Conditions))
                    .Where(g => !string.IsNullOrEmpty(g.Key) && g.All(c => !string.IsNullOrEmpty(c.Name)));

                foreach (var group in groupedPartyNames)
                {
                    string partyName = group.First().Name;
                    string conditionBaseKey = group.Key;

                    // Build party name keys: TAG_conditions_party and TAG_conditions_party_long
                    string baseKey = $"{conditionBaseKey}_party";
                    string longKey = $"{baseKey}_long";

                    sw.WriteLine($" {baseKey}:0 \"{partyName}\"");
                    sw.WriteLine($" {longKey}:0 \"{partyName}\"");
                }
            });
        }

        /// Formats a localisation key from a tag and multiple stacked conditions
        /// Handles condition-specific formatting and joins them into a single key
        /// Special case: overlord="generic" triggers COUNTRY_TAG format and is skipped from output
        /// Examples:
        /// - Subject: TAG_target_subject
        /// - Overlord: TAG_target_overlord
        /// - Overlord generic: COUNTRY_TAG_autonomy_dominion (overlord=generic is skipped)
        /// - Ideology: TAG_ideology
        /// - Autonomy: TAG_autonomy_level
        private string FormatConditionKey(string tag, IEnumerable<HistoryParser.Condition> conditions, string nameType = "")
        {
            // Ensure tag is always uppercase using safe method
            string safeTag = tag.ToUpperInvariant();

            // If tag is not exactly 3 characters, it's generic - replace with COUNTRY
            string displayTag = safeTag.Length == 3 ? safeTag : "COUNTRY";

            // If there's a name type (def or adj) and no conditions, just append it to the tag
            if (!string.IsNullOrEmpty(nameType) && (conditions == null || !conditions.Any()))
            {
                return $"{displayTag}_{nameType}";
            }

            if (conditions == null || !conditions.Any())
                return displayTag;

            // Check if any condition is overlord="generic" (case-insensitive)
            bool hasGenericOverlord = conditions.Any(c => 
                c != null && 
                (c.Type ?? "").ToLowerInvariant() == "overlord" && 
                c.ConditionValue.ToUpperInvariant() == "GENERIC");

            var formattedSegments = new List<string>();

            foreach (var c in conditions)
            {
                if (c == null || string.IsNullOrEmpty(c.ConditionValue))
                    continue;

                string type = (c.Type ?? string.Empty).ToLowerInvariant();
                string normValue = NormalizeValue(c.ConditionValue);

                // Skip overlord="generic" condition - don't format it
                if (type == "overlord" && c.ConditionValue.ToUpperInvariant() == "GENERIC")
                {
                    continue;
                }

                // Format the condition based on its type
                string formatted = type switch
                {
                    "subject" => $"{normValue.ToUpperInvariant()}_subject",
                    "overlord" => $"{normValue.ToUpperInvariant()}_overlord",
                    "ideology" => normValue,
                    "autonomy" => normValue.StartsWith("autonomy_") ? normValue : $"autonomy_{normValue}",
                    "party" => normValue,
                    _ => normValue
                };

                if (!string.IsNullOrEmpty(formatted))
                    formattedSegments.Add(formatted);
            }

            if (formattedSegments.Count == 0)
            {
                // If we have a nameType but no valid segments, use just the nameType with tag
                if (!string.IsNullOrEmpty(nameType))
                    return $"{displayTag}_{nameType}";
                return displayTag;
            }

            // Build the key tag: if overlord="generic", use COUNTRY_original_tag, otherwise use displayTag
            string keyTag = displayTag;
            if (hasGenericOverlord)
            {
                keyTag = $"COUNTRY_{safeTag}";
            }

            var result = $"{keyTag}_{string.Join("_", formattedSegments)}";

            // Append nameType if present
            if (!string.IsNullOrEmpty(nameType))
                result = $"{result}_{nameType}";

            return result;
        }

        /// Normalizes condition values to match HOI4 localisation key conventions
        /// Converts to lowercase and replaces spaces/hyphens with underscores
        private string NormalizeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.ToLowerInvariant().Replace(" ", "_").Replace("-", "_");
        }

        private void WriteCountryTagDefinition(HistoryParser.Country country)
        {

            WriteFile("common/country_tags/", country.Name, ".txt", (sw, created) =>
            {
                sw.WriteLine($"{country.Tag} = \"countries/{country.Name}.txt\"");
            });
        }

        private void WriteCountryFile(HistoryParser.Country country)
        {
            WriteFile("common/countries/", country.Name, ".txt", (sw, created) =>
            {
                // Write graphical culture (required for game to display country)
                sw.WriteLine("graphical_culture = western_european_gfx");
                sw.WriteLine("graphical_culture_2d = western_european_2d");

                // Write color if available, otherwise use default
                if (country.MapColor != null)
                {
                    sw.WriteLine($"color = {{ {country.MapColor.Red} {country.MapColor.Green} {country.MapColor.Blue} }}");
                }
                if (country.UiColor != null)
                {
                    sw.WriteLine($"color_ui = {{ {country.UiColor.Red} {country.UiColor.Green} {country.UiColor.Blue} }}");
                }
            });
        }
    }
}
