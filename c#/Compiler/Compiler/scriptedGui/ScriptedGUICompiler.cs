using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace Compiler.scriptedGui
{
    public class ScriptedGUICompiler : Compiler.BaseCompiler
    {
        public Compiler.ScriptedGUI PassedData { get; set; }
        string indent1 = Ident(1);
        string indent2 = Ident(2);
        string indent3 = Ident(3);
        string indent4 = Ident(4);

        public override void Compile()
        {
            if (PassedData == null) return;
            // Accumulate outputs in memory per target to avoid many file opens
            var sbLocalisationYml = new System.Text.StringBuilder(); // localisation/english/scripted_gui/ + _l_english.yml
            var sbCommonScriptedGuiTxt = new System.Text.StringBuilder(); // common/scripted_gui/<file>.txt
            var sbInterfaceGfx = new System.Text.StringBuilder(); // interface/<file>.gfx
            var sbInterfaceGui = new System.Text.StringBuilder(); // interface/<file>.gui (body only)
            var sbCommonScriptedLocalisationTxt = new System.Text.StringBuilder(); // common/scripted_localisation/<file>.txt

            // Use shared RenderAllowedToString from BaseCompiler where needed


            foreach (var window in PassedData.Windows)
            {
                int counterGuiEllement = 0;

                // Build common/scripted_gui block for this window
                sbCommonScriptedGuiTxt.AppendLine($"{indent1}{window.Id} = {{");
                sbCommonScriptedGuiTxt.AppendLine($"{indent2}window_name = \"{window.Id}\"");
                sbCommonScriptedGuiTxt.AppendLine($"{indent2}context_type = player_context");
                sbCommonScriptedGuiTxt.AppendLine($"{indent2}visible = {{");
                sbCommonScriptedGuiTxt.Append(RenderAllowedToString(window.VisibleRaw, rl => rl.depth + 1, rl => rl.trimmedLine));
                sbCommonScriptedGuiTxt.AppendLine($"{indent2}}}");
                sbCommonScriptedGuiTxt.AppendLine($"{indent2}properties = {{");
                foreach (var property in window.Properties)
                {
                    sbCommonScriptedGuiTxt.AppendLine($"{indent3}{property.ParentId} = {{");
                    if (property.Type == Compiler.GuiElementType.ProgressBar)
                    {
                        sbCommonScriptedGuiTxt.AppendLine($"{indent4}frame = {property.Id}");
                    }
                    else if (property.Type == Compiler.GuiElementType.Icon)
                    {
                        sbCommonScriptedGuiTxt.AppendLine($"{indent4}image = \"[{property.Id}]\"");
                    }
                    sbCommonScriptedGuiTxt.AppendLine($"{indent3}}}");
                }
                sbCommonScriptedGuiTxt.AppendLine($"{indent2}}}");

                sbCommonScriptedGuiTxt.AppendLine($"{indent2}effects = {{");
                foreach (var element in window.Elements)
                {
                    // only buttons currently produce effect blocks
                    if (element is ButtonElement btn)
                    {
                        sbCommonScriptedGuiTxt.AppendLine($"{indent3}{btn.Id}_click = {{");
                        sbCommonScriptedGuiTxt.Append(RenderAllowedToString(btn.OnClickRaw, rl => rl.depth + 1, rl => rl.trimmedLine));
                        sbCommonScriptedGuiTxt.AppendLine($"{indent3}}}");
                    }
                }
                sbCommonScriptedGuiTxt.AppendLine($"{indent2}}}");
                sbCommonScriptedGuiTxt.AppendLine("}");

                // Accumulate .gfx progressbar definitions for this window
                foreach (var element in window.Elements)
                {
                    if (element is ProgressBarElement pbe)
                    {
                        sbInterfaceGfx.AppendLine($"{indent1}progressbartype = {{");
                        sbInterfaceGfx.AppendLine($"{indent2}name = \"{pbe.Id}\"");
                        sbInterfaceGfx.AppendLine($"{indent2}textureFile1 = \"{pbe.ProgressedSprite}\"");
                        sbInterfaceGfx.AppendLine($"{indent2}textureFile2 = \"{pbe.UnprogressedSprite}\"");
                        if (pbe.ProgressedColor.HasValue)
                        {
                            var r = pbe.ProgressedColor.Value.r;
                            var g = pbe.ProgressedColor.Value.g;
                            var b = pbe.ProgressedColor.Value.b;
                            sbInterfaceGfx.AppendLine(indent2 + "color = { " + r.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + g.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + b.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " }");
                        }
                        else
                        {
                            sbInterfaceGfx.AppendLine($"{indent2}color = {{ 0.0 0.0 0.0 }}");
                        }
                        if (pbe.UnprogressedColor.HasValue)
                        {
                            var r2 = pbe.UnprogressedColor.Value.r;
                            var g2 = pbe.UnprogressedColor.Value.g;
                            var b2 = pbe.UnprogressedColor.Value.b;
                            sbInterfaceGfx.AppendLine(indent2 + "colortwo = { " + r2.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + g2.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + b2.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " }");
                        }
                        else
                        {
                            sbInterfaceGfx.AppendLine($"{indent2}colortwo = {{ 0.0 0.0 0.0 }}");
                        }
                        var sx = pbe.Size.HasValue ? pbe.Size.Value.x : 0;
                        var sy = pbe.Size.HasValue ? pbe.Size.Value.y : 0;
                        sbInterfaceGfx.AppendLine($"{indent2}size = {{ x = {sx} y = {sy} }}");
                        sbInterfaceGfx.AppendLine($"{indent2}steps = {pbe.Steps}");
                        sbInterfaceGfx.AppendLine($"{indent2}effectFile = \"gfx/FX/progress.lua\"");
                        if (pbe.Orientation == Orientation.Horizontal)
                        {
                            sbInterfaceGfx.AppendLine($"{indent2}horizontal = yes");
                        }
                        else
                        {
                            sbInterfaceGfx.AppendLine($"{indent2}horizontal = no");
                        }

                        sbInterfaceGfx.AppendLine($"{indent1}}}");
                    }
                }
            
                // Accumulate this window's GUI body in memory so we can write once per target file later
                sbInterfaceGui.AppendLine($"{indent2}name = \"{window.Id}\"");
                if (window.Draggable) sbInterfaceGui.AppendLine($"{indent2}moveable = yes");
                sbInterfaceGui.AppendLine($"{indent2}size = {{ x = {window.Size.Value.x} y = {window.Size.Value.y} }}");
                sbInterfaceGui.AppendLine($"{indent2}position = {{ x = {window.Position.Value.x} y = {window.Position.Value.y} }}");
                if (window.Sprite != null)
                {
                    sbInterfaceGui.AppendLine($"{indent2}background = {{");
                    sbInterfaceGui.AppendLine($"{indent3}name = \"Background\"");
                    sbInterfaceGui.AppendLine($"{indent3}quadTextureSprite = \"{window.Sprite}\"");
                    sbInterfaceGui.AppendLine($"{indent2}}}");
                }

                foreach (var element in window.Elements)
                {
                    if (element is TextElement te)
                    {
                        sbInterfaceGui.AppendLine($"{indent2}instantTextBoxType = {{");
                        sbInterfaceGui.AppendLine($"{indent3}name = \"{te.Id}\"");
                        if (te.IsTextScriptedLocalisationId)
                        {
                            sbInterfaceGui.AppendLine($"{indent3}text = \"[{te.DefinesId}]\"");
                        }
                        else
                        {
                            sbInterfaceGui.AppendLine($"{indent3}text = {te.Id}");
                            sbLocalisationYml.AppendLine($" {te.Id}_gui_ellement_{counterGuiEllement}:0 \"{te.Text}\"");
                            counterGuiEllement++;
                        }
                        sbInterfaceGui.AppendLine($"{indent3}font = \"{te.Font}\"");
                        sbInterfaceGui.AppendLine($"{indent3}position = {{ x = {te.Position.Value.x} y = {te.Position.Value.y} }}");
                        sbInterfaceGui.AppendLine($"{indent3}maxWidth = {te.MaxSize.Value.x}");
                        sbInterfaceGui.AppendLine($"{indent3}maxHeight = {te.MaxSize.Value.y}");

                        sbInterfaceGui.AppendLine($"{indent2}}}");
                    }
                    else if (element is IconElement ie)
                    {
                        sbInterfaceGui.AppendLine($"{indent2}iconType = {{");
                        sbInterfaceGui.AppendLine($"{indent3}name = \"{ie.Id}\"");
                        if (ie.IsProperty)
                        {
                        }
                        else
                        {
                            sbInterfaceGui.AppendLine($"{indent3}spriteType = \"{ie.Sprite}\"");
                        }
                        if (ie.IsTextScriptedLocalisationId)
                        {
                            sbInterfaceGui.AppendLine($"{indent3}text = {ie.Id}");
                            sbLocalisationYml.AppendLine($" {ie.Id}_gui_ellement_{counterGuiEllement}:0 \"{ie.Text}\"");
                            counterGuiEllement++;
                        }
                        sbInterfaceGui.AppendLine($"{indent3}font = \"{ie.Font}\"");
                        sbInterfaceGui.AppendLine($"{indent3}position = {{ x = {ie.Position.Value.x} y = {ie.Position.Value.y} }}");
                        if (ie.SizePercent != null)
                        {
                            sbInterfaceGui.AppendLine($"{indent3}scale = {ie.SizePercent}");
                        }
                        sbInterfaceGui.AppendLine($"{indent2}}}");
                    }
                    else if (element is ButtonElement be)
                    {
                        sbInterfaceGui.AppendLine($"{indent2}iconType = {{");
                        sbInterfaceGui.AppendLine($"{indent3}name = \"{be.Id}\"");
                        if (be.IsProperty)
                        {
                        }
                        else
                        {
                            sbInterfaceGui.AppendLine($"{indent3}spriteType = \"{be.Sprite}\"");
                        }
                        sbInterfaceGui.AppendLine($"{indent3}position = {{ x = {be.Position.Value.x} y = {be.Position.Value.y} }}");
                        if (be.SizePercent != null)
                        {
                            sbInterfaceGui.AppendLine($"{indent3}scale = {be.SizePercent}");
                        }
                        sbInterfaceGui.AppendLine($"{indent2}}}");
                    }
                    else if (element is ProgressBarElement pe)
                    {
                        sbInterfaceGui.AppendLine($"{indent2}iconType = {{");
                        sbInterfaceGui.AppendLine($"{indent3}name = \"{pe.Id}\"");
                        sbInterfaceGui.AppendLine($"{indent3}spriteType = \"GFX_{pe.Id}\"");
                        sbInterfaceGui.AppendLine($"{indent3}position = {{ x = {pe.Position.Value.x} y = {pe.Position.Value.y} }}");
                        sbInterfaceGui.AppendLine($"{indent2}}}");
                    }
                }
            }
            // Close gui top-level container
            sbInterfaceGui.AppendLine($"{indent1}}}");
            sbInterfaceGui.AppendLine("}");

            // Close gfx top-level
            sbInterfaceGfx.AppendLine("}");

            // common scripted_gui trailing close already appended per-window in loop, but ensure global close
            sbCommonScriptedGuiTxt.AppendLine("}");

            // Now write buffers out once per file using WriteFile so the created flag is respected
            WriteFile("common/scripted_gui", PassedData.SourceFileName, ".txt", (sw, created) =>
            {
                if (created) sw.WriteLine("scripted_gui = {");
                if (sbCommonScriptedGuiTxt.Length > 0) sw.Write(sbCommonScriptedGuiTxt.ToString());
            });

            WriteFile("interface", PassedData.SourceFileName, ".gfx", (sw, created) =>
            {
                if (created) sw.WriteLine("spriteTypes = {");
                if (sbInterfaceGfx.Length > 0) sw.Write(sbInterfaceGfx.ToString());
            });

            WriteFile("interface", PassedData.SourceFileName, ".gui", (sw, created) =>
            {
                if (created) {
                    sw.WriteLine("guiTypes = {");
                    sw.WriteLine($"{indent1}containerWindowType = {{");
                }
                if (sbInterfaceGui.Length > 0) sw.Write(sbInterfaceGui.ToString());
            });
            


            foreach (var define in PassedData.Defines)
            {
                int counterDefines = 0;
                // build the common scripted_localisation entry in-memory
                sbCommonScriptedLocalisationTxt.AppendLine("defined_text = {");
                sbCommonScriptedLocalisationTxt.AppendLine($"{indent1}name = {define.Id}");
                foreach (var DefineBranch in define.Branches)
                {
                    sbCommonScriptedLocalisationTxt.AppendLine($"{indent1}text = {{");
                    if (!DefineBranch.IsElse)
                    {
                        sbCommonScriptedLocalisationTxt.AppendLine($"{indent2}trigger = {{");
                    sbCommonScriptedLocalisationTxt.Append(RenderAllowedToString(DefineBranch.ConditionRaw, rl => rl.depth + 1, rl => rl.trimmedLine));
                        sbCommonScriptedLocalisationTxt.AppendLine($"{indent2}}}");
                    }

                    if (define.Type == Compiler.DefineType.Text)
                    {
                        sbCommonScriptedLocalisationTxt.AppendLine($"{indent2}localization_key = {define.Id}_{counterDefines}");
                        // Add localisation yml entry with same index and then increment
                        sbLocalisationYml.AppendLine($" {define.Id}_{counterDefines}:0 \"{DefineBranch.ThenBlock}\"");
                        counterDefines++;
                    }
                    else if (define.Type == Compiler.DefineType.Sprite)
                    {
                        sbCommonScriptedLocalisationTxt.AppendLine($"{indent2}localization_key = \"{DefineBranch.ThenBlock}\"");
                    }
                    sbCommonScriptedLocalisationTxt.AppendLine($"{indent1}}}");
                }
                sbCommonScriptedLocalisationTxt.AppendLine("}");
            }

            // Write common scripted_localisation and localisation yml buffers
            WriteFile("common/scripted_localisation", PassedData.SourceFileName, ".txt", (sw, created) =>
            {
                if (created) { }
                if (sbCommonScriptedLocalisationTxt.Length > 0) sw.Write(sbCommonScriptedLocalisationTxt.ToString());
            });

            WriteFile("localisation/english/scripted_gui/", PassedData.SourceFileName + "_l_english", ".yml", (sw, created) =>
            {
                if (created) sw.WriteLine("l_english:");
                if (sbLocalisationYml.Length > 0) sw.Write(sbLocalisationYml.ToString());
            });

        }
    }
}
