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

            if (country.MapColor != null)
            {
                sw.WriteLine($"color = rgb {{ {country.MapColor.Red} {country.MapColor.Green} {country.MapColor.Blue} }}");
            }

            if (country.UiColor != null)
            {
                sw.WriteLine($"color_ui = rgb {{ {country.UiColor.Red} {country.UiColor.Green} {country.UiColor.Blue} }}");
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

                // Always write the base country name first
                if (!string.IsNullOrEmpty(country.Name))
                {
                    sw.WriteLine($" {country.Tag}:0 \"{country.Name}\"");
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
        /// Examples:
        /// - Subject: TAG_target_subject
        /// - Overlord: TAG_target_overlord
        /// - Ideology: TAG_ideology
        /// - Autonomy: TAG_autonomy_level
        /// - Multiple: TAG_target_subject_communism
        /// - With def: TAG_def
        /// - With adj: TAG_adj
        private string FormatConditionKey(string tag, IEnumerable<HistoryParser.Condition> conditions, string nameType = "")
        {
            // If there's a name type (def or adj) and no conditions, just append it to the tag
            if (!string.IsNullOrEmpty(nameType) && (conditions == null || !conditions.Any()))
            {
                return $"{tag}_{nameType}";
            }

            if (conditions == null || !conditions.Any())
                return tag;

            var formattedSegments = conditions
                .Where(c => c != null && !string.IsNullOrEmpty(c.ConditionValue))
                .Select(c =>
                {
                    string normValue = NormalizeValue(c.ConditionValue);
                    string type = (c.Type ?? string.Empty).ToLowerInvariant();

                    return type switch
                    {
                        "subject" => $"{normValue}_subject",
                        "overlord" => $"{normValue}_overlord",
                        "ideology" => normValue,
                        "autonomy" => normValue.StartsWith("autonomy_") ? normValue : $"autonomy_{normValue}",
                        "party" => normValue,
                        _ => normValue
                    };
                })
                .Where(s => !string.IsNullOrEmpty(s));

            if (!formattedSegments.Any())
            {
                // If we have a nameType but no valid segments, use just the nameType with tag
                if (!string.IsNullOrEmpty(nameType))
                    return $"{tag}_{nameType}";
                return tag;
            }

            var result = $"{tag}_{string.Join("_", formattedSegments)}";

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
                    sw.WriteLine($"color = {{ {country.UiColor.Red} {country.UiColor.Green} {country.UiColor.Blue} }}");
                }
            });
        }
    }
}
