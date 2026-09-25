using System;
using System.IO;
using System.Linq;
using static Compiler.OobParser;

namespace Compiler
{
    public class OobCompiler : BaseCompiler
    {
        // Parsed unit data provided by the parser
        public OobParser.ParsedOobFile PassedData { get; set; }

        public override void Compile()
        {
            if (PassedData == null) return;

            // Prepare in-memory buffers for each target so we only call WriteFile once per file
            var sbOob = new System.Text.StringBuilder();
            var sbLocalization = new System.Text.StringBuilder();

            // Process all units in the parsed unit file
            if (PassedData.Oobs != null && PassedData.Oobs.Count > 0)
            {
                sbOob.AppendLine("sub_units = {");

                foreach (var unit in PassedData.Oobs)
                {
                    CompileOob(unit, sbOob, sbLocalization);
                }

                sbOob.AppendLine("}");
            }

            // Flush buffers using WriteFile once per target
            if (sbOob.Length > 0)
            {
                WriteFile("common/units/", PassedData.SourceFileName, ".txt", (sw, created) =>
                {
                    sw.Write(sbOob.ToString());
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

        private void CompileOob(OobParser.Oob unit, System.Text.StringBuilder sbOob, System.Text.StringBuilder sbLocalization)
        {
            // Start unit definition block
            sbOob.AppendLine($"{Ident(1)}{unit.Id} = {{");

            // Model / Sprite Entity
            if (!string.IsNullOrEmpty(unit.OobModel))
            {
                sbOob.AppendLine($"{Ident(2)}sprite = {unit.OobModel}");
            }

            // Internal Types Block
            if (unit.OobTypes != null && unit.OobTypes.Count > 0)
            {
                sbOob.AppendLine($"{Ident(2)}type = {{");
                foreach (var typeId in unit.OobTypes)
                {
                    sbOob.AppendLine($"{Ident(3)}{typeId}");
                }
                sbOob.AppendLine($"{Ident(2)}}}");
            }

            // Oob Categories Block
            if (unit.OobCategories != null && unit.OobCategories.Count > 0)
            {
                sbOob.AppendLine($"{Ident(2)}categories = {{");
                foreach (var categoryId in unit.OobCategories)
                {
                    sbOob.AppendLine($"{Ident(3)}{categoryId}");
                }
                sbOob.AppendLine($"{Ident(2)}}}");
            }

            // Required/Needed Equipment Block
            if (unit.RequiredEquipment != null && unit.RequiredEquipment.Count > 0)
            {
                sbOob.AppendLine($"{Ident(2)}need = {{");
                sbOob.Append(RenderAllowedToString(unit.RequiredEquipment, r => r.depth + 1, r => r.trimmedLine));
                sbOob.AppendLine($"{Ident(2)}}}");
            }

            // Additional stats, modifiers, and unhandled DSL properties from RawLines
            if (unit.RawLines != null && unit.RawLines.Count > 0)
            {
                sbOob.Append(RenderAllowedToString(unit.RawLines, r => r.depth + 1, r => r.trimmedLine));
            }

            sbOob.AppendLine($"{Ident(1)}}}\n");

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