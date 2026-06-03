using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Compiler.focus
{
    public class FocusCompiler : BaseCompiler
    {
        // Parsed focuses provided by the parser
        public Compiler.ParsedFocusFile PassedTrees { get; set; }

        public override void Compile()
        {
            if (PassedTrees == null || PassedTrees.Trees.Count == 0) return;

            // Prefer the original source file name provided by the parser when available.
            // Fall back to previous behaviour (default_focus / focuses) when no source name is present.
            string fileName = !string.IsNullOrWhiteSpace(PassedTrees.SourceFileName)
                ? Path.GetFileNameWithoutExtension(PassedTrees.SourceFileName)
                : throw new ArgumentException("The SourceFileName property is missing or invalid in focus compiler.", nameof(PassedTrees.SourceFileName));
            string indent1 = Ident(1);
            string indent2 = Ident(2);
            string indent3 = Ident(3);
            foreach (var tree in PassedTrees.Trees)
            {
                // Write a single file containing all trees passed
                WriteFile("common/national_focus/", fileName, ".txt", (sw, created) =>
                {
                    if (created)
                    {
                        sw.WriteLine("focus_tree = {");
                    }
                    sw.WriteLine($"{indent1}id = {tree.id}");
                    if (tree.isDefault) sw.WriteLine($"{indent1}default = yes");
                    if (!string.IsNullOrEmpty(tree.countryTag))
                    {
                        sw.WriteLine($"{indent1}country = {{");
                        sw.WriteLine($"{indent2}factor = 0");
                        sw.WriteLine($"{indent2}modifier = {{");
                        sw.WriteLine($"{indent3}add = 100");
                        sw.WriteLine($"{indent3}original_tag = {tree.countryTag}");
                        sw.WriteLine($"{indent2}}}");
                        sw.WriteLine($"{indent1}}}");
                    }
                    WriteAllowedWithConversions(sw, tree.rawLines, rl => rl.depth, rl => rl.trimmedLine);
                    foreach (var focus in tree.focuses)
                    {
                        sw.WriteLine($"{indent1}focus = {{");
                        sw.WriteLine($"{indent2}id = {focus.id}");
                        sw.WriteLine($"{indent2}cost = {GetHoi4Cost(focus.timeValue, focus.timeUnit)}");
                        sw.WriteLine($"{indent2}icon = {focus.sprite}");
                        sw.WriteLine($"{indent2}x = {focus.positionX}");
                        sw.WriteLine($"{indent2}y = {focus.positionY}");
                        foreach (var require in focus.requireIds)
                        {
                            sw.WriteLine($"{indent2}prerequisite = {{ id = {require} }}");
                        }
                        foreach (var prevents in focus.preventsIds)
                        {
                            sw.WriteLine($"{indent2}mutually_exclusive = {{ id = {prevents} }}");
                        }
                        if (!string.IsNullOrEmpty(focus.followPositionOf))
                        {
                            sw.WriteLine($"{indent2}relative_position_id = {focus.followPositionOf}");
                        }
                        WriteAllowedWithConversions(sw, focus.rawLines, rl => rl.depth, rl => rl.trimmedLine);
                        sw.WriteLine($"{indent2}completion_reward = {{");
                        WriteAllowedWithConversions(sw, focus.onComplete, rl => rl.depth, rl => rl.trimmedLine);
                        sw.WriteLine($"{indent2}}}");
                        sw.WriteLine($"{indent1}}}");
                    }
                    sw.WriteLine("}");
                });


                WriteFile("localisation/english/national_focus/", fileName + "_l_english", ".yml", (sw, created) =>
                {
                    if (created) sw.WriteLine("l_english:");
                });
                WriteFile("localisation/english/national_focus/", fileName + "_l_english", ".yml", (sw, created) =>
                {
                    foreach (var focus in tree.focuses)
                    {
                        sw.WriteLine($" {focus.id}:0 \"{focus.name}\"");
                        sw.WriteLine($" {focus.id}_desc:0 \"{focus.desc}\"");
                    }
                });
            }
        }
    }
}
