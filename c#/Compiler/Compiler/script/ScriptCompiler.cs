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
            // Prepare in-memory buffers for each target so we only call WriteFile once per file
            var sbScriptedEffects = new System.Text.StringBuilder();
            var sbGameRules = new System.Text.StringBuilder();
            var sbGameRulesLoc = new System.Text.StringBuilder();
            var sbScriptedTriggers = new System.Text.StringBuilder();
            var sbOnActions = new System.Text.StringBuilder();

            // Use shared RenderAllowedToString from BaseCompiler where needed

            // Scripted effects
            if (PassedData.ScriptedEffects != null && PassedData.ScriptedEffects.Count > 0)
            {
                foreach (var se in PassedData.ScriptedEffects)
                {
                    sbScriptedEffects.AppendLine($"{se.id} = {{");
                    sbScriptedEffects.Append(RenderAllowedToString(se.rawLines, r => r.depth, r => r.trimmedLine));
                    sbScriptedEffects.AppendLine("}\n");
                }
            }

            // Game rules
            if (PassedData.GameRules != null && PassedData.GameRules.Count > 0)
            {
                foreach (var gr in PassedData.GameRules)
                {
                    var idUpper = gr.id.ToUpperInvariant();
                    sbGameRules.AppendLine($"{gr.id} = {{");
                    sbGameRules.AppendLine($"{Ident(1)}name = \"RULE_{idUpper}\"");
                    sbGameRules.AppendLine($"{Ident(1)}group = \"GROUP_{idUpper}\"");

                    foreach (var opt in gr.options)
                    {
                        var optUpper = (opt.name ?? string.Empty).ToUpperInvariant().Replace(' ', '_');
                        if (opt.isDefault)
                        {
                            sbGameRules.AppendLine($"{Ident(1)}default = {{");
                            sbGameRules.AppendLine($"{Ident(2)}name = {optUpper}");
                            sbGameRules.AppendLine($"{Ident(2)}text = \"RULE_{idUpper}_{optUpper}\"");
                            sbGameRules.AppendLine($"{Ident(1)}}}");
                        }
                        else
                        {
                            sbGameRules.AppendLine($"{Ident(1)}option = {{");
                            sbGameRules.AppendLine($"{Ident(2)}name = {optUpper}");
                            sbGameRules.AppendLine($"{Ident(2)}text = \"RULE_{idUpper}_{optUpper}\"");
                            sbGameRules.AppendLine($"{Ident(1)}}}");
                        }
                    }

                    sbGameRules.AppendLine("}\n");

                    // localisation entries
                    sbGameRulesLoc.AppendLine($" RULE_{idUpper}:0 \"{EscapeForYml(gr.name)}\"");
                    sbGameRulesLoc.AppendLine($" GROUP_{idUpper}:0 \"{EscapeForYml(gr.group)}\"");
                    foreach (var opt in gr.options)
                    {
                        var optUpper = (opt.name ?? string.Empty).ToUpperInvariant().Replace(' ', '_');
                        sbGameRulesLoc.AppendLine($" RULE_{idUpper}_{optUpper}:0 \"{EscapeForYml(opt.name)}\"");
                    }
                }
            }

            // Scripted triggers
            if (PassedData.ScriptedTriggers != null && PassedData.ScriptedTriggers.Count > 0)
            {
                foreach (var st in PassedData.ScriptedTriggers)
                {
                    sbScriptedTriggers.AppendLine($"{st.id} = {{");
                    sbScriptedTriggers.Append(RenderAllowedToString(st.rawLines, r => r.depth, r => r.trimmedLine));
                    sbScriptedTriggers.AppendLine("}\n");
                }
            }

            // On actions
            if (PassedData.OnActions != null && PassedData.OnActions.Count > 0)
            {
                sbOnActions.AppendLine("on_actions = {");
                foreach (var oa in PassedData.OnActions)
                {
                    sbOnActions.Append(RenderAllowedToString(oa.rawLines, r => r.depth, r => r.trimmedLine));
                    sbOnActions.AppendLine();
                }
                sbOnActions.AppendLine("}\n");
            }

            // Flush buffers using WriteFile once per target, preserving created header logic
            if (sbScriptedEffects.Length > 0)
            {
                WriteFile("common/scripted_effects", PassedData.SourceFileName, ".txt", (sw, created) =>
                {
                    sw.Write(sbScriptedEffects.ToString());
                });
            }

            if (sbGameRules.Length > 0)
            {
                WriteFile("common/game_rules", PassedData.SourceFileName, ".txt", (sw, created) =>
                {
                    sw.Write(sbGameRules.ToString());
                });
            }

            if (sbGameRulesLoc.Length > 0)
            {
                WriteFile("localisation/english/game_rules", PassedData.SourceFileName + "_l_english", ".yml", (sw, created) =>
                {
                    if (created) sw.WriteLine("l_english:");
                    sw.Write(sbGameRulesLoc.ToString());
                });
            }

            if (sbScriptedTriggers.Length > 0)
            {
                WriteFile("common/scripted_triggers", PassedData.SourceFileName, ".txt", (sw, created) =>
                {
                    sw.Write(sbScriptedTriggers.ToString());
                });
            }

            if (sbOnActions.Length > 0)
            {
                WriteFile("common/on_actions", PassedData.SourceFileName, ".txt", (sw, created) =>
                {
                    sw.Write(sbOnActions.ToString());
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
