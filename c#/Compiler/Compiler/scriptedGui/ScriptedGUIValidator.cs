using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Compiler
{
    public class ScriptedGUIValidator : BaseValidator
    {
        // Allow unquoted sprite ids for GUI scripts
        protected override bool AllowSpriteId => true;

        // Provide a permissive set of block keywords so BaseValidator will run
        protected override Dictionary<string, int[]> AllowedBlockDepths => new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = new[] { 1, 2 },
            ["desc"] = new[] { 1, 2 },
            ["sprite"] = new[] { 0, 1, 2 },
        };

        public IList<string> Validate(string script)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(script))
            {
                results.Add("Script is empty");
                return results;
            }

            var lines = script.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Basic best-effort checks
            CheckDefineBlocks(lines, results);
            CheckWindows(lines, results);
            CheckProgressBars(lines, results);
            CheckButtons(lines, results);

            return results;
        }

        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            // Recognize var "id" immediately so it's always treated as a valid statement.
            // Be permissive when extracting the quoted id and validate using IsValidId.
            var immediateVar = Regex.Match(trimmedLine, "var\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
            if (immediateVar.Success)
            {
                string id = immediateVar.Groups[1].Value;
                if (!IsValidId(id))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Invalid var id: '{id}'. var quoted value must be a valid id."));
                }
                return true;
            }

            // Special-case 'text': at depth 1 it may be a block header with no trailing
            // identifier (e.g., 'text' followed by indented lines). When used inline it
            // must have either a quoted string or a valid id following it.
            if (trimmedLine.StartsWith("text", StringComparison.OrdinalIgnoreCase))
            {
                int prefixLen = "text".Length;
                string remainder = trimmedLine.Length > prefixLen ? trimmedLine.Substring(prefixLen).Trim() : string.Empty;
                if (string.IsNullOrEmpty(remainder))
                {
                    // Accept header-only 'text' blocks at depth 1 (common for window titles)
                    if (currentDepth == 1)
                    {
                        // treat as a recognized block opener with deeper content
                        ExpectedDepth = currentDepth + 1;
                        return true;
                    }

            // top-level var "id" - must be a valid id (recognized as its own statement)
            var topVarMatch = Regex.Match(trimmedLine, "^var\\s+\"([^\"]+)\"$", RegexOptions.IgnoreCase);
            if (topVarMatch.Success)
            {
                string id = topVarMatch.Groups[1].Value;
                if (!IsValidId(id))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Invalid var id: '{id}'. var quoted value must be a valid id."));
                }
                return true;
            }

                    Errors.Add(new ValidationError(fileName, lineNumber, "Malformed text: missing value after 'text'."));
                    return true;
                }

                // Otherwise validate inline text: quoted string or valid id
                if (IsQuotedString(remainder))
                {
                    string outside = RemoveQuotedContent(remainder);
                    if (!string.IsNullOrWhiteSpace(outside))
                    {
                        Errors.Add(new ValidationError(fileName, lineNumber, "Malformed text: unexpected content outside quoted string."));
                    }
                }
                else if (!IsValidId(remainder))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Malformed text: '{remainder}' is not a valid id or quoted string."));
                }

                return true;
            }

            // horizontal|vertical bar with N steps increases inner depth
            var barMatch = Regex.Match(trimmedLine, "^(horizontal|vertical)\\s+bar\\s+with\\s+(\\d+)\\s+steps$", RegexOptions.IgnoreCase);
            if (barMatch.Success)
            {
                if (!int.TryParse(barMatch.Groups[2].Value, out var steps) || steps <= 0)
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Malformed bar steps: '{trimmedLine}' - steps must be a positive integer."));
                }
                // The bar opens a nested block
                ExpectedDepth = currentDepth + 1;
                return true;
            }

            // size xNN yNN, max size xNN yNN, or percentage form like 'size 120%'
            if (trimmedLine.StartsWith("size ", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("max size ", StringComparison.OrdinalIgnoreCase))
            {
                var tokens = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                // tokens: [size, xNN, yNN]  OR [max, size, xNN, yNN] OR percentage: [size, 120%]
                string xToken = null, yToken = null;
                bool isPercentage = false;

                if (tokens.Length == 3 && string.Equals(tokens[0], "size", StringComparison.OrdinalIgnoreCase))
                {
                    xToken = tokens[1];
                    yToken = tokens[2];
                }
                else if (tokens.Length == 4 && string.Equals(tokens[0], "max", StringComparison.OrdinalIgnoreCase) && string.Equals(tokens[1], "size", StringComparison.OrdinalIgnoreCase))
                {
                    xToken = tokens[2];
                    yToken = tokens[3];
                }
                else if (tokens.Length == 2 && string.Equals(tokens[0], "size", StringComparison.OrdinalIgnoreCase) && tokens[1].EndsWith("%"))
                {
                    isPercentage = true;
                    string num = tokens[1].TrimEnd('%');
                    if (!IsInt(num))
                    {
                        Errors.Add(new ValidationError(fileName, lineNumber, $"Malformed size percentage: '{trimmedLine}'. Expected integer percent value like 'size 120%'."));
                        return true;
                    }
                }
                else
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Malformed size declaration: '{trimmedLine}'. Expected 'size xNN yNN' or 'max size xNN yNN' or 'size 120%'."));
                    return true;
                }

                if (!isPercentage)
                {
                    if (string.IsNullOrEmpty(xToken) || string.IsNullOrEmpty(yToken) || xToken.Length < 2 || yToken.Length < 2)
                    {
                        Errors.Add(new ValidationError(fileName, lineNumber, $"Malformed size declaration: '{trimmedLine}'."));
                        return true;
                    }

                    if (!IsValidCoordinate(xToken, 'x') || !IsValidCoordinate(yToken, 'y'))
                    {
                        Errors.Add(new ValidationError(fileName, lineNumber, $"Malformed size coordinates: '{trimmedLine}'. Use xNN and yNN numbers."));
                    }
                }

                return true;
            }

            // font "quoted_lowercase_snake"
            if (trimmedLine.StartsWith("font", StringComparison.OrdinalIgnoreCase))
            {
                int prefixLen = "font".Length;
                string remainder = trimmedLine.Length > prefixLen ? trimmedLine.Substring(prefixLen).Trim() : string.Empty;
                if (string.IsNullOrEmpty(remainder) || !IsQuotedString(remainder))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, "Malformed font: expected a quoted font id (lowercase snake_case) after 'font'."));
                    return true;
                }

                string inner = remainder.Substring(1, remainder.Length - 2);
                if (!Regex.IsMatch(inner, "^[a-z0-9_]+$"))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Invalid font id: '{inner}'. Font ids must be lowercase snake_case."));
                }

                return true;
            }

            // visible opens an inner block
            if (trimmedLine.Equals("visible", StringComparison.OrdinalIgnoreCase))
            {
                ExpectedDepth = currentDepth + 1;
                return true;
            }

            // position xNN yNN
            if (trimmedLine.StartsWith("position", StringComparison.OrdinalIgnoreCase))
            {
                var tokens = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 3)
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Malformed position declaration: '{trimmedLine}'. Expected 'position xNN yNN'."));
                    return true;
                }

                if (!IsValidCoordinate(tokens[1], 'x') || !IsValidCoordinate(tokens[2], 'y'))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Malformed position coordinates: '{trimmedLine}'. Use xNN and yNN numbers."));
                }

                return true;
            }

            // static tooltip "..." or dynamic tooltip "..."
            if (trimmedLine.StartsWith("static tooltip", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("dynamic tooltip", StringComparison.OrdinalIgnoreCase))
            {
                int firstQuote = trimmedLine.IndexOf('"');
                if (firstQuote == -1 || !IsQuotedString(trimmedLine.Substring(firstQuote)))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, "Malformed tooltip: expected quoted text after tooltip keyword."));
                }
                return true;
            }

            // progressed/unprogressed color r g b where each component is 0.0-1.0 and >= 0.1
            if (trimmedLine.StartsWith("progressed color", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("unprogressed color", StringComparison.OrdinalIgnoreCase))
            {
                var tokens = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                // tokens: [progressed|unprogressed, color, r, g, b]
                if (tokens.Length != 5)
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Malformed color declaration: '{trimmedLine}'. Expected 'progressed/unprogressed color r g b'."));
                    return true;
                }

                bool ok = true;
                for (int i = 2; i < 5; i++)
                {
                    if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                    {
                        ok = false;
                        break;
                    }
                    if (val < 0.0 || val > 1.0)
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok)
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Invalid color values in '{trimmedLine}': components must be between 0.0 and 1.0 inclusive."));
                }

                return true;
            }

            // on click opens a shallow inner block (only +1 expected depth)
            if (trimmedLine.StartsWith("on click", StringComparison.OrdinalIgnoreCase))
            {
                ExpectedDepth = currentDepth + 1;
                return true;
            }

            // progressed/unprogressed sprite must be a quoted path to a .dds file
            if (trimmedLine.StartsWith("progressed sprite", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("unprogressed sprite", StringComparison.OrdinalIgnoreCase))
            {
                int idx = trimmedLine.IndexOf('"');
                if (idx == -1)
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, "Malformed sprite path: expected quoted .dds path."));
                    return true;
                }

                string quoted = trimmedLine.Substring(idx);
                if (!IsQuotedString(quoted))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, "Malformed sprite path: unterminated quoted string."));
                    return true;
                }

                string inner = quoted.Substring(1, quoted.Length - 2);
                if (!inner.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Invalid sprite path: '{inner}'. Progress bar sprites must be .dds files."));
                }

                // ensure nothing outside quoted string
                string outside = RemoveQuotedContent(quoted);
                if (!string.IsNullOrWhiteSpace(outside))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, "Malformed sprite path: unexpected content outside quoted string."));
                }

                return true;
            }

            // For these GUI constructs the effective inner content is one level deeper
            // than the literal line. Recognize them here and adjust ExpectedDepth so
            // the base validator does not complain about AllowedBlockDepths. If the
            // prefix appears at depth 1 we accept header-only usage (no id required).
            var increaseDepthPrefixes = new[] { "draggable window", "window", "button", "icon", "horizontal bar", "vertical bar", "define" };
            string matched = increaseDepthPrefixes.FirstOrDefault(p => trimmedLine.StartsWith(p, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(matched))
            {
                string remainder = trimmedLine.Length > matched.Length ? trimmedLine.Substring(matched.Length).Trim() : string.Empty;
                var tokens = string.IsNullOrWhiteSpace(remainder)
                    ? Array.Empty<string>()
                    : remainder.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // If this is a header-only usage at depth 1 we accept it (no id needed).
                if (tokens.Length == 0 && currentDepth == 1)
                {
                    ExpectedDepth = currentDepth + 1;
                    return true;
                }

                string idToCheck = null;
                if (tokens.Length == 0)
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Malformed {matched}: missing identifier after '{matched}'."));
                }
                else
                {
                    if (string.Equals(matched, "define", StringComparison.OrdinalIgnoreCase))
                    {
                        // define sprite <id>  OR  define text <id>
                        if (tokens.Length >= 2 && (string.Equals(tokens[0], "sprite", StringComparison.OrdinalIgnoreCase) || string.Equals(tokens[0], "text", StringComparison.OrdinalIgnoreCase)))
                        {
                            idToCheck = tokens[1];
                        }
                        else
                        {
                            idToCheck = tokens[0];
                        }
                    }
                    else
                    {
                        idToCheck = tokens[0];
                    }

                    if (!string.IsNullOrEmpty(idToCheck) && !IsValidId(idToCheck))
                    {
                        Errors.Add(new ValidationError(fileName, lineNumber, $"Malformed {matched}: '{idToCheck}' is not a valid id."));
                    }
                }

                ExpectedDepth = currentDepth + 1;
                return true;
            }

            return false;
        }

        private void CheckDefineBlocks(string[] lines, List<string> results)
        {
            var defineRegex = new Regex("^\\s*define\\s+(sprite|text)\\b", RegexOptions.IgnoreCase);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!defineRegex.IsMatch(lines[i])) continue;

                int j = i + 1;
                bool hasElse = false;
                for (; j < lines.Length; j++)
                {
                    if (defineRegex.IsMatch(lines[j])) break;
                    if (Regex.IsMatch(lines[j], "^\\s*else\\b", RegexOptions.IgnoreCase)) { hasElse = true; break; }
                    if (Regex.IsMatch(lines[j], "^\\s*(draggable\\s+window|window|button|icon|text|icon)\\b", RegexOptions.IgnoreCase)) break;
                }

                if (!hasElse)
                {
                    var header = lines[i].Trim();
                    results.Add($"Define block missing 'else' fallback: '{header}' - add an else case to avoid missing GFX/text errors.");
                }

                i = j - 1;
            }
        }

        private void CheckWindows(string[] lines, List<string> results)
        {
            var windowRegex = new Regex("^\\s*(draggable\\s+)?window\\b", RegexOptions.IgnoreCase);
            var sizePixelRegex = new Regex("size\\s+x\\d+\\s+y\\d+", RegexOptions.IgnoreCase);
            var positionRegex = new Regex("position\\s+x\\d+\\s+y\\d+", RegexOptions.IgnoreCase);
            var spriteRegex = new Regex("sprite\\s+\"?.+\"?", RegexOptions.IgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                if (!windowRegex.IsMatch(lines[i])) continue;

                int lookahead = Math.Min(lines.Length, i + 40);
                bool hasSize = false, hasPosition = false, hasSprite = false;
                for (int j = i + 1; j < lookahead; j++)
                {
                    if (sizePixelRegex.IsMatch(lines[j])) hasSize = true;
                    if (positionRegex.IsMatch(lines[j])) hasPosition = true;
                    if (spriteRegex.IsMatch(lines[j])) hasSprite = true;
                    if (Regex.IsMatch(lines[j], "^\\s*(draggable\\s+window|window|define\\s+|button|icon|text)\\b", RegexOptions.IgnoreCase)) break;
                }

                var header = lines[i].Trim();
                if (!hasSize) results.Add($"Window missing size: '{header}' - windows should declare size xNN yNN.");
                if (!hasPosition) results.Add($"Window missing position: '{header}' - windows should declare position xNN yNN.");
                if (!hasSprite) results.Add($"Window missing sprite: '{header}' - windows should declare a sprite.");

                if (Regex.IsMatch(lines[i], "size\\s+.*%" , RegexOptions.IgnoreCase))
                    results.Add($"Window uses percentage size - windows should use exact pixels (xNN yNN): '{header}'");
            }
        }

        private void CheckProgressBars(string[] lines, List<string> results)
        {
            var barRegex = new Regex("^\\s*(horizontal|vertical)\\s+bar\\s+with\\s+\\d+\\s+steps", RegexOptions.IgnoreCase);
            var varRegex = new Regex("var\\s+\"?.+\"?", RegexOptions.IgnoreCase);
            var progColorRegex = new Regex("progressed\\s+color\\s+[0-9.]+\\s+[0-9.]+\\s+[0-9.]+", RegexOptions.IgnoreCase);
            var unprogColorRegex = new Regex("unprogressed\\s+color\\s+[0-9.]+\\s+[0-9.]+\\s+[0-9.]+", RegexOptions.IgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                if (!barRegex.IsMatch(lines[i])) continue;

                int lookahead = Math.Min(lines.Length, i + 20);
                bool hasVar = false, hasProg = false, hasUnprog = false;
                for (int j = i + 1; j < lookahead; j++)
                {
                    if (varRegex.IsMatch(lines[j])) hasVar = true;
                    if (progColorRegex.IsMatch(lines[j])) hasProg = true;
                    if (unprogColorRegex.IsMatch(lines[j])) hasUnprog = true;
                    if (Regex.IsMatch(lines[j], "^\\s*(draggable\\s+window|window|define\\s+|button|icon|text|horizontal\\s+bar|vertical\\s+bar)\\b", RegexOptions.IgnoreCase)) break;
                }

                var header = lines[i].Trim();
                if (!hasVar) results.Add($"Progress bar missing linked variable (var): '{header}'");
                if (!hasProg) results.Add($"Progress bar missing 'progressed color': '{header}'");
                if (!hasUnprog) results.Add($"Progress bar missing 'unprogressed color': '{header}'");
            }
        }

        private void CheckButtons(string[] lines, List<string> results)
        {
            var buttonRegex = new Regex("^\\s*button\\b", RegexOptions.IgnoreCase);
            var onClickRegex = new Regex("^\\s*on\\s+click\\b", RegexOptions.IgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                if (!buttonRegex.IsMatch(lines[i])) continue;

                int lookahead = Math.Min(lines.Length, i + 40);
                int onClickLine = -1;
                for (int j = i + 1; j < lookahead; j++)
                {
                    if (onClickRegex.IsMatch(lines[j])) { onClickLine = j; break; }
                    if (Regex.IsMatch(lines[j], "^\\s*(draggable\\s+window|window|define\\s+|button|icon|text)\\b", RegexOptions.IgnoreCase)) break;
                }

                var header = lines[i].Trim();
                if (onClickLine == -1)
                {
                    results.Add($"Button missing 'on click' block: '{header}' - reactive buttons should include on click logic.");
                    continue;
                }

                int nextAction = onClickLine + 1;
                bool hasAction = false;
                while (nextAction < lines.Length && string.IsNullOrWhiteSpace(lines[nextAction])) nextAction++;
                if (nextAction < lines.Length)
                {
                    for (int k = nextAction; k < Math.Min(lines.Length, onClickLine + 20); k++)
                    {
                        if (Regex.IsMatch(lines[k], "^\\s*(set_variable|add_political_power|add_timed_idea|if|then|else|open_random_minigame|num_of_civilian_factories|has_political_power)\\b", RegexOptions.IgnoreCase))
                        { hasAction = true; break; }
                        if (Regex.IsMatch(lines[k], "^\\s*(draggable\\s+window|window|define\\s+|button|icon|text)\\b", RegexOptions.IgnoreCase)) break;
                    }
                }

                if (!hasAction)
                    results.Add($"'on click' block appears empty for button: '{header}' - add actions (set_variable, add_political_power, etc.).");
            }
        }
    }
}
