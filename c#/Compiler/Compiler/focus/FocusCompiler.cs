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
            // Use shared RenderAllowedToString helper from BaseCompiler to avoid duplication
            string RenderAllowed<T>(IEnumerable<T> items, Func<T, int> depthSel, Func<T, string> textSel)
                => RenderAllowedToString(items, depthSel, textSel);



            foreach (var tree in PassedTrees.Trees)
            {
                // Build buffers for this tree
                var sbFocus = new System.Text.StringBuilder();
                var sbLocalisation = new System.Text.StringBuilder();

                sbFocus.AppendLine($"{indent1}id = {tree.id}");
                if (tree.isDefault) sbFocus.AppendLine($"{indent1}default = yes");
                if (!string.IsNullOrEmpty(tree.countryTag))
                {
                    sbFocus.AppendLine($"{indent1}country = {{");
                    sbFocus.AppendLine($"{indent2}factor = 0");
                    sbFocus.AppendLine($"{indent2}modifier = {{");
                    sbFocus.AppendLine($"{indent3}add = 100");
                    sbFocus.AppendLine($"{indent3}original_tag = {tree.countryTag}");
                    sbFocus.AppendLine($"{indent2}}}");
                    sbFocus.AppendLine($"{indent1}}}");
                }
                sbFocus.Append(RenderAllowed(tree.rawLines, rl => rl.depth, rl => rl.trimmedLine));
                foreach (var focus in tree.focuses)
                {
                    sbFocus.AppendLine($"{indent1}focus = {{");
                    sbFocus.AppendLine($"{indent2}id = {focus.id}");
                    sbFocus.AppendLine($"{indent2}cost = {GetHoi4Cost(focus.timeValue, focus.timeUnit)}");
                    sbFocus.AppendLine($"{indent2}icon = {focus.sprite}");
                    sbFocus.AppendLine($"{indent2}x = {focus.positionX}");
                    sbFocus.AppendLine($"{indent2}y = {focus.positionY}");
                    foreach (var require in focus.requireIds)
                    {
                        sbFocus.AppendLine($"{indent2}prerequisite = {{ focus = {require} }}");
                    }
                    foreach (var prevents in focus.preventsIds)
                    {
                        sbFocus.AppendLine($"{indent2}mutually_exclusive = {{ focus = {prevents} }}");
                    }
                    if (!string.IsNullOrEmpty(focus.followPositionOf))
                    {
                        sbFocus.AppendLine($"{indent2}relative_position_id = {focus.followPositionOf}");
                    }
                    // Render optional named raw blocks: allowed, available, visible
                    if (focus.allowed != null && focus.allowed.Count > 0)
                    {
                        sbFocus.AppendLine($"{indent2}allowed = {{");
                        sbFocus.Append(RenderAllowed(focus.allowed, rl => rl.depth, rl => rl.trimmedLine));
                        sbFocus.AppendLine($"{indent2}}}");
                    }
                    if (focus.available != null && focus.available.Count > 0)
                    {
                        sbFocus.AppendLine($"{indent2}available = {{");
                        sbFocus.Append(RenderAllowed(focus.available, rl => rl.depth, rl => rl.trimmedLine));
                        sbFocus.AppendLine($"{indent2}}}");
                    }
                    if (focus.visible != null && focus.visible.Count > 0)
                    {
                        sbFocus.AppendLine($"{indent2}visible = {{");
                        sbFocus.Append(RenderAllowed(focus.visible, rl => rl.depth, rl => rl.trimmedLine));
                        sbFocus.AppendLine($"{indent2}}}");
                    }
                    sbFocus.Append(RenderAllowed(focus.rawLines, rl => rl.depth, rl => rl.trimmedLine));
                    sbFocus.AppendLine($"{indent2}completion_reward = {{");
                    sbFocus.Append(RenderAllowed(focus.onComplete, rl => rl.depth, rl => rl.trimmedLine));
                    sbFocus.AppendLine($"{indent2}}}");
                    sbFocus.AppendLine($"{indent1}}}");
                }
                sbFocus.AppendLine("}");

                foreach (var focus in tree.focuses)
                {
                    sbLocalisation.AppendLine($" {focus.id}:0 \"{focus.name}\"");
                    sbLocalisation.AppendLine($" {focus.id}_desc:0 \"{focus.desc}\"");
                }

                // Flush buffers
                WriteFile("common/national_focus/", fileName, ".txt", (sw, created) =>
                {
                    if (created) sw.WriteLine("focus_tree = {");
                    if (sbFocus.Length > 0) sw.Write(sbFocus.ToString());
                });

                WriteFile("localisation/english/national_focus/", fileName + "_l_english", ".yml", (sw, created) =>
                {
                    if (created) sw.WriteLine("l_english:");
                    if (sbLocalisation.Length > 0) sw.Write(sbLocalisation.ToString());
                });
            }
        }
    }
}
