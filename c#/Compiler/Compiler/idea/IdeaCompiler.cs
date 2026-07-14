using System;
using System.IO;
using System.Linq;

namespace Compiler.idea
{
    public class IdeaCompiler : BaseCompiler
    {
        // Parsed ideas provided by the parser (type defined inside IdeaParser)
        public Compiler.ParsedIdeaFile PassedData { get; set; }

        public override void Compile()
        {
            if (PassedData == null || PassedData.Ideas == null) return;

            var fileName = PassedData.SourceFileName;
            string indent1 = Ident(1);
            string indent2 = Ident(2);
            string indent3 = Ident(3);
            string indent4 = Ident(4);
            // Group ideas by their declared type and write them under their respective wrapper
            var ideasByType = PassedData.Ideas.GroupBy(i => string.IsNullOrEmpty(i.type) ? "country" : i.type);

            WriteFile("common/ideas/", fileName, ".txt", (sw, created) =>
            {
                sw.WriteLine("ideas = {");

                foreach (var group in ideasByType)
                {
                    var typeName = group.Key;
                    sw.WriteLine($"{indent1}{typeName} = {{");

                    foreach (var idea in group)
                    {
                        // Idea header
                        sw.WriteLine();
                        sw.WriteLine($"{indent2}{idea.id} = {{");

                        // picture / sprite
                        var sprite = idea.sprite ?? string.Empty;
                        sw.WriteLine($"{indent3}picture = \"{sprite}\"");

                        // Any generic rawLines that belong directly to the idea (other blocks)
                        if (idea.rawLines != null && idea.rawLines.Count > 0)
                        {
                            // previous behavior wrote Ident(raw.depth + 2) for each physical raw line
                            WriteAllowedWithConversions(sw, idea.rawLines, r => r.depth + 2, r => r.trimmedLine);
                        }

                        // modifier block - write header at the depth one less than the minimum raw depth
                        if (idea.modifier != null && idea.modifier.rawLines != null && idea.modifier.rawLines.Count > 0)
                        {
                            var minDepth = idea.modifier.rawLines.Min(r => r.depth);
                            var modifierHeaderDepth = Math.Max(0, minDepth - 1);
                            sw.WriteLine($"{Ident(modifierHeaderDepth + 2)}modifier = {{");
                            WriteAllowedWithConversions(sw, idea.modifier.rawLines, r => r.depth + 2, r => r.trimmedLine);
                            sw.WriteLine($"{Ident(modifierHeaderDepth + 2)}}}");
                        }

                        // Close idea block
                        sw.WriteLine($"{indent2}}}");
                    }

                    // Close type wrapper
                    sw.WriteLine($"{indent1}}}");
                }

                sw.WriteLine("}");
            });

            // Localisation files (one per idea)
            foreach (var idea in PassedData.Ideas)
            {
                WriteFile("localisation/english/ideas/", fileName + "_l_english", ".yml", (sw, created) =>
                {
                    if (created) sw.WriteLine("l_english:");

                    sw.WriteLine($" {idea.id}:0 \"{idea.name}\"");
                    sw.WriteLine($" {idea.id}_desc:0 \"{idea.desc}\"");
                });
            }


           
        }
    }
}
