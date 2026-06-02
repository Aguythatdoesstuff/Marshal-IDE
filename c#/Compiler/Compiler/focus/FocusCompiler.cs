using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Compiler.focus
{
    public class FocusCompiler : BaseCompiler
    {
        // Parsed focuses provided by the parser
        public Compiler.Tree[] PassedTrees { get; set; }
        public Compiler.ParsedIdeaFile PassedIdeas { get; set; } // not used but kept for symmetry

        public override void Compile()
        {
            if (PassedTrees == null || PassedTrees.Length == 0) return;

            string fileName = PassedTrees[0]?.isDefault == true ? "default_focus" : "focuses";
            string indent1 = Ident(1);
            string indent2 = Ident(2);

            // Write a single file containing all trees passed
            WriteFile("common/national_focus/", fileName, ".txt", sw =>
            {
                sw.WriteLine("focus_tree = {");

                foreach (var tree in PassedTrees)
                {
                    // if tree has a countryTag, write a for tag line
                    if (!string.IsNullOrEmpty(tree.countryTag))
                    {
                        sw.WriteLine($"{indent1}focus_tree = {{");
                        sw.WriteLine($"{indent2}id = {tree.id}");
                        sw.WriteLine($"{indent2}country = {tree.countryTag}");
                        // write raw lines for tree
                        if (tree.rawLines != null && tree.rawLines.Count > 0)
                        {
                            WriteAllowedWithConversions(sw, tree.rawLines, r => r.depth + 2, r => r.trimmedLine);
                        }
                        sw.WriteLine($"{indent1}}}");
                    }
                    else
                    {
                        // default tree: just write it out
                        sw.WriteLine($"{indent1}tree = {{");
                        sw.WriteLine($"{indent2}id = {tree.id}");
                        if (tree.rawLines != null && tree.rawLines.Count > 0)
                        {
                            WriteAllowedWithConversions(sw, tree.rawLines, r => r.depth + 2, r => r.trimmedLine);
                        }
                        sw.WriteLine($"{indent1}}}");
                    }

                    // write each focus in the tree
                    if (tree.focuses != null)
                    {
                        foreach (var f in tree.focuses)
                        {
                            sw.WriteLine();
                            sw.WriteLine($"{indent2}{f.id} = {{");
                            if (!string.IsNullOrEmpty(f.name)) sw.WriteLine($"{Ident(3)}name = \"{f.name}\"");
                            if (!string.IsNullOrEmpty(f.desc)) sw.WriteLine($"{Ident(3)}desc = \"{f.desc}\"");
                            if (!string.IsNullOrEmpty(f.sprite)) sw.WriteLine($"{Ident(3)}picture = \"{f.sprite}\"");

                            if (f.requireIds != null && f.requireIds.Count > 0)
                            {
                                sw.WriteLine($"{Ident(3)}prerequisites = {{");
                                foreach (var r in f.requireIds) sw.WriteLine($"{Ident(4)}{r}");
                                sw.WriteLine($"{Ident(3)}}}");
                            }

                            if (f.preventsIds != null && f.preventsIds.Count > 0)
                            {
                                sw.WriteLine($"{Ident(3)}mutually_exclusive = {{");
                                foreach (var p in f.preventsIds) sw.WriteLine($"{Ident(4)}{p}");
                                sw.WriteLine($"{Ident(3)}}}");
                            }

                            // raw lines and onComplete
                            if (f.rawLines != null && f.rawLines.Count > 0)
                            {
                                WriteAllowedWithConversions(sw, f.rawLines, r => r.depth + 3, r => r.trimmedLine);
                            }

                            if (f.onComplete != null && f.onComplete.Count > 0)
                            {
                                sw.WriteLine($"{Ident(3)}on_completion = {{");
                                WriteAllowedWithConversions(sw, f.onComplete, r => r.depth + 3, r => r.trimmedLine);
                                sw.WriteLine($"{Ident(3)}}}");
                            }

                            sw.WriteLine($"{indent2}}}");
                        }
                    }
                }

                sw.WriteLine("}");
            });
        }
    }
}
