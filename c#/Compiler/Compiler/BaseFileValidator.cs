using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Compiler
{
    public interface IValidator
    {
        bool Validate(string filePath, out string error);
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; } = new List<string>();
    }

    /// Abstract base file validator. Specializations should add expected block keywords
    /// to ExpectedIndentationBlocks and may override ValidateLines for custom rules.
    /// Default indentation policy: 4 spaces => one indent level, or tabs count as levels.
    public abstract class BaseFileValidator : IValidator
    {
        protected int SpacesPerIndent { get; set; } = 4; // default 4 spaces
        protected bool AllowTabsAsIndent { get; set; } = true;
        protected HashSet<string> ExpectedIndentationBlocks { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool Validate(string filePath, out string error)
        {
            error = null;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath);
            }
            catch (Exception ex)
            {
                error = "Failed to read file: " + ex.Message;
                return false;
            }

            var errors = new List<string>();
            ValidateLines(lines, errors);
            if (errors.Any())
            {
                error = string.Join(Environment.NewLine, errors);
                return false;
            }

            return true;
        }

        // Child classes can override to add checks
        protected virtual void ValidateLines(string[] lines, List<string> errors)
        {
            int braceLevel = 0;
            int pendingKeywordIncrease = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var trimmed = raw.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var (okIndent, indentUnits, indentErr) = ComputeIndentationUnits(raw);
                if (!okIndent) errors.Add($"Line {i + 1}: {indentErr}");

                // If line starts with a closing brace reduce brace level first
                if (trimmed.StartsWith("}"))
                {
                    int closings = CountChar(trimmed, '}');
                    braceLevel = Math.Max(0, braceLevel - closings);
                }

                int expectedIndent = braceLevel + pendingKeywordIncrease;

                if (okIndent && indentUnits != expectedIndent)
                {
                    errors.Add($"Line {i + 1}: expected indent {expectedIndent} but found {indentUnits} (content: '{trimmed}')");
                }

                // Detect keywords that indicate a following block should be indented
                foreach (var k in ExpectedIndentationBlocks)
                {
                    if (trimmed.StartsWith(k, StringComparison.OrdinalIgnoreCase) || trimmed.Contains(" " + k + " ", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!trimmed.Contains("{")) pendingKeywordIncrease++;
                    }
                }

                // Detect assignment-style blocks: key = { (opening braces handled below)
                if (trimmed.Contains("=") && trimmed.Contains("{"))
                {
                    // no-op: opening braces processed below
                }

                int openings = CountChar(trimmed, '{');
                if (openings > 0)
                {
                    braceLevel += openings;
                    if (pendingKeywordIncrease > 0)
                    {
                        var consume = Math.Min(pendingKeywordIncrease, openings);
                        pendingKeywordIncrease -= consume;
                    }
                }

                // If this line shows an increased indent, consume pending increases
                if (pendingKeywordIncrease > 0 && okIndent && indentUnits >= expectedIndent + 1)
                {
                    pendingKeywordIncrease = Math.Max(0, pendingKeywordIncrease - 1);
                }

                // Closing braces elsewhere on the line reduce level
                if (!trimmed.StartsWith("}") && trimmed.Contains("}"))
                {
                    int c = CountChar(trimmed, '}');
                    braceLevel = Math.Max(0, braceLevel - c);
                }
            }
        }

        private static int CountChar(string s, char c)
        {
            int r = 0;
            foreach (var ch in s) if (ch == c) r++;
            return r;
        }

        private (bool ok, int units, string error) ComputeIndentationUnits(string raw)
        {
            if (raw == null) return (true, 0, null);
            int idx = 0; int tabs = 0; int spaces = 0;
            while (idx < raw.Length)
            {
                if (raw[idx] == '\t') { tabs++; idx++; }
                else if (raw[idx] == ' ') { spaces++; idx++; }
                else break;
            }

            if (tabs > 0 && spaces > 0)
                return (false, 0, "Mixed tabs and spaces in indentation are not allowed");

            if (tabs > 0)
            {
                if (!AllowTabsAsIndent) return (false, 0, "Tabs are not allowed for indentation");
                return (true, tabs, null);
            }

            if (spaces > 0)
            {
                if (spaces % SpacesPerIndent != 0) return (false, 0, $"Spaces indentation must be a multiple of {SpacesPerIndent} (found {spaces})");
                return (true, spaces / SpacesPerIndent, null);
            }

            return (true, 0, null);
        }
    }

    /// Manager that dispatches validators by extension and enforces absolute path/existence checks.
    public class BaseValidatorManager
    {
        private readonly Dictionary<string, IValidator> _map = new(StringComparer.OrdinalIgnoreCase);

        private static readonly string[] SupportedExtensions = new[]
        {
            ".decision",
            ".event",
            ".scriptedgui",
            ".script",
            ".idea",
            ".focus",
            ".scriptedeffect"
        };

        public BaseValidatorManager()
        {
            var generic = new GenericScriptValidator();
            foreach (var e in SupportedExtensions)
                _map[e] = generic;
        }

        public void RegisterValidator(string extension, IValidator validator)
        {
            if (!extension.StartsWith(".")) extension = "." + extension;
            _map[extension] = validator;
        }

        public ValidationResult ProcessFiles(IEnumerable<string> absolutePaths)
        {
            var res = new ValidationResult { IsValid = true };
            if (absolutePaths == null || !absolutePaths.Any())
            {
                res.IsValid = false; res.Errors.Add("No files provided."); return res;
            }

            foreach (var path in absolutePaths)
            {
                if (string.IsNullOrWhiteSpace(path)) { res.IsValid = false; res.Errors.Add("Empty path provided."); continue; }
                if (!Path.IsPathRooted(path)) { res.IsValid = false; res.Errors.Add($"Path is not absolute: {path}"); continue; }
                if (!File.Exists(path)) { res.IsValid = false; res.Errors.Add($"File not found: {path}"); continue; }

                var ext = Path.GetExtension(path);
                if (string.IsNullOrEmpty(ext) || !_map.ContainsKey(ext)) { res.IsValid = false; res.Errors.Add($"Unsupported extension '{ext}' for file {path}"); continue; }

                var validator = _map[ext];
                if (!validator.Validate(path, out var err)) { res.IsValid = false; res.Errors.Add($"{path}: {err}"); }
            }

            return res;
        }

        public bool TryValidateAndRun(IEnumerable<string> absolutePaths, Action<IEnumerable<string>> onAllValid)
        {
            var r = ProcessFiles(absolutePaths);
            if (!r.IsValid) { foreach (var e in r.Errors) Console.WriteLine("Validation error: " + e); return false; }
            onAllValid?.Invoke(absolutePaths); return true;
        }
    }

    public class GenericScriptValidator : BaseFileValidator
    {
        public GenericScriptValidator()
        {
            ExpectedIndentationBlocks.Add("if");
            ExpectedIndentationBlocks.Add("else");
            ExpectedIndentationBlocks.Add("else if");
            ExpectedIndentationBlocks.Add("then");
            ExpectedIndentationBlocks.Add("or");
            ExpectedIndentationBlocks.Add("and");
            ExpectedIndentationBlocks.Add("not");
        }
    }
}
