using System;
using System.Collections.Generic;
using System.Linq;

namespace Compiler
{
    // Lightweight models to hold parsed GUI data. Parser is best-effort and
    // deliberately permissive: validation is the validator's responsibility.
    public class DefineBranch
    {
        public string ConditionRaw = string.Empty; // raw lines that make up the if / else if condition
        public string ThenRaw = string.Empty; // raw lines inside the then branch
        public bool IsElse = false;
    }

    public class Define
    {
        public DefineType Type; // sprite or text
        public string Id;
        public List<DefineBranch> Branches = new List<DefineBranch>();
    }

    public abstract class GuiElement
    {
        public GuiElementType Type;
        public string Id; // optional (only set when quoted id present after header)
        public string Sprite;
        public string Text;
        public string Font;
        public (int x, int y)? Position;
        public (int x, int y)? Size; // pixel size
        public bool SizeIsPercent = false;
        public int? SizePercent = null; // when size specified as percent (element-level)
        public bool IsTextScriptedLocalisationId = false;
        public bool SpriteIsScriptedId = false;
        public string OnClickRaw = string.Empty;
        public string Raw = string.Empty; // raw aggregated inner lines for debugging
    }

    public class TextElement : GuiElement { }
    public class ButtonElement : GuiElement { }
    public class IconElement : GuiElement { }
    public class ProgressBarElement : GuiElement
    {
        public Orientation Orientation; // horizontal | vertical
        public int Steps;
        public (double r, double g, double b)? ProgressedColor;
        public (double r, double g, double b)? UnprogressedColor;
        public string VarName;
        public string ProgressedSprite;
        public string UnprogressedSprite;
    }

    public enum DefineType
    {
        Sprite,
        Text
    }

    public enum GuiElementType
    {
        Text,
        Button,
        Icon,
        ProgressBar
    }

    public enum Orientation
    {
        Horizontal,
        Vertical
    }

    public class Window
    {
        public bool Draggable;
        public string Id;
        public (int x, int y)? Size;
        public bool SizeIsPercent;
        public (int x, int y)? Position;
        public string Sprite;
        public string VisibleRaw = string.Empty;
        public List<GuiElement> Elements = new List<GuiElement>();
    }

    public class ScriptedGUI
    {
        public List<Define> Defines = new List<Define>();
        public List<Window> Windows = new List<Window>();
    }

