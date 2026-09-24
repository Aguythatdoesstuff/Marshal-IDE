using System;
using System.IO;
using System.Linq;
using static Compiler.UnitParser;

namespace Compiler
{
    public class UnitCompiler : BaseCompiler
    {
        // Parsed unit data provided by the parser
        public UnitParser.ParsedUnitFile PassedData { get; set; }

        public override void Compile()
        {
            if (PassedData == null) return;

            // Prepare in-memory buffers for each target so we only call WriteFile once per file
            var sbUnit = new System.Text.StringBuilder();
            var sbLocalization = new System.Text.StringBuilder();

            // Process all units in the parsed unit file
            if (PassedData.Units != null && PassedData.Units.Count > 0)
            {
                sbUnit.AppendLine("sub_units = {");

                foreach (var unit in PassedData.Units)
                {
                    CompileUnit(unit, sbUnit, sbLocalization);
                }

                sbUnit.AppendLine("}");
            }

            // Flush buffers using WriteFile once per target
            if (sbUnit.Length > 0)
            {
                WriteFile("common/units/", PassedData.SourceFileName, ".txt", (sw, created) =>
                {
                    sw.Write(sbUnit.ToString());
                });
            }

            if (sbLocalization.Length > 0)
            {
                WriteFile("localisation/english/units/", PassedData.SourceFileName + "_l_english", ".yml", (sw, created) =>
                {
                    if (created) sw.WriteLine("l_english:");
                    sw.Write(sbLocalization.ToString());
                });
            }
        }

        private void CompileUnit(UnitParser.Unit unit, System.Text.StringBuilder sbUnit, System.Text.StringBuilder sbLocalization)
        {
            // Start unit definition block
            sbUnit.AppendLine($"{Ident(1)}{unit.Id} = {{");

            // Model / Sprite Entity
            if (!string.IsNullOrEmpty(unit.UnitModel))
            {
                sbUnit.AppendLine($"{Ident(2)}sprite = {unit.UnitModel}");
            }

            // Internal Types Block
            if (unit.UnitTypes != null && unit.UnitTypes.Count > 0)
            {
                sbUnit.AppendLine($"{Ident(2)}type = {{");
                foreach (var typeId in unit.UnitTypes)
                {
                    sbUnit.AppendLine($"{Ident(3)}{typeId}");
                }
                sbUnit.AppendLine($"{Ident(2)}}}");
            }

            // Unit Categories Block
            if (unit.UnitCategories != null && unit.UnitCategories.Count > 0)
            {
                sbUnit.AppendLine($"{Ident(2)}categories = {{");
                foreach (var categoryId in unit.UnitCategories)
                {
                    sbUnit.AppendLine($"{Ident(3)}{categoryId}");
                }
                sbUnit.AppendLine($"{Ident(2)}}}");
            }

            // Required/Needed Equipment Block
            if (unit.RequiredEquipment != null && unit.RequiredEquipment.Count > 0)
            {
                sbUnit.AppendLine($"{Ident(2)}need = {{");
                sbUnit.Append(RenderAllowedToString(unit.RequiredEquipment, r => r.depth + 1, r => r.trimmedLine));
                sbUnit.AppendLine($"{Ident(2)}}}");
            }

            // Additional stats, modifiers, and unhandled DSL properties from RawLines
            if (unit.RawLines != null && unit.RawLines.Count > 0)
            {
                sbUnit.Append(RenderAllowedToString(unit.RawLines, r => r.depth + 1, r => r.trimmedLine));
            }

            sbUnit.AppendLine($"{Ident(1)}}}\n");

            // Localisation Output
            string baseKey = unit.Id.ToLowerInvariant();

            if (!string.IsNullOrEmpty(unit.Name))
            {
                sbLocalization.AppendLine($" {baseKey}: \"{EscapeForYml(unit.Name)}\"");
            }

            if (!string.IsNullOrEmpty(unit.Desc))
            {
                sbLocalization.AppendLine($" {baseKey}_desc: \"{EscapeForYml(unit.Desc)}\"");
            }

            if (!string.IsNullOrEmpty(unit.Abbreviation))
            {
                sbLocalization.AppendLine($" {baseKey}_short: \"{EscapeForYml(unit.Abbreviation)}\"");
            }
        }

        private static string EscapeForYml(string s)
        {
            if (s == null) return string.Empty;
            return s.Replace("\"", "\\\"");
        }
    }
}