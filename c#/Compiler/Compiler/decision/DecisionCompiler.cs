using System.IO;
using System.Linq;

namespace Compiler.decision
{
    public class DecisionCompiler : BaseCompiler
    {
        public DecisionCompiler()
        {
            Compiler.Logging.Logger.LogComponent("Compiler", "DecisionCompiler initialized.");
        }
        public Compiler.DecisionParser.ParsedDecisionFile PassedData { get; set; }

        public override void Compile()
        {
            if (PassedData == null || PassedData.Categories == null) return;

            var fileName = PassedData.SourceFileName;
            string indent1 = Ident(1);
            string indent2 = Ident(2);
            // Buffers for outputs so we only open each file once
            var sbCategories = new System.Text.StringBuilder();
            var sbDecisions = new System.Text.StringBuilder();
            var sbLocalisation = new System.Text.StringBuilder();
            // Use BaseCompiler.RenderAllowedToString directly in call sites

            foreach (var cat in PassedData.Categories)
            {
                // categories file content
                sbCategories.AppendLine($"{cat.id} = {{");
                if (cat.sprite != null) sbCategories.AppendLine($"{indent1}icon = {cat.sprite}");
                if (cat.pictureSprite != null) sbCategories.AppendLine($"{indent1}picture = {cat.pictureSprite}");
                sbCategories.AppendLine($"{indent1}priority = {cat.priority}");
                if (cat.allowed != null && cat.allowed.Any())
                {
                    sbCategories.AppendLine($"{indent1}allowed = {{");
                    sbCategories.Append(RenderAllowedToString(cat.allowed, rl => rl.depth, rl => rl.trimmedLine));
                    sbCategories.AppendLine($"{indent1}}}");
                }
                if (cat.available != null && cat.available.Any())
                {
                    sbCategories.AppendLine($"{indent1}available = {{");
                    sbCategories.Append(RenderAllowedToString(cat.available, rl => rl.depth, rl => rl.trimmedLine));
                    sbCategories.AppendLine($"{indent1}}}");
                }
                sbCategories.AppendLine($"}}");

                // decisions file: header for this category
                sbDecisions.AppendLine($"{cat.id} = {{");
                // add decisions (if any)
                if (cat.decisions != null && cat.decisions.Any())
                {
                    foreach (var cat2 in cat.decisions)
                    {
                        sbDecisions.AppendLine($"{indent1}{cat2.id} = {{");
                        if (cat2.sprite != null) sbDecisions.AppendLine($"{indent2}icon = {cat2.sprite}");
                        sbDecisions.AppendLine($"{indent2}cost = {cat2.cost}");
                        sbDecisions.AppendLine($"{indent1}priority = {cat2.priority}");
                        if (cat2.allowed != null && cat2.allowed.Any())
                        {
                            sbDecisions.AppendLine($"{indent2}allowed = {{");
                            sbDecisions.Append(RenderAllowedToString(cat2.allowed, rl => rl.depth, rl => rl.trimmedLine));
                            sbDecisions.AppendLine($"{indent2}}}");
                        }
                        if (cat2.available != null && cat2.available.Any())
                        {
                            sbDecisions.AppendLine($"{indent2}available = {{");
                            sbDecisions.Append(RenderAllowedToString(cat2.available, rl => rl.depth, rl => rl.trimmedLine));
                            sbDecisions.AppendLine($"{indent2}}}");
                        }
                        if (cat2.onClick != null && cat2.onClick.Any())
                        {
                            sbDecisions.AppendLine($"{indent2}complete_effect = {{");
                            sbDecisions.Append(RenderAllowedToString(cat2.onClick, rl => rl.depth, rl => rl.trimmedLine));
                            sbDecisions.AppendLine($"{indent2}}}");
                        }
                        sbDecisions.AppendLine($"{indent1}}}");
                    }
                }
                // close category block
                sbDecisions.AppendLine($"}}");

                // localisation entries
                sbLocalisation.AppendLine($" {cat.id}:0 \"{cat.name}\"");
                sbLocalisation.AppendLine($" {cat.id}_desc:0 \"{cat.desc}\"");
                foreach (var cat2 in cat.decisions)
                {
                    sbLocalisation.AppendLine($" {cat2.id}:0 \"{cat2.name}\"");
                    sbLocalisation.AppendLine($" {cat2.id}_desc:0 \"{cat2.desc}\"");
                }
            }

            // Now write each buffer once via WriteFile and respect created header logic
            WriteFile("common/decisions/categories", fileName, ".txt", (sw, created) =>
            {
                if (created) Compiler.Logging.Logger.LogComponent("Decision", $"Created categories output for {fileName}");
                if (sbCategories.Length > 0) sw.Write(sbCategories.ToString());
            });

            WriteFile("common/decisions", fileName, ".txt", (sw, created) =>
            {
                if (sbDecisions.Length > 0) sw.Write(sbDecisions.ToString());
            });

            WriteFile("localisation/english/decisions/", fileName + "_l_english", ".yml", (sw, created) =>
            {
                if (created) sw.WriteLine("l_english:");
                if (sbLocalisation.Length > 0) sw.Write(sbLocalisation.ToString());
            });
        }
    }
}
