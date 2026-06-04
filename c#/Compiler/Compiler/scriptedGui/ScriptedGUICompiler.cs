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
            WriteFile("localisation/english/scripted_gui/", PassedData.SourceFileName + "_l_english", ".yml", (sw, created) =>
            {
                if (created) sw.WriteLine("l_english:");
            });
            WriteFile("common/scripted_gui", PassedData.SourceFileName, ".txt", (sw, created) =>
            {
                sw.WriteLine("scripted_gui = {");
            });
            WriteFile("interface", PassedData.SourceFileName, ".gfx", (sw, created) =>
            {
                sw.WriteLine("spriteTypes = {");
            });
            WriteFile("interface", PassedData.SourceFileName, ".gui", (sw, created) =>
            {
                sw.WriteLine("guiTypes = {");
                sw.WriteLine($"{indent1}containerWindowType = {{");
            });


            foreach (var window in PassedData.Windows)
            {
                int counterGuiEllement = 0;
                WriteFile("common/scripted_gui", PassedData.SourceFileName, ".txt", (sw, created) =>
                {
                    sw.WriteLine($"{indent1}{window.Id} = {{");
                    sw.WriteLine($"{indent2}window_name = \"{window.Id}\"");
                    sw.WriteLine($"{indent2}context_type = player_context");
                    sw.WriteLine($"{indent2}visible = {{");
                    WriteAllowedWithConversions(sw, window.VisibleRaw, rl => rl.depth + 1, rl => rl.trimmedLine);
                    sw.WriteLine($"{indent2}}}");
                    sw.WriteLine($"{indent2}properties = {{");
                    foreach (var property in window.Properties)
                    {
                        sw.WriteLine($"{indent3}{property.ParentId} = {{");
                        if (property.Type == GuiElementType.ProgressBar)
                        {
                            sw.WriteLine($"{indent4}frame = {property.Id}");
                        }
                        else if (property.Type == GuiElementType.Icon)
                        {
                            sw.WriteLine($"{indent4}image = \"[{property.Id}]\"");
                        }
                        sw.WriteLine($"{indent3}}}");
                    }
                    sw.WriteLine($"{indent2}}}");

                    sw.WriteLine($"{indent2}effects = {{");
                    foreach (var Ellement in window.Elements)
                    {
                        if (Ellement.Type == GuiElementType.Button)
                        {
                            sw.WriteLine($"{indent3}{Ellement.Id}_click = {{");
                            WriteAllowedWithConversions(sw, Ellement.OnClickRaw, rl => rl.depth + 1, rl => rl.trimmedLine);
                            sw.WriteLine($"{indent3}}}");
                        }
                    }
                    sw.WriteLine($"{indent2}}}");
                    sw.WriteLine("}");
                });

                WriteFile("interface", PassedData.SourceFileName, ".gfx", (sw, created) =>
                {
                    foreach (var Ellement in window.Elements)
                    {
                        if (Ellement.Type == GuiElementType.ProgressBar)
                        {
                            var pbe = Ellement as Compiler.GuiElement.ProgressBarElement;
                            if (pbe == null) continue;
                            sw.WriteLine($"{indent1}progressbartype = {{");
                            sw.WriteLine($"{indent2}name = \"{pbe.Id}\"");
                            sw.WriteLine($"{indent2}textureFile1 = \"{pbe.ProgressedSprite}\"");
                            sw.WriteLine($"{indent2}textureFile2 = \"{pbe.UnprogressedSprite}\"");
                            if (pbe.ProgressedColor.HasValue)
                            {
                                var r = pbe.ProgressedColor.Value.r;
                                var g = pbe.ProgressedColor.Value.g;
                                var b = pbe.ProgressedColor.Value.b;
                                sw.WriteLine(indent2 + "color = { " + r.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + g.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + b.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " }");
                            }
                            else
                            {
                                sw.WriteLine($"{indent2}color = {{ 0.0 0.0 0.0 }}");
                            }
                            if (pbe.UnprogressedColor.HasValue)
                            {
                                var r2 = pbe.UnprogressedColor.Value.r;
                                var g2 = pbe.UnprogressedColor.Value.g;
                                var b2 = pbe.UnprogressedColor.Value.b;
                                sw.WriteLine(indent2 + "colortwo = { " + r2.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + g2.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " " + b2.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " }");
                            }
                            else
                            {
                                sw.WriteLine($"{indent2}colortwo = {{ 0.0 0.0 0.0 }}");
                            }
                            var sx = pbe.Size.HasValue ? pbe.Size.Value.x : 0;
                            var sy = pbe.Size.HasValue ? pbe.Size.Value.y : 0;
                            sw.WriteLine($"{indent2}size = {{ x = {sx} y = {sy} }}");
                            sw.WriteLine($"{indent2}steps = {pbe.Steps}");
                            sw.WriteLine($"{indent2}effectFile = \"gfx/FX/progress.lua\"");
                            if (pbe.Orientation == Compiler.Orientation.Horizontal)
                            {
                                sw.WriteLine($"{indent2}horizontal = yes");
                            }
                            else
                            {
                                sw.WriteLine($"{indent2}horizontal = no");
                            }

                            sw.WriteLine($"{indent1}}}");
                        }
                    }
                });
            
                WriteFile("interface", PassedData.SourceFileName, ".gui", (sw, created) =>
                {
                    sw.WriteLine($"{indent2}name = \"{window.Id}\"");
                    if (window.Draggable) sw.WriteLine($"{indent2}moveable = yes");
                    sw.WriteLine($"{indent2}size = {{ x = {window.Size.Value.x} y = {window.Size.Value.y} }}");
                    sw.WriteLine($"{indent2}position = {{ x = {window.Position.Value.x} y = {window.Position.Value.y} }}");
                    if (window.Sprite != null)
                    {
                        sw.WriteLine($"{indent2}background = {{");
                        sw.WriteLine($"{indent3}name = \"Background\"");
                        sw.WriteLine($"{indent3}quadTextureSprite = \"{window.Sprite}\"");
                        sw.WriteLine($"{indent2}}}");
                    }


                    foreach (var Ellement in window.Elements)
                    {
                        if (Ellement.Type == GuiElementType.Text)
                        {
                            sw.WriteLine($"{indent2}instantTextBoxType = {{");
                            sw.WriteLine($"{indent3}name = \"{Ellement.Id}\"");
                            if (Ellement.IsTextScriptedLocalisationId)
                            {
                                sw.WriteLine($"{indent3}text = \"[{Ellement.DefinesId}]\"");
                            }
                            else
                            {
                                sw.WriteLine($"{indent3}text = {Ellement.Id}");
                                WriteFile("localisation/english/scripted_gui/", PassedData.SourceFileName + "_l_english", ".yml", (sw, created) =>
                                {
                                    sw.WriteLine($" {Ellement.Id}_gui_ellement_{counterGuiEllement}:0 \"{Ellement.Text}\"");
                                });
                            }
                            sw.WriteLine($"{indent3}font = \"{Ellement.Font}\"");
                            sw.WriteLine($"{indent3}position = {{ x = {Ellement.Position.Value.x} y = {Ellement.Position.Value.y} }}");
                            sw.WriteLine($"{indent3}maxWidth = {Ellement.MaxSize.Value.x}");
                            sw.WriteLine($"{indent3}maxHeight = {Ellement.MaxSize.Value.y}");

                            sw.WriteLine($"{indent2}}}");
                        }
                        else if (Ellement.Type == GuiElementType.Icon)
                        {
                            sw.WriteLine($"{indent2}iconType = {{");
                            sw.WriteLine($"{indent3}name = \"{Ellement.Id}\"");
                            if (Ellement.IsProperty)
                            {
                            }
                            else
                            {
                                sw.WriteLine($"{indent3}spriteType = \"{Ellement.Sprite}\"");
                            }
                            if (Ellement.IsTextScriptedLocalisationId)
                            {
                                sw.WriteLine($"{indent3}text = {Ellement.Id}");
                                WriteFile("localisation/english/scripted_gui/", PassedData.SourceFileName + "_l_english", ".yml", (sw, created) =>
                                {
                                    sw.WriteLine($" {Ellement.Id}_gui_ellement_{counterGuiEllement}:0 \"{Ellement.Text}\"");
                                });
                            }
                            sw.WriteLine($"{indent3}font = \"{Ellement.Font}\"");
                            sw.WriteLine($"{indent3}position = {{ x = {Ellement.Position.Value.x} y = {Ellement.Position.Value.y} }}");
                            if (Ellement.SizePercent != null)
                            {
                                sw.WriteLine($"{indent3}scale = {Ellement.SizePercent}");
                            }
                            sw.WriteLine($"{indent2}}}");
                        }
                        else if (Ellement.Type == GuiElementType.Button)
                        {
                            sw.WriteLine($"{indent2}iconType = {{");
                            sw.WriteLine($"{indent3}name = \"{Ellement.Id}\"");
                            if (Ellement.IsProperty)
                            {
                            }
                            else
                            {
                                sw.WriteLine($"{indent3}spriteType = \"{Ellement.Sprite}\"");
                            }
                            sw.WriteLine($"{indent3}position = {{ x = {Ellement.Position.Value.x} y = {Ellement.Position.Value.y} }}");
                            if (Ellement.SizePercent != null)
                            {
                                sw.WriteLine($"{indent3}scale = {Ellement.SizePercent}");
                            }
                            sw.WriteLine($"{indent2}}}");
                        }
                        else if (Ellement.Type == GuiElementType.ProgressBar)
                        {
                            sw.WriteLine($"{indent2}iconType = {{");
                            sw.WriteLine($"{indent3}name = \"{Ellement.Id}\"");
                            sw.WriteLine($"{indent3}spriteType = \"GFX_{Ellement.Id}\"");
                            sw.WriteLine($"{indent3}position = {{ x = {Ellement.Position.Value.x} y = {Ellement.Position.Value.y} }}");
                            sw.WriteLine($"{indent2}}}");
                        }
                    }
                });
            }
            WriteFile("interface", PassedData.SourceFileName, ".gui", (sw, created) =>
            {
                sw.WriteLine($"{indent1}}}");
                sw.WriteLine("}");
            });
            WriteFile("interface", PassedData.SourceFileName, ".gfx", (sw, created) =>
            {
                sw.WriteLine("}");
            });
            WriteFile("common/scripted_gui", PassedData.SourceFileName, ".txt", (sw, created) =>
            {
                sw.WriteLine("}");
            });
            


            foreach (var define in PassedData.Defines)
            {
                int counterDefines = 0;
                WriteFile("common/scripted_localisation", PassedData.SourceFileName, ".txt", (sw, created) =>
                {
                    sw.WriteLine("defined_text = {");
                    sw.WriteLine($"{indent1}name = {define.Id}");
                    foreach (var DefineBranch in define.Branches)
                    {
                        sw.WriteLine($"{indent1}text = {{");
                        if (!DefineBranch.IsElse)
                        {
                            sw.WriteLine($"{indent2}trigger = {{");
                            WriteAllowedWithConversions(sw, DefineBranch.ConditionRaw, rl => rl.depth + 1, rl => rl.trimmedLine);
                            sw.WriteLine($"{indent2}}}");
                        }

                        if (define.Type == Compiler.DefineType.Text)
                        {
                            sw.WriteLine($"{indent2}localization_key = {define.Id}_{counterDefines++}");

                            
                            WriteFile("localisation/english/scripted_gui/", PassedData.SourceFileName + "_l_english", ".yml", (sw, created) =>
                            {
                                sw.WriteLine($" {define.Id}_{counterDefines}:0 \"{DefineBranch.ThenBlock}\"");
                            });
                        }
                        else if (define.Type == Compiler.DefineType.Sprite)
                        {
                            sw.WriteLine($"{indent2}localization_key = \"{DefineBranch.ThenBlock}\"");
                        }
                        sw.WriteLine($"{indent1}}}");
                    }
                    sw.WriteLine("}");
                });
            }

        }
    }
}
