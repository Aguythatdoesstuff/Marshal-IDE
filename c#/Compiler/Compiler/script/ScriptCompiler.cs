using System;
using System.IO;
using System.Linq;

namespace Compiler.script
{
    public class ScriptCompiler : BaseCompiler
    {
        // Parsed scripts provided by the parser
        public Compiler.ParsedScriptFile PassedData { get; set; }

        public override void Compile()
        {
            if (PassedData == null) return;
            // Write scripted effects
            if (PassedData.ScriptedEffects != null && PassedData.ScriptedEffects.Count > 0)
            {
                WriteFile("common/scripted_effects", PassedData.SourceFileName, ".txt", sw =>
                {
                    foreach (var se in PassedData.ScriptedEffects)
                    {
                        sw.WriteLine($"{se.id} = {{");
                        foreach (var raw in se.rawLines)
                        {
                            // Use helper to write raw lines. No extra offset here, original used Ident(raw.depth)
                            WriteAllowedWithConversions(sw, se.rawLines, r => r.depth, r => r.trimmedLine);
                            break;
                        }
                        sw.WriteLine("}\n");
                    }
                });
            }

            // Write game rules
            if (PassedData.GameRules != null && PassedData.GameRules.Count > 0)
            {
                bool hasWrittenLocHeader = false;
                WriteFile("common/game_rules", PassedData.SourceFileName, ".txt", sw =>
                {
                    foreach (var gr in PassedData.GameRules)
                    {
                        // rule header
                        sw.WriteLine($"{gr.id} = {{");

                        // name and group reference keys
                        var idUpper = gr.id.ToUpperInvariant();
                        sw.WriteLine($"{Ident(1)}name = \"RULE_{idUpper}\"");
                        sw.WriteLine($"{Ident(1)}group = \"GROUP_{idUpper}\"");

                        // options
                        foreach (var opt in gr.options)
                        {
                            var optUpper = (opt.name ?? string.Empty).ToUpperInvariant().Replace(' ', '_');
                            if (opt.isDefault)
                            {
                                sw.WriteLine($"{Ident(1)}default = {{");
                                sw.WriteLine($"{Ident(2)}name = {optUpper}");
                                sw.WriteLine($"{Ident(2)}text = \"RULE_{idUpper}_{optUpper}\"");
                                sw.WriteLine($"{Ident(1)}}}");
                            }
                            else
                            {
                                sw.WriteLine($"{Ident(1)}option = {{");
                                sw.WriteLine($"{Ident(2)}name = {optUpper}");
                                sw.WriteLine($"{Ident(2)}text = \"RULE_{idUpper}_{optUpper}\"");
                                sw.WriteLine($"{Ident(1)}}}");
                            }
                        }

                        sw.WriteLine("}\n");
                    }
                });

                // localisation for game rules
                WriteFile("localisation/english/game_rules", PassedData.SourceFileName + "_l_english", ".yml", sw =>
                {
                    sw.WriteLine("l_english:");
                    foreach (var gr in PassedData.GameRules)
                    {
                        var idUpper = gr.id.ToUpperInvariant();
                        // rule name
                        sw.WriteLine($" RULE_{idUpper}:0 \"{EscapeForYml(gr.name)}\"");
                        // group text
                        sw.WriteLine($" GROUP_{idUpper}:0 \"{EscapeForYml(gr.group)}\"");

                        foreach (var opt in gr.options)
                        {
                            var optUpper = (opt.name ?? string.Empty).ToUpperInvariant().Replace(' ', '_');
                            sw.WriteLine($" RULE_{idUpper}_{optUpper}:0 \"{EscapeForYml(opt.name)}\"");
                        }
                    }
                });
            }

            // Scripted triggers
            if (PassedData.ScriptedTriggers != null && PassedData.ScriptedTriggers.Count > 0)
            {
                WriteFile("common/scripted_triggers", PassedData.SourceFileName, ".txt", sw =>
                {
                    foreach (var st in PassedData.ScriptedTriggers)
                    {
                        sw.WriteLine($"{st.id} = {{");
                        WriteAllowedWithConversions(sw, st.rawLines, r => r.depth, r => r.trimmedLine);
                        sw.WriteLine("}\n");
                    }
                });
            }

            // On actions
            if (PassedData.OnActions != null && PassedData.OnActions.Count > 0)
            {
                WriteFile("common/on_actions", PassedData.SourceFileName, ".txt", sw =>
                {
                    sw.WriteLine("on_actions = {");
                    foreach (var oa in PassedData.OnActions)
                    {
                        WriteAllowedWithConversions(sw, oa.rawLines, r => r.depth, r => r.trimmedLine);
                        sw.WriteLine();
                    }
                    sw.WriteLine("}\n");
                });
            }
        }

        private static string EscapeForYml(string s)
        {
            if (s == null) return string.Empty;
            return s.Replace("\"", "\\\"");
        }
    }
}
