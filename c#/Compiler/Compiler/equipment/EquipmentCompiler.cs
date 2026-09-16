using System;
using System.IO;
using System.Linq;
using static Compiler.EquipmentParser;

namespace Compiler
{
    public class EquipmentCompiler : BaseCompiler
    {
        // Parsed equipment data provided by the parser
        public EquipmentParser.ParsedEquipmentFile PassedData { get; set; }

        public override void Compile()
        {
            if (PassedData == null) return;

            // Prepare in-memory buffers for each target so we only call WriteFile once per file
            var sbEquipment = new System.Text.StringBuilder();
            var sbLocalization = new System.Text.StringBuilder();

            // Process all archetypes in the parsed equipment file
            if (PassedData.Archetypes != null && PassedData.Archetypes.Count > 0)
            {
                foreach (var archetype in PassedData.Archetypes)
                {
                    sbEquipment.AppendLine("equipments = {");
                    CompileArchetype(archetype, sbEquipment, sbLocalization);
                    sbEquipment.AppendLine("}");
                }
            }

            // Flush buffers using WriteFile once per target
            if (sbEquipment.Length > 0)
            {
                WriteFile("common/units/equipment/", PassedData.SourceFileName, ".txt", (sw, created) =>
                {
                    sw.Write(sbEquipment.ToString());
                });
            }

            if (sbLocalization.Length > 0)
            {
                WriteFile("localisation/english/equipment", PassedData.SourceFileName + "_l_english", ".yml", (sw, created) =>
                {
                    if (created) sw.WriteLine("l_english:");
                    sw.Write(sbLocalization.ToString());
                });
            }
        }

        private void CompileArchetype(EquipmentParser.Archetype archetype, System.Text.StringBuilder sbEquipment, System.Text.StringBuilder sbLocalization)
        {
            // Start archetype block
            sbEquipment.AppendLine($"{Ident(1)}{archetype.Id} = {{");
            sbEquipment.AppendLine($"{Ident(2)}year = {archetype.AvailableFromYear}");
            if (archetype.ForUnits != null && archetype.ForUnits.Count > 0)
            {
                sbEquipment.AppendLine($"{Ident(2)}type = {{");
                foreach (var unit in archetype.ForUnits)
                {
                    sbEquipment.AppendLine($"{Ident(3)}{unit}");
                }
                sbEquipment.AppendLine($"{Ident(2)}}}");
            }
            // Add raw lines if any
            if (archetype.RawLines != null && archetype.RawLines.Count > 0)
            {
                sbEquipment.Append(RenderAllowedToString(archetype.RawLines, r => r.depth, r => r.trimmedLine)); // depth in DSL compared to output is different due to the game eninge requiring a wrapper called "equipments" wich the dsl doesnt use
            }

            sbEquipment.AppendLine($"{Ident(1)}}}\n");

            // equipment is required to not be not inside the archetype block, so we compile it separately
            if (archetype.BaseEquipment != null)
            {
                CompileEquipment(archetype.BaseEquipment, sbEquipment, sbLocalization, archetype.Id, 1, null);
            }

            // Add archetype localizations
            string archetypeBaseKey = archetype.Id.ToLowerInvariant();

            // Add archetype names with optional country tag
            foreach (var name in archetype.Names ?? Enumerable.Empty<EquipmentParser.LocalizedText>())
            {
                string key = GenerateCountryTagLocalizationKey(archetypeBaseKey, name.CountryTag);
                sbLocalization.AppendLine($" {key}: \"{EscapeForYml(name.Value)}\"");
            }

            // Add archetype descriptions with optional country tag
            string archetypeDescKey = $"{archetypeBaseKey}_desc";
            foreach (var desc in archetype.Descriptions ?? Enumerable.Empty<EquipmentParser.LocalizedText>())
            {
                string key = GenerateCountryTagLocalizationKey(archetypeDescKey, desc.CountryTag);
                sbLocalization.AppendLine($" {key}: \"{EscapeForYml(desc.Value)}\"");
            }

            // Add archetype short descriptions with optional country tag
            string archetypeShortDescKey = $"{archetypeBaseKey}_short";
            foreach (var shortDesc in archetype.ShortDescriptions ?? Enumerable.Empty<EquipmentParser.LocalizedText>())
            {
                string key = GenerateCountryTagLocalizationKey(archetypeShortDescKey, shortDesc.CountryTag);
                sbLocalization.AppendLine($" {key}: \"{EscapeForYml(shortDesc.Value)}\"");
            }
        }

        private void CompileEquipment(EquipmentParser.Equipment equipment, System.Text.StringBuilder sbEquipment, System.Text.StringBuilder sbLocalization, string archeTypeId, int indentLevel, string parentEquipmentId)
        {
            // Start archetype block
            sbEquipment.AppendLine($"{Ident(1)}{equipment.Id} = {{");
            sbEquipment.AppendLine($"{Ident(2)}year = {equipment.AvailableFromYear}");
            sbEquipment.AppendLine($"{Ident(2)}archetype = {archeTypeId}");
            if (!string.IsNullOrEmpty(parentEquipmentId))
                sbEquipment.AppendLine($"{Ident(2)}parent = {parentEquipmentId}");

            // Add raw lines if any
            if (equipment.RawLines != null && equipment.RawLines.Count > 0)
            {
                sbEquipment.Append(RenderAllowedToString(equipment.RawLines, r => r.depth, r => r.trimmedLine)); // depth in DSL compared to output is different due to the game eninge requiring a wrapper called "equipments" wich the dsl doesnt use
            }

            sbEquipment.AppendLine($"{Ident(1)}}}\n");


            // Add upgraded equipment recursively if available
            if (equipment.UpgradedEquipment != null)
            {
                CompileEquipment(equipment.UpgradedEquipment, sbEquipment, sbLocalization, archeTypeId, indentLevel + 1, equipment.Id);
            }



            // Add equipment localizations
            string equipmentBaseKey = equipment.Id.ToLowerInvariant();

            // Add equipment names with optional country tag
            foreach (var name in equipment.Names ?? Enumerable.Empty<EquipmentParser.LocalizedText>())
            {
                string key = GenerateCountryTagLocalizationKey(equipmentBaseKey, name.CountryTag);
                sbLocalization.AppendLine($" {key}: \"{EscapeForYml(name.Value)}\"");
            }

            // Add equipment descriptions with optional country tag
            string equipmentDescKey = $"{equipmentBaseKey}_desc";
            foreach (var desc in equipment.Descriptions ?? Enumerable.Empty<EquipmentParser.LocalizedText>())
            {
                string key = GenerateCountryTagLocalizationKey(equipmentDescKey, desc.CountryTag);
                sbLocalization.AppendLine($" {key}: \"{EscapeForYml(desc.Value)}\"");
            }

            // Add equipment short descriptions with optional country tag
            string equipmentShortDescKey = $"{equipmentBaseKey}_short";
            foreach (var shortDesc in equipment.ShortDescriptions ?? Enumerable.Empty<EquipmentParser.LocalizedText>())
            {
                string key = GenerateCountryTagLocalizationKey(equipmentShortDescKey, shortDesc.CountryTag);
                sbLocalization.AppendLine($" {key}: \"{EscapeForYml(shortDesc.Value)}\"");
            }
        }

        private static string EscapeForYml(string s)
        {
            if (s == null) return string.Empty;
            return s.Replace("\"", "\\\"");
        }
    }
}
