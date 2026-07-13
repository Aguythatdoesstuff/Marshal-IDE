using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using static importer.FocusTreeImporter;

namespace importer
{
    public class FocusTreeImporter : BaseImporter
    {
        public override string FolderSubPath => Path.Combine("common", "national_focus");
        public override IEnumerable<string> FileExtensions => new[] { ".txt" };

        // We still wait for Loc so the Compiler has a guarantee the dictionary is full
        public override bool RequiresLocalisation => true;

        public class ScriptedLine
        {
            public string Content { get; set; }
            public int Depth { get; set; }
            public ScriptedLine(string content, int depth) { Content = content; Depth = depth; }
        }

        public class Focus
        {
            public string NameLocKey { get; set; } // The value from 'text = ...'
            // Focus-specific extracted fields
            public string Id { get; set; }
            public int? CostDays { get; set; }
            public string Sprite { get; set; }
            public int? X { get; set; }
            public int? Y { get; set; }

            // New fields requested: multiple prerequisites and mutually exclusive focuses, and relative position id
            public List<string> Prerequisites { get; } = new List<string>();
            public List<string> MutuallyExclusive { get; } = new List<string>();
            public string RelativePositionId { get; set; }

            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        public class FocusTree
        {
            public string Id { get; set; } // e.g., enable_france
            public string Type { get; set; } // e.g., enable_france
            public string Sprite { get; set; } // The FocusTree sprite
            public string NameLocKey { get; set; } // e.g., RULE_ENABLE_FRANCE
            public string DescLocKey { get; set; } // e.g., GROUP_ENABLE_FRANCE
            public string FileName { get; set; }
            public string Tag { get; set; } // e.g., FRA
            public Boolean IsDefault { get; set; }
            // Raw lines at the focus tree root (depth 1) that aren't otherwise parsed
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
            public List<Focus> Options { get; } = new List<Focus>();
        }

        private readonly AsyncLocal<FocusTree> _currentFocusTree = new AsyncLocal<FocusTree>();
        private readonly AsyncLocal<Focus> _currentFocus = new AsyncLocal<Focus>();
        // Tracks whether we're inside a prerequisite / mutually_exclusive block for the current option
        private readonly AsyncLocal<string> _currentListContext = new AsyncLocal<string>();
        private readonly AsyncLocal<int?> _currentListDepth = new AsyncLocal<int?>();
        public ConcurrentBag<FocusTree> Results { get; } = new ConcurrentBag<FocusTree>();