    public class ScriptedGUIParser : BaseParser
    {
        public ScriptedGUI Result { get; } = new ScriptedGUI();

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {
            Console.WriteLine($"[PARSER] ScriptedGUIParser starting for {fileName} ({preprocessedLines.Count} lines)");

            Window currentWindow = null;
            GuiElement currentElement = null;
            Define currentDefine = null;

            bool inOnClick = false; int onClickBaseDepth = 0;
            bool inVisible = false; int visibleBaseDepth = 0;

            // For define parsing
            DefineBranch currentBranch = null;
            bool inDefine = false; int defineBaseDepth = 0;

            for (int i = 0; i < preprocessedLines.Count; i++)
            {
                var pl = preprocessedLines[i];
                Console.WriteLine($"[PARSER] Line {pl.LineNumber} Depth {pl.Depth}: {pl.TrimmedLine}");

                // If currently collecting an on click block for an element
                if (inOnClick)
                {
                    if (pl.Depth > onClickBaseDepth)
                    {
                        currentElement.OnClickRaw += (string.IsNullOrEmpty(currentElement.OnClickRaw) ? "" : "\n") + pl.TrimmedLine;
                        continue;
                    }
                    else
                    {
                        inOnClick = false;
                        // reprocess this line in outer context
                        i--;
                        continue;
                    }
                }

                if (inVisible)
                {
                    if (pl.Depth > visibleBaseDepth)
                    {
                        currentWindow.VisibleRaw += (string.IsNullOrEmpty(currentWindow.VisibleRaw) ? "" : "\n") + pl.TrimmedLine;
                        continue;
                    }
                    else
                    {
                        inVisible = false;
                        i--;
                        continue;
                    }
                }

                if (inDefine)
                {
                    if (pl.Depth > defineBaseDepth)
                    {
                        // handle if / else if / else / then and collect raw content
                        string t = pl.TrimmedLine;
                        if (t.Equals("if", StringComparison.OrdinalIgnoreCase) || t.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
                        {
                            currentBranch = new DefineBranch();
                            currentDefine.Branches.Add(currentBranch);
                            currentBranch.ConditionRaw = string.Empty;
                            currentBranch.ThenRaw = string.Empty;
                            currentBranch.IsElse = false;
                            continue;
                        }
                        if (t.StartsWith("else if", StringComparison.OrdinalIgnoreCase))
                        {
                            currentBranch = new DefineBranch();
                            currentDefine.Branches.Add(currentBranch);
                            currentBranch.ConditionRaw = t; // store the else if line as condition starter
                            currentBranch.ThenRaw = string.Empty;
                            currentBranch.IsElse = false;
                            continue;
                        }
                        if (t.Equals("else", StringComparison.OrdinalIgnoreCase))
                        {
                            currentBranch = new DefineBranch();
                            currentBranch.IsElse = true;
                            currentDefine.Branches.Add(currentBranch);
                            continue;
                        }
                        if (t.Equals("then", StringComparison.OrdinalIgnoreCase))
                        {
                            // subsequent deeper lines belong to then content; we'll collect them
                            // mark by setting a placeholder if needed
                            continue;
                        }

                        // if we have a branch and line is deeper treat as condition or then depending
                        if (currentBranch != null)
                        {
                            // Heuristic: if currentBranch.ConditionRaw is empty and line starts at defineBaseDepth+1
                            // we append to ConditionRaw until we see 'then' keyword (we don't detect here),
                            // but simplest is to append everything into ConditionRaw unless it looks like a 'sprite' or 'text' which we treat as ThenRaw.
                            if (t.StartsWith("sprite", StringComparison.OrdinalIgnoreCase) || t.StartsWith("text", StringComparison.OrdinalIgnoreCase))
                            {
                                currentBranch.ThenRaw += (string.IsNullOrEmpty(currentBranch.ThenRaw) ? "" : "\n") + t;
                            }
                            else
                            {
                                currentBranch.ConditionRaw += (string.IsNullOrEmpty(currentBranch.ConditionRaw) ? "" : "\n") + t;
                            }
                            continue;
                        }

                        // otherwise just append to a generic define debug raw (fallback)
                        currentDefine.Branches.Add(new DefineBranch { ConditionRaw = pl.TrimmedLine });
                        continue;
                    }
                    else
                    {
                        inDefine = false;
                        currentBranch = null;
                        currentDefine = null;
                        i--;
                        continue;
                    }
                }

                // Root-level constructs
                if (pl.Depth == 0)
                {
                    currentElement = null;
                    // define
                    if (pl.TrimmedLine.StartsWith("define ", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = pl.TrimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        currentDefine = new Define();
                        if (parts.Length >= 2)
                        {
                            // define sprite NAME  OR define text NAME
                            if (parts.Length >= 3)
                            {
                                var t = parts[1].ToLowerInvariant();
                                currentDefine.Type = t == "sprite" ? DefineType.Sprite : DefineType.Text;
                                currentDefine.Id = BaseParser.GetQuotedContent(pl.TrimmedLine.Substring(pl.TrimmedLine.IndexOf(parts[2]))).Trim();
                            }
                            else
                            {
                                var t = parts[1].ToLowerInvariant();
                                currentDefine.Type = t == "sprite" ? DefineType.Sprite : DefineType.Text;
                            }
                        }
                        Result.Defines.Add(currentDefine);
                        Console.WriteLine($"[PARSER] Started define: type={currentDefine.Type} id={currentDefine.Id}");
                        inDefine = true; defineBaseDepth = pl.Depth;
                        continue;
                    }

                    // window
                    if (pl.TrimmedLine.StartsWith("draggable window", StringComparison.OrdinalIgnoreCase) || pl.TrimmedLine.StartsWith("window", StringComparison.OrdinalIgnoreCase))
                    {
                        currentWindow = new Window();
                        currentWindow.Draggable = pl.TrimmedLine.StartsWith("draggable window", StringComparison.OrdinalIgnoreCase);
                        // extract quoted id if present
                        int q = pl.TrimmedLine.IndexOf('"');
                        if (q >= 0)
                        {
                            currentWindow.Id = BaseParser.GetQuotedContent(pl.TrimmedLine.Substring(q));
                        }
                        else
                        {
                            // no quoted id -> try unquoted token after keyword
                            var header = currentWindow.Draggable ? "draggable window" : "window";
                            var rem = pl.TrimmedLine.Length > header.Length ? pl.TrimmedLine.Substring(header.Length).Trim() : string.Empty;
                            currentWindow.Id = string.IsNullOrEmpty(rem) ? null : rem;
                        }

                        Result.Windows.Add(currentWindow);
                        Console.WriteLine($"[PARSER] Started window: draggable={currentWindow.Draggable} id={currentWindow.Id}");
                        continue;
                    }
                }

                // Inside a window: depth 1 entries belong to the window
                if (currentWindow != null && pl.Depth >= 1)
                {
                    // Start of a child element
                    if (pl.Depth == 1 && (pl.TrimmedLine.Equals("text", StringComparison.OrdinalIgnoreCase) || pl.TrimmedLine.StartsWith("text ", StringComparison.OrdinalIgnoreCase)))
                    {
                        var el = new TextElement() { Type = GuiElementType.Text };
                        // element ID may be provided on the header line (quoted) OR as an unquoted token after the keyword
                        var headerRem = pl.TrimmedLine.Length > 4 ? pl.TrimmedLine.Substring(4).Trim() : string.Empty;
                        if (!string.IsNullOrEmpty(headerRem))
                        {
                            if (headerRem.StartsWith("\"")) el.Id = BaseParser.GetQuotedContent(headerRem);
                            else el.Id = headerRem;
                        }
                        currentElement = el; currentWindow.Elements.Add(el);
                        Console.WriteLine($"[PARSER] Added Text element id={el.Id}");
                        continue;
                    }

                    if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("button", StringComparison.OrdinalIgnoreCase))
                    {
                        var el = new ButtonElement() { Type = GuiElementType.Button };
                        var headerRem = pl.TrimmedLine.Length > 6 ? pl.TrimmedLine.Substring(6).Trim() : string.Empty;
                        if (!string.IsNullOrEmpty(headerRem))
                        {
                            if (headerRem.StartsWith("\"")) el.Id = BaseParser.GetQuotedContent(headerRem);
                            else el.Id = headerRem;
                        }
                        currentElement = el; currentWindow.Elements.Add(el);
                        Console.WriteLine($"[PARSER] Added Button element id={el.Id}");
                        continue;
                    }

                    if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("icon", StringComparison.OrdinalIgnoreCase))
                    {
                        var el = new IconElement() { Type = GuiElementType.Icon };
                        var headerRem = pl.TrimmedLine.Length > 4 ? pl.TrimmedLine.Substring(4).Trim() : string.Empty;
                        if (!string.IsNullOrEmpty(headerRem))
                        {
                            if (headerRem.StartsWith("\"")) el.Id = BaseParser.GetQuotedContent(headerRem);
                            else el.Id = headerRem;
                        }
                        currentElement = el; currentWindow.Elements.Add(el);
                        Console.WriteLine($"[PARSER] Added Icon element id={el.Id}");
                        continue;
                    }

                    var barMatch = pl.TrimmedLine;
                    if (pl.Depth == 1 && (pl.TrimmedLine.StartsWith("horizontal bar", StringComparison.OrdinalIgnoreCase) || pl.TrimmedLine.StartsWith("vertical bar", StringComparison.OrdinalIgnoreCase)))
                    {
                        // parse steps if present
                        var parts = pl.TrimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var el = new ProgressBarElement() { Type = GuiElementType.ProgressBar };
                        el.Orientation = parts[0].Equals("horizontal", StringComparison.OrdinalIgnoreCase) ? Orientation.Horizontal : Orientation.Vertical;
                        int idxWith = Array.FindIndex(parts, p => p.Equals("with", StringComparison.OrdinalIgnoreCase));
                        if (idxWith >= 0 && idxWith + 1 < parts.Length && int.TryParse(parts[idxWith + 1], out var steps))
                        {
                            el.Steps = steps;
                        }
                        currentElement = el; currentWindow.Elements.Add(el);
                        Console.WriteLine($"[PARSER] Added {el.Orientation} bar steps={el.Steps}");
                        continue;
                    }

                    // window-level properties
                    if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("size ", StringComparison.OrdinalIgnoreCase))
                    {
                        if (pl.TrimmedLine.EndsWith("%"))
                        {
                            currentWindow.SizeIsPercent = true;
                            // optionally capture percent value (not parsed as int here)
                        }
                        else
                        {
                            var toks = pl.TrimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (toks.Length >= 3)
                            {
                                int.TryParse(toks[1].TrimStart('x'), out var sx);
                                int.TryParse(toks[2].TrimStart('y'), out var sy);
                                currentWindow.Size = (sx, sy);
                            }
                        }
                        continue;
                    }

                    if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("position ", StringComparison.OrdinalIgnoreCase))
                    {
                        var toks = pl.TrimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (toks.Length >= 3)
                        {
                            int.TryParse(toks[1].TrimStart('x'), out var px);
                            int.TryParse(toks[2].TrimStart('y'), out var py);
                            currentWindow.Position = (px, py);
                        }
                        continue;
                    }

                    if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
                    {
                        currentWindow.Sprite = BaseParser.GetQuotedContent(pl.TrimmedLine.Substring("sprite".Length).Trim());
                        continue;
                    }

                    if (pl.Depth == 1 && pl.TrimmedLine.Equals("visible", StringComparison.OrdinalIgnoreCase))
                    {
                        inVisible = true; visibleBaseDepth = pl.Depth;
                        currentWindow.VisibleRaw = string.Empty;
                        continue;
                    }

                    // element sub-properties (depth > 1)
                    if (currentElement != null && pl.Depth >= 2)
                    {
                        var t = pl.TrimmedLine;
                        currentElement.Raw += (string.IsNullOrEmpty(currentElement.Raw) ? "" : "\n") + t;
                        if (t.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
                        {
                            var rem = t.Substring("sprite".Length).Trim();
                            if (rem.StartsWith("\""))
                            {
                                currentElement.Sprite = BaseParser.GetQuotedContent(rem);
                                currentElement.SpriteIsScriptedId = false;
                            }
                            else
                            {
                                currentElement.Sprite = rem;
                                currentElement.SpriteIsScriptedId = true;
                            }
                            continue;
                        }
                        if (t.StartsWith("text ", StringComparison.OrdinalIgnoreCase))
                        {
                            var rem = t.Substring("text".Length).Trim();
                            if (rem.StartsWith("\""))
                            {
                                currentElement.Text = BaseParser.GetQuotedContent(rem);
                                if (currentElement is TextElement te) te.IsTextScriptedLocalisationId = false;
                            }
                            else
                            {
                                currentElement.Text = rem;
                                if (currentElement is TextElement te) te.IsTextScriptedLocalisationId = true;
                            }
                            continue;
                        }
                        if (t.StartsWith("font", StringComparison.OrdinalIgnoreCase))
                        {
                            currentElement.Font = BaseParser.GetQuotedContent(t.Substring("font".Length).Trim());
                            continue;
                        }
                        if (t.StartsWith("position", StringComparison.OrdinalIgnoreCase))
                        {
                            var toks = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (toks.Length >= 3)
                            {
                                int.TryParse(toks[1].TrimStart('x'), out var px);
                                int.TryParse(toks[2].TrimStart('y'), out var py);
                                currentElement.Position = (px, py);
                            }
                            continue;
                        }
                        if (t.StartsWith("max size", StringComparison.OrdinalIgnoreCase))
                        {
                            var toks = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (toks.Length >= 4)
                            {
                                int.TryParse(toks[2].TrimStart('x'), out var sx);
                                int.TryParse(toks[3].TrimStart('y'), out var sy);
                                currentElement.Size = (sx, sy);
                            }
                            continue;
                        }
                        if (t.StartsWith("size ", StringComparison.OrdinalIgnoreCase))
                        {
                            var toks = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            if (toks.Length == 2 && toks[1].EndsWith("%"))
                            {
                                currentElement.SizeIsPercent = true;
                                if (int.TryParse(toks[1].TrimEnd('%'), out var p)) currentElement.SizePercent = p;
                            }
                            else if (toks.Length >= 3)
                            {
                                int.TryParse(toks[1].TrimStart('x'), out var sx);
                                int.TryParse(toks[2].TrimStart('y'), out var sy);
                                currentElement.Size = (sx, sy);
                            }
                            continue;
                        }

                        if (t.StartsWith("on click", StringComparison.OrdinalIgnoreCase))
                        {
                            inOnClick = true; onClickBaseDepth = pl.Depth;
                            currentElement.OnClickRaw = string.Empty;
                            continue;
                        }

                        // progress bar specific properties
                        if (currentElement is ProgressBarElement pbe)
                        {
                            if (t.StartsWith("var ", StringComparison.OrdinalIgnoreCase))
                            {
                                pbe.VarName = BaseParser.GetQuotedContent(t.Substring("var".Length).Trim());
                                continue;
                            }
                            if (t.StartsWith("progressed color", StringComparison.OrdinalIgnoreCase) || t.StartsWith("unprogressed color", StringComparison.OrdinalIgnoreCase))
                            {
                                var toks = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (toks.Length >= 5)
                                {
                                    double.TryParse(toks[toks.Length - 3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var a);
                                    double.TryParse(toks[toks.Length - 2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b);
                                    double.TryParse(toks[toks.Length - 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var c);
                                    if (toks[0].StartsWith("progressed", StringComparison.OrdinalIgnoreCase)) pbe.ProgressedColor = (a, b, c);
                                    else pbe.UnprogressedColor = (a, b, c);
                                }
                                continue;
                            }
                            if (t.StartsWith("progressed sprite", StringComparison.OrdinalIgnoreCase))
                            {
                                var rem = t.Substring("progressed sprite".Length).Trim();
                                pbe.ProgressedSprite = BaseParser.GetQuotedContent(rem);
                                continue;
                            }
                            if (t.StartsWith("unprogressed sprite", StringComparison.OrdinalIgnoreCase))
                            {
                                var rem = t.Substring("unprogressed sprite".Length).Trim();
                                pbe.UnprogressedSprite = BaseParser.GetQuotedContent(rem);
                                continue;
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"[PARSER] Completed parse: {Result.Windows.Count} windows, {Result.Defines.Count} defines");
        }
    }
}
