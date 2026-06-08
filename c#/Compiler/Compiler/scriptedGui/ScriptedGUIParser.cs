using System;
using System.Collections.Generic;
using System.Linq;

namespace Compiler
{
    // Lightweight models to hold parsed GUI data. Parser is best-effort and
    // deliberately permissive: validation is the validator's responsibility.
    public class DefineBranch
    {
        public List<RawLine> ConditionRaw = new List<RawLine>(); // raw lines that make up the if / else if condition
        public bool IsElse = false;
        // Single string extracted from the then block (the value inside the text "..." or sprite "..." line)
        public string ThenBlock = null;
    }

    public class Define
    {
        public DefineType Type; // sprite or text
        public string Id;
        public List<DefineBranch> Branches = new List<DefineBranch>();
    }

    public abstract class GuiElement
    {
        // Only keep fields that are common to all element types. Specialized
        // properties have been moved to concrete subclasses to remove dead
        // weight from the base type.
        public string Id; // optional (only set when quoted id present after header)
        public (int x, int y)? Position;
    }

    public class TextElement : GuiElement
    {
        public string Text;
        public string Font;
        public (int x, int y)? MaxSize;
        // When true the element's text is a scripted localisation id
        public bool IsTextScriptedLocalisationId = false;
        // optional id that links this element to a Define entry (preserve original define id)
        public string DefinesId;
    }

    public class ButtonElement : GuiElement
    {
        public string Sprite;
        // When true this element's main field (text or sprite) came from an unquoted property
        public bool IsProperty = false;
        public int? SizePercent = null;
        public List<RawLine> OnClickRaw = new List<RawLine>();
        // Optional text content for buttons (may be scripted localisation id or literal)
        public string Text;
        public bool IsTextScriptedLocalisationId = false;
        // optional id that links this button's text to a Define entry
        public string DefinesId;
    }

    public class IconElement : GuiElement
    {
        public string Sprite;
        public string Text;
        public string Font;
        public bool IsProperty = false;
        public int? SizePercent = null;
        public bool IsTextScriptedLocalisationId = false;
    }

    // ProgressBar is a specialized element with its own properties. Promoted
    // from nested type to top-level to make the model clearer and easier to
    // reason about while preserving the same data layout used by the
    // rest of the codebase.
    public class ProgressBarElement : GuiElement
    {
        public Orientation Orientation; // horizontal | vertical
        public int Steps;
        public (double r, double g, double b)? ProgressedColor;
        public (double r, double g, double b)? UnprogressedColor;
        public string VarName;
        public string ProgressedSprite;
        public string UnprogressedSprite;
        // progress bars use size as pixel size when emitted
        public (int x, int y)? Size;
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
        public List<RawLine> VisibleRaw = new List<RawLine>();
        public List<GuiElement> Elements = new List<GuiElement>();
        // Collected properties for unquoted scripted sprite/text used inside elements
        public List<WindowProperty> Properties = new List<WindowProperty>();
    }

    public class WindowProperty
    {
        // element type this property belongs to (Icon or ProgressBar)
        public GuiElementType Type;
        // generated or derived id for this property (e.g. "text_get_random_image_loc")
        public string Id;
        // parent element id (the element this property belongs to)
        public string ParentId;
        // the raw value following the keyword (unquoted value)
        public string Value;
    }

    public class ScriptedGUI
    {
        public List<Define> Defines = new List<Define>();
        public List<Window> Windows = new List<Window>();
        // source file name for this parsed snapshot
        public string SourceFileName { get; set; } = string.Empty;
    }

    public class ScriptedGUIParser : BaseParser
    {
        public ScriptedGUIParser()
        {
            Compiler.Logging.Logger.LogComponent("ScriptedGUI", "ScriptedGUIParser initialized.");
        }
        public ScriptedGUI Result { get; private set; } = new ScriptedGUI();
        // Expose the last parsed file snapshot similar to other parsers
        public ScriptedGUI LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {

            // reset result and record source file name so compilers can access it
            Result = new ScriptedGUI();
            Result.SourceFileName = fileName;

            Window currentWindow = null;
            GuiElement currentElement = null;
            Define currentDefine = null;

            bool inOnClick = false; int onClickBaseDepth = 0;
            bool inVisible = false; int visibleBaseDepth = 0;

            // For define parsing
            DefineBranch currentBranch = null;
            bool inDefine = false; int defineBaseDepth = 0;

            // Iterate using index to be able to push back (i--)
            for (int i = 0; i < preprocessedLines.Count; i++)
            {
                var pl = preprocessedLines[i];
                Compiler.Logging.Logger.LogComponent("ScriptedGUI", $"- [PARSER] Line {pl.LineNumber} Depth {pl.Depth}: {pl.TrimmedLine}");

                // If currently collecting an on click block for an element (only buttons keep on-click data)
                if (inOnClick)
                {
                    if (pl.Depth > onClickBaseDepth)
                    {
                        if (currentElement is ButtonElement btn)
                        {
                            btn.OnClickRaw.Add(new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth });
                        }
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
                        currentWindow.VisibleRaw.Add(new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth });
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
                        HandleDefineLine(pl, currentDefine, ref currentBranch);
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
                        currentDefine = ParseDefineHeader(pl.TrimmedLine);
                        Result.Defines.Add(currentDefine);
                        Compiler.Logging.Logger.LogComponent("ScriptedGUI", $"- [PARSER] Started define: type={currentDefine.Type} id={currentDefine.Id}");
                        inDefine = true; defineBaseDepth = pl.Depth;
                        continue;
                    }

                    // window
                    if (pl.TrimmedLine.StartsWith("draggable window", StringComparison.OrdinalIgnoreCase) || pl.TrimmedLine.StartsWith("window", StringComparison.OrdinalIgnoreCase))
                    {
                        currentWindow = ParseWindowHeader(pl.TrimmedLine);
                        Result.Windows.Add(currentWindow);
                        Compiler.Logging.Logger.LogComponent("ScriptedGUI", $"- [PARSER] Started window: draggable={currentWindow.Draggable} id={currentWindow.Id}");
                        continue;
                    }
                }

                // Inside a window: depth 1 entries belong to the window
                if (currentWindow != null && pl.Depth >= 1)
                {
                    // Start of a child element at depth 1
                    if (pl.Depth == 1)
                    {
                        if (IsElementHeader(pl.TrimmedLine, out var newElement))
                        {
                            currentElement = newElement;
                            currentWindow.Elements.Add(currentElement);
                            EnsureElementHasId(currentElement, currentWindow);
                            var typeName = currentElement is TextElement ? "Text" : currentElement is ButtonElement ? "Button" : currentElement is IconElement ? "Icon" : currentElement is ProgressBarElement ? "ProgressBar" : "element";
                            Compiler.Logging.Logger.LogComponent("ScriptedGUI", $"- [PARSER] Added {typeName} element id={currentElement.Id}");
                            continue;
                        }

                        // window-level properties
                        if (pl.TrimmedLine.StartsWith("size ", StringComparison.OrdinalIgnoreCase))
                        {
                            if (pl.TrimmedLine.EndsWith("%"))
                            {
                                currentWindow.SizeIsPercent = true;
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

                        if (pl.TrimmedLine.StartsWith("position ", StringComparison.OrdinalIgnoreCase))
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

                        if (pl.TrimmedLine.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
                        {
                            currentWindow.Sprite = BaseParser.GetQuotedContent(pl.TrimmedLine.Substring("sprite".Length).Trim());
                            continue;
                        }

                        if (pl.TrimmedLine.Equals("visible", StringComparison.OrdinalIgnoreCase))
                        {
                            inVisible = true; visibleBaseDepth = pl.Depth;
                            currentWindow.VisibleRaw = new List<RawLine>();
                            continue;
                        }
                    }

                    // element sub-properties (depth > 1)
                    if (currentElement != null && pl.Depth >= 2)
                    {
                        HandleElementProperty(pl, currentElement, currentWindow, ref inOnClick, ref onClickBaseDepth);
                        continue;
                    }
                }
            }

            Compiler.Logging.Logger.LogComponent("ScriptedGUI", $"- [PARSER] Completed parse: {Result.Windows.Count} windows, {Result.Defines.Count} defines");

            // Save parsed snapshot for external consumers
            LastParsedFile = Result;
        }

        // Helper: parse define header like "define text NAME" or "define sprite NAME"
        private Define ParseDefineHeader(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var d = new Define();
            if (parts.Length >= 2)
            {
                if (parts.Length >= 3)
                {
                    var t = parts[1].ToLowerInvariant();
                    d.Type = t == "sprite" ? DefineType.Sprite : DefineType.Text;
                    d.Id = BaseParser.GetQuotedContent(line.Substring(line.IndexOf(parts[2]))).Trim();
                }
                else
                {
                    var t = parts[1].ToLowerInvariant();
                    d.Type = t == "sprite" ? DefineType.Sprite : DefineType.Text;
                }
            }
            return d;
        }

        private void HandleDefineLine(BaseValidator.PreprocessedLine pl, Define currentDefine, ref DefineBranch currentBranch)
        {
            string t = pl.TrimmedLine;
            if (t.Equals("if", StringComparison.OrdinalIgnoreCase) || t.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
            {
                currentBranch = new DefineBranch();
                currentDefine.Branches.Add(currentBranch);
                currentBranch.ConditionRaw = new List<RawLine>();
                currentBranch.IsElse = false;
                return;
            }
            if (t.StartsWith("else if", StringComparison.OrdinalIgnoreCase))
            {
                currentBranch = new DefineBranch();
                currentDefine.Branches.Add(currentBranch);
                currentBranch.ConditionRaw = new List<RawLine>();
                currentBranch.IsElse = false;
                return;
            }
            if (t.Equals("else", StringComparison.OrdinalIgnoreCase))
            {
                currentBranch = new DefineBranch();
                currentBranch.IsElse = true;
                currentDefine.Branches.Add(currentBranch);
                currentBranch.ConditionRaw = new List<RawLine>();
                return;
            }
            if (t.Equals("then", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (currentBranch != null)
            {
                if (t.StartsWith("sprite", StringComparison.OrdinalIgnoreCase) || t.StartsWith("text", StringComparison.OrdinalIgnoreCase))
                {
                    string rem;
                    string val = null;
                    if (t.StartsWith("sprite", StringComparison.OrdinalIgnoreCase)) rem = t.Substring("sprite".Length).Trim();
                    else rem = t.Substring("text".Length).Trim();
                    if (rem.StartsWith("\"")) val = BaseParser.GetQuotedContent(rem);
                    else val = rem;
                    currentBranch.ThenBlock = val;
                }
                else
                {
                    currentBranch.ConditionRaw.Add(new RawLine { trimmedLine = t, depth = pl.Depth });
                }
                return;
            }

            var fb = new DefineBranch();
            fb.ConditionRaw = new List<RawLine> { new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth } };
            currentDefine.Branches.Add(fb);
        }

        private Window ParseWindowHeader(string line)
        {
            var w = new Window();
            w.Draggable = line.StartsWith("draggable window", StringComparison.OrdinalIgnoreCase);
            int q = line.IndexOf('"');
            if (q >= 0)
            {
                w.Id = BaseParser.GetQuotedContent(line.Substring(q));
            }
            else
            {
                var header = w.Draggable ? "draggable window" : "window";
                var rem = line.Length > header.Length ? line.Substring(header.Length).Trim() : string.Empty;
                w.Id = string.IsNullOrEmpty(rem) ? null : rem;
            }
            return w;
        }

        private bool IsElementHeader(string trimmedLine, out GuiElement element)
        {
            element = null;
            if (trimmedLine.Equals("text", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("text ", StringComparison.OrdinalIgnoreCase))
            {
                var el = new TextElement();
                var headerRem = trimmedLine.Length > 4 ? trimmedLine.Substring(4).Trim() : string.Empty;
                if (!string.IsNullOrEmpty(headerRem))
                {
                    if (headerRem.StartsWith("\"")) el.Id = BaseParser.GetQuotedContent(headerRem);
                    else el.Id = headerRem;
                }
                element = el;
                return true;
            }
            if (trimmedLine.StartsWith("button", StringComparison.OrdinalIgnoreCase))
            {
                var el = new ButtonElement();
                var headerRem = trimmedLine.Length > 6 ? trimmedLine.Substring(6).Trim() : string.Empty;
                if (!string.IsNullOrEmpty(headerRem))
                {
                    if (headerRem.StartsWith("\"")) el.Id = BaseParser.GetQuotedContent(headerRem);
                    else el.Id = headerRem;
                }
                element = el;
                return true;
            }
            if (trimmedLine.StartsWith("icon", StringComparison.OrdinalIgnoreCase))
            {
                var el = new IconElement();
                var headerRem = trimmedLine.Length > 4 ? trimmedLine.Substring(4).Trim() : string.Empty;
                if (!string.IsNullOrEmpty(headerRem))
                {
                    if (headerRem.StartsWith("\"")) el.Id = BaseParser.GetQuotedContent(headerRem);
                    else el.Id = headerRem;
                }
                element = el;
                return true;
            }
            if (trimmedLine.StartsWith("horizontal bar", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("vertical bar", StringComparison.OrdinalIgnoreCase))
            {
                var parts = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var el = new ProgressBarElement();
                el.Orientation = parts[0].Equals("horizontal", StringComparison.OrdinalIgnoreCase) ? Orientation.Horizontal : Orientation.Vertical;
                int idxWith = Array.FindIndex(parts, p => p.Equals("with", StringComparison.OrdinalIgnoreCase));
                if (idxWith >= 0 && idxWith + 1 < parts.Length && int.TryParse(parts[idxWith + 1], out var steps)) el.Steps = steps;
                element = el;
                return true;
            }
            return false;
        }

        private void EnsureElementHasId(GuiElement el, Window window)
        {
            if (!string.IsNullOrEmpty(el.Id)) return;
            string elemTypeName;
            if (el is ProgressBarElement) elemTypeName = "bar";
            else if (el is TextElement) elemTypeName = "text";
            else if (el is ButtonElement) elemTypeName = "button";
            else if (el is IconElement) elemTypeName = "icon";
            else elemTypeName = "element";

            var count = window.Elements.Count(e =>
                (e is ProgressBarElement && el is ProgressBarElement)
                || (e is TextElement && el is TextElement)
                || (e is ButtonElement && el is ButtonElement)
                || (e is IconElement && el is IconElement)
            );
            var winId = string.IsNullOrEmpty(window.Id) ? "window" : window.Id.Replace(' ', '_');
            el.Id = $"{winId}_{elemTypeName}_{count}";
            Compiler.Logging.Logger.LogComponent("ScriptedGUI", $"- [PARSER] Generated element id={el.Id}");
        }

        private void HandleElementProperty(BaseValidator.PreprocessedLine pl, GuiElement currentElement, Window currentWindow, ref bool inOnClick, ref int onClickBaseDepth)
        {
            var t = pl.TrimmedLine;
            if (t.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
            {
                var rem = t.Substring("sprite".Length).Trim();
                if (rem.StartsWith("\""))
                {
                    if (currentElement is IconElement ie) ie.Sprite = BaseParser.GetQuotedContent(rem);
                    else if (currentElement is ButtonElement be) be.Sprite = BaseParser.GetQuotedContent(rem);
                    else if (currentElement is ProgressBarElement pbar) pbar.ProgressedSprite = BaseParser.GetQuotedContent(rem); // fallback
                }
                else
                {
                    // unquoted => property id used
                    if (currentElement is IconElement ie)
                    {
                        ie.Sprite = rem;
                        ie.IsProperty = true;
                        if (string.IsNullOrEmpty(ie.Id) && currentWindow != null) EnsureElementHasId(ie, currentWindow);
                        var prop = new WindowProperty();
                        prop.Type = GuiElementType.Icon;
                        var baseName = rem.Replace(' ', '_');
                        prop.Id = baseName;
                        prop.ParentId = ie.Id;
                        prop.Value = rem;
                        currentWindow.Properties.Add(prop);
                        Compiler.Logging.Logger.LogComponent("ScriptedGUI", $"- [PARSER] Added window property type={prop.Type} id={prop.Id} parent={prop.ParentId} value={prop.Value}");
                    }
                    else if (currentElement is ButtonElement be)
                    {
                        be.Sprite = rem;
                        be.IsProperty = true;
                        if (string.IsNullOrEmpty(be.Id) && currentWindow != null) EnsureElementHasId(be, currentWindow);
                    }
                    else if (currentElement is ProgressBarElement pbar)
                    {
                        pbar.ProgressedSprite = rem;
                        pbar.UnprogressedSprite = rem;
                    }
                }
                return;
            }
            if (t.StartsWith("text ", StringComparison.OrdinalIgnoreCase))
            {
                var rem = t.Substring("text".Length).Trim();
                if (currentElement is TextElement te)
                {
                    if (rem.StartsWith("\""))
                    {
                        te.Text = BaseParser.GetQuotedContent(rem);
                        te.IsTextScriptedLocalisationId = false;
                    }
                    else
                    {
                        te.Text = rem;
                        te.IsTextScriptedLocalisationId = true;
                        te.DefinesId = rem;
                        if (string.IsNullOrEmpty(te.Id) && currentWindow != null) EnsureElementHasId(te, currentWindow);
                    }
                }
                else if (currentElement is IconElement ie)
                {
                    if (rem.StartsWith("\"")) ie.Text = BaseParser.GetQuotedContent(rem);
                    else
                    {
                        ie.Text = rem;
                        ie.IsTextScriptedLocalisationId = true;
                    }
                    if (string.IsNullOrEmpty(ie.Id) && currentWindow != null) EnsureElementHasId(ie, currentWindow);
                }
                return;
            }
            if (t.StartsWith("font", StringComparison.OrdinalIgnoreCase))
            {
                var f = BaseParser.GetQuotedContent(t.Substring("font".Length).Trim());
                if (currentElement is TextElement te) te.Font = f;
                else if (currentElement is IconElement ie) ie.Font = f;
                return;
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
                return;
            }
            if (t.StartsWith("max size", StringComparison.OrdinalIgnoreCase))
            {
                var toks = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (toks.Length >= 4)
                {
                    int.TryParse(toks[2].TrimStart('x'), out var sx);
                    int.TryParse(toks[3].TrimStart('y'), out var sy);
                    if (currentElement is TextElement te) te.MaxSize = (sx, sy);
                }
                return;
            }
            if (t.StartsWith("size ", StringComparison.OrdinalIgnoreCase))
            {
                var toks = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (toks.Length == 2 && toks[1].EndsWith("%"))
                {
                    if (currentElement is IconElement ie) { if (int.TryParse(toks[1].TrimEnd('%'), out var p)) ie.SizePercent = p; }
                    else if (currentElement is ButtonElement be) { if (int.TryParse(toks[1].TrimEnd('%'), out var p)) be.SizePercent = p; }
                }
                else if (toks.Length >= 3)
                {
                    int.TryParse(toks[1].TrimStart('x'), out var sx);
                    int.TryParse(toks[2].TrimStart('y'), out var sy);
                    if (currentElement is ProgressBarElement pbar) pbar.Size = (sx, sy);
                }
                return;
            }

            if (t.StartsWith("on click", StringComparison.OrdinalIgnoreCase))
            {
                inOnClick = true; onClickBaseDepth = pl.Depth;
                if (currentElement is ButtonElement be) be.OnClickRaw = new List<RawLine>();
                return;
            }

            // progress bar specific properties
            if (currentElement is ProgressBarElement pbe)
            {
                if (t.StartsWith("var ", StringComparison.OrdinalIgnoreCase))
                {
                    pbe.VarName = BaseParser.GetQuotedContent(t.Substring("var".Length).Trim());
                    if (currentWindow != null)
                    {
                        if (string.IsNullOrEmpty(pbe.Id)) EnsureElementHasId(pbe, currentWindow);
                        var prop = new WindowProperty();
                        prop.Type = GuiElementType.ProgressBar;
                        prop.Id = pbe.VarName;
                        prop.ParentId = pbe.Id;
                        prop.Value = pbe.VarName;
                        currentWindow.Properties.Add(prop);
                        Compiler.Logging.Logger.LogComponent("ScriptedGUI", $"- [PARSER] Added bar property id={prop.Id} parent={prop.ParentId}");
                    }
                    return;
                }
                if (t.StartsWith("progressed color", StringComparison.OrdinalIgnoreCase) || t.StartsWith("unprogressed color", StringComparison.OrdinalIgnoreCase))
                {
                    var toks = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (toks.Length >= 5)
                    {
                        var sa = toks[toks.Length - 3].Replace(',', '.');
                        var sb = toks[toks.Length - 2].Replace(',', '.');
                        var sc = toks[toks.Length - 1].Replace(',', '.');
                        double.TryParse(sa, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var a);
                        double.TryParse(sb, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b);
                        double.TryParse(sc, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var c);
                        if (toks[0].StartsWith("progressed", StringComparison.OrdinalIgnoreCase)) pbe.ProgressedColor = (a, b, c);
                        else pbe.UnprogressedColor = (a, b, c);
                    }
                    return;
                }
                if (t.StartsWith("progressed sprite", StringComparison.OrdinalIgnoreCase))
                {
                    var rem = t.Substring("progressed sprite".Length).Trim();
                    pbe.ProgressedSprite = BaseParser.GetQuotedContent(rem);
                    return;
                }
                if (t.StartsWith("unprogressed sprite", StringComparison.OrdinalIgnoreCase))
                {
                    var rem = t.Substring("unprogressed sprite".Length).Trim();
                    pbe.UnprogressedSprite = BaseParser.GetQuotedContent(rem);
                    return;
                }
            }
        }
    }
}