        private readonly object _sync = new object();

        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            lock (_sync)
            {
                string rawValueTrimmed = value?.Trim() ?? "";
                string cleanValue = rawValueTrimmed.Trim('\"', '\'');
                string cleanKey = key?.Trim() ?? "";

                // 1. Root Level: Start of FocusTree (Depth 0)
                if (depth == 0 && op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{"))
                {
                    string FocusTreeType = cleanKey.Replace("_FocusTree", "");

                    _currentFocusTree.Value = new FocusTree { Type = FocusTreeType, FileName = fileName };
                    DebugLogger.Log("Compiler", "", LogLevel.Info, $">>> Started FocusTree type: {FocusTreeType}");
                    return;
                }
                if (key == "original_tag" && op == "=" && !string.IsNullOrEmpty(value))
                {
                    string tag = cleanKey.Replace("_FocusTree", "");

                    _currentFocusTree.Value.Tag = value.Trim();
                    DebugLogger.Log("Compiler", "", LogLevel.Info, $">>> Started FocusTree country tag: {value.Trim()}");
                    return;
                }
                var FocusTreeValue = _currentFocusTree.Value;
                if (FocusTreeValue == null) return;

                // 2. Root Level: End of FocusTree (Depth 0)
                // Because depth is decremented BEFORE the token is passed, the root '}' is at Depth 0
                if (depth == 0 && cleanKey == "}")
                {
                    Results.Add(FocusTreeValue);
                    DebugLogger.Log("Compiler", "", LogLevel.Info, $"<<< Finished FocusTree: {{FocusTreeValue.Type}} (Saved {{FocusTreeValue.Options.Count}} options) with the id: {{FocusTreeValue.Id");
                    _currentFocusTree.Value = null;
                    return;
                }

                var option = _currentFocus.Value;

                // If we're currently inside a prerequisite / mutually_exclusive block, handle those tokens first
                if (option != null && _currentListContext.Value != null)
                {
                    // Closing the current list block
                    if (cleanKey == "}" && _currentListDepth.Value.HasValue && depth == _currentListDepth.Value)
                    {
                        DebugLogger.Log("Compiler", "", LogLevel.Info, $"Closed List Block: {{_currentListContext.Value");
                        _currentListContext.Value = null;
                        _currentListDepth.Value = null;
                        return;
                    }

                    // Inside list entries: look for 'focus = id'
                    if (cleanKey == "focus" && op == "=")
                    {
                        var fid = cleanValue;
                        if (!string.IsNullOrEmpty(fid))
                        {
                            if (_currentListContext.Value == "prerequisite")
                            {
                                option.Prerequisites.Add(fid);
                                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Added prerequisite: {fid}");
                            }
                            else if (_currentListContext.Value == "mutually_exclusive")
                            {
                                option.MutuallyExclusive.Add(fid);
                                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Added mutually_exclusive: {fid}");
                            }
                        }
                        return;
                    }
                    // If we encounter a one-liner relationship while another list block is open,
                    // still attempt to parse it (this can happen in some file layouts).
                    if (isOneLiner && (cleanKey == "prerequisite" || cleanKey == "mutually_exclusive") && op == "=")
                    {
                        var inner = rawValueTrimmed.Trim();
                        if (inner.StartsWith("{")) inner = inner.Substring(1).Trim();
                        if (inner.EndsWith("}")) inner = inner.Substring(0, inner.Length - 1).Trim();
                        var matches = Regex.Matches(inner, "focus\\s*=\\s*['\"]?([A-Za-z0-9_\\-]+)['\"]?", RegexOptions.IgnoreCase);
                        foreach (Match m in matches)
                        {
                            var fid = m.Groups[1].Value;
                            if (cleanKey == "prerequisite")
                            {
                                if (!option.Prerequisites.Contains(fid)) option.Prerequisites.Add(fid);
                                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Added prerequisite (inline while block open): {fid}");
                            }
                            else
                            {
                                if (!option.MutuallyExclusive.Contains(fid)) option.MutuallyExclusive.Add(fid);
                                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Added mutually_exclusive (inline while block open): {fid}");
                            }
                        }
                        return;
                    }
                    // Ignore any other tokens inside these blocks to avoid saving wrapper/raw lines — we only want the focus ids
                    return;
                }

                // 3. We are inside the FocusTreeValue, but NOT inside an option yet
                if (option == null)
                {
                    // Handle depth-1 special keys (id, default, opening a focus block)
                    if (depth == 1)
                    {
                        if (cleanKey == "id" && op == "=")
                        {
                            FocusTreeValue.Id = cleanValue;
                            DebugLogger.Log("Compiler", "", LogLevel.Info, $"FocusTree id: {cleanValue}");
                            return;
                        }

                        if (cleanKey == "default" && op == "=" && cleanValue == "yes")
                        {
                            FocusTreeValue.IsDefault = true;
                            return;
                        }

                        // Opening an option/focus block
                        if (cleanKey == "focus" && op == "=")
                        {
                            _currentFocus.Value = new Focus();
                            DebugLogger.Log("Compiler", "", LogLevel.Info, $"Opening Focus block...");
                            return;
                        }
                    }

                    // For any other root-level or deeper tokens that are not part of an option, save the raw line
                    var rootLine = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, depth);
                    FocusTreeValue.Lines.Add(new ScriptedLine(rootLine, depth));
                    DebugLogger.Log("Compiler", "", LogLevel.Info, $"Saved Root Line (D{{depth}}): {{rootLine");
                    return;
                }
                // 4. We ARE inside an option block
                else
                {
                    // Did we just close the option? (The '}' for a depth 2 block is passed as depth 1)
                    if (depth == 1 && cleanKey == "}")
                    {
                        FocusTreeValue.Options.Add(option);
                        DebugLogger.Log("Compiler", "", LogLevel.Info, $"Closed focus: {option.Id ?? "unnamed"}");
                        _currentFocus.Value = null;
                        return;
                    }

                    if (depth >= 2)
                    {

                        if (cleanKey == "id" && op == "=")
                        {
                            option.Id = cleanValue;
                            DebugLogger.Log("Compiler", "", LogLevel.Info, $"Focus Id: {cleanValue}");
                            return;
                        }

                        // relative_position_id = political_effort
                        if (cleanKey == "relative_position_id" && op == "=")
                        {
                            option.RelativePositionId = cleanValue;
                            DebugLogger.Log("Compiler", "", LogLevel.Info, $"RelativePositionId: {cleanValue}");
                            return;
                        }

                        if (cleanKey == "cost" && op == "=")
                        {
                            // convert hoi4 cost to days
                            if (double.TryParse(cleanValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double costVal))
                            {
                                int days = (int)Math.Ceiling(costVal * 7.0);
                                option.CostDays = days;
                                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Focus Cost: {cleanValue} -> {days} day");
                            }
                            return;
                        }

                        if ((cleanKey == "icon") && op == "=")
                        {
                            option.Sprite = cleanValue;
                            DebugLogger.Log("Compiler", "", LogLevel.Info, $"Focus Sprite: {cleanValue}");
                            return;
                        }

                        if (cleanKey == "x" && op == "=")
                        {
                            if (int.TryParse(cleanValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int xi))
                            {
                                option.X = xi;
                                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Focus X: {xi}");
                            }
                            return;
                        }

                        if (cleanKey == "y" && op == "=")
                        {
                            if (int.TryParse(cleanValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int yi))
                            {
                                option.Y = yi;
                                DebugLogger.Log("Compiler", "", LogLevel.Info, $"Focus Y: {yi}");
                            }
                            return;
                        }
                    }

                    // Detect start of prerequisite or mutually_exclusive blocks at depth >=2
                    if (depth >= 2 && (cleanKey == "prerequisite" || cleanKey == "mutually_exclusive") && op == "=")
                    {
                        // If this token is a one-liner (both '{' and '}' on same line), extract any focus ids from the value
                        if (isOneLiner)
                        {
                            // Normalize the content (strip surrounding braces) and find focus entries
                            var inner = rawValueTrimmed.Trim();
                            if (inner.StartsWith("{")) inner = inner.Substring(1).Trim();
                            if (inner.EndsWith("}")) inner = inner.Substring(0, inner.Length - 1).Trim();

                            var matches = Regex.Matches(inner, "focus\\s*=\\s*['\"]?([A-Za-z0-9_\\-]+)['\"]?", RegexOptions.IgnoreCase);
                            if (matches.Count == 0)
                            {
                                DebugLogger.Log("Compiler", "", LogLevel.Info, $"One-liner had no focus= entries: {rawValueTrimmed}");
                            }
                            foreach (Match m in matches)
                            {
                                var fid = m.Groups[1].Value;
                                if (cleanKey == "prerequisite")
                                {
                                    option.Prerequisites.Add(fid);
                                    DebugLogger.Log("Compiler", "", LogLevel.Info, $"Added prerequisite (one-liner): {fid}");
                                }
                                else
                                {
                                    option.MutuallyExclusive.Add(fid);
                                    DebugLogger.Log("Compiler", "", LogLevel.Info, $"Added mutually_exclusive (one-liner): {fid}");
                                }
                            }
                        }
                        else
                        {
                            // Start of a multi-line block. Mark context so inner tokens are captured above.
                            _currentListContext.Value = cleanKey; // 'prerequisite' or 'mutually_exclusive'
                            _currentListDepth.Value = depth; // Block depth when entries are passed (closing '}' will be passed with this depth)
                            DebugLogger.Log("Compiler", "", LogLevel.Info, $"Opened List Block: {{cleanKey}} at depth {{depth");
                        }

                        // In both cases we handled the relationship entries; do not save the wrapper/raw line
                        return;
                    }

                    // 5. Construct and save the raw lines (just like OnActionsImporter)
                    var rawLine = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, depth);
                    option.Lines.Add(new ScriptedLine(rawLine, depth));
                    DebugLogger.Log("Compiler", "", LogLevel.Info, $"Saved Line (D{depth}): {rawLine}");
                }
            }
        }
    }
}