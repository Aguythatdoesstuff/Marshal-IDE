using Microsoft.VisualBasic.FileIO;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using static importer.ScriptedLocalisationImporter;

namespace importer
{
    public class ScriptedLocalisationImporter : BaseImporter
    {
        public override string FolderSubPath => Path.Combine("common", "scripted_localisation");
        public override IEnumerable<string> FileExtensions => new[] { ".txt" };

        // We still wait for Loc so the Compiler has a guarantee the dictionary is full
        public override bool RequiresLocalisation => true;

        public class ScriptedLine
        {
            public string Content { get; set; }
            public int Depth { get; set; }
            public ScriptedLine(string content, int depth) { Content = content; Depth = depth; }
        }

        public enum ConditionKind { If, ElseIf, Else }

        public class Focus
        {
            // The localisation key from inside a text block
            public string NameLocKey { get; set; }

            // Whether this text option is an if / else if / else or unconditional
            public ConditionKind Condition { get; set; } = ConditionKind.Else;

            // Raw lines inside this text block (triggers, other raw content)
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
            // If a trigger block is open, this stores the depth at which 'trigger =' was opened.
            // Inner trigger lines should be saved at one depth less than parser depth.
            public int TriggerOpenDepth { get; set; } = -1;
        }

        public class ScriptedLocalisation
        {
            public string Id { get; set; } // e.g., enable_france
            public string Type { get; set; } // e.g., enable_france
            public string FileName { get; set; }
            // Raw lines at the focus tree root (depth 1) that aren't otherwise parsed
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
            public List<Focus> Options { get; } = new List<Focus>();
        }

        private readonly AsyncLocal<ScriptedLocalisation> _currentScriptedLocalisation = new AsyncLocal<ScriptedLocalisation>();
        private readonly AsyncLocal<Focus> _currentFocus = new AsyncLocal<Focus>();
        // (No prerequisite/mutually_exclusive handling in scripted localisation)
        public ConcurrentBag<ScriptedLocalisation> Results { get; } = new ConcurrentBag<ScriptedLocalisation>();

        private readonly object _sync = new object();

        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            lock (_sync)
            {
                string rawValueTrimmed = value?.Trim() ?? "";
                string cleanValue = rawValueTrimmed.Trim('\"', '\'');
                string cleanKey = key?.Trim() ?? "";
                if (depth == 0 && cleanKey == "add_namespace" && op == "=")
                {
                    return; // useless data we dont need but hoi4 does
                }
                // 1. Root Level: Start of ScriptedLocalisation (Depth 0)
                if (depth == 0 && op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{"))
                {
                    string ScriptedLocalisationType = cleanKey.Replace("_ScriptedLocalisation", "");

                    _currentScriptedLocalisation.Value = new ScriptedLocalisation { Type = ScriptedLocalisationType, FileName = fileName };
                    DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Info, $"Started ScriptedLocalisation type: {ScriptedLocalisationType}");
                    return;
                }

                var ScriptedLocalisationValue = _currentScriptedLocalisation.Value;
                if (ScriptedLocalisationValue == null) return;

                // 2. Root Level: End of ScriptedLocalisation (Depth 0)
                // Because depth is decremented BEFORE the token is passed, the root '}' is at Depth 0
                if (depth == 0 && cleanKey == "}")
                {
                    Results.Add(ScriptedLocalisationValue);
                    DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Info, $"Finished ScriptedLocalisation: {ScriptedLocalisationValue.Type} (Saved {ScriptedLocalisationValue.Options.Count} options) with the id: {ScriptedLocalisationValue.Id}");
                    _currentScriptedLocalisation.Value = null;
                    return;
                }

                var option = _currentFocus.Value;

                // 3. We are inside the ScriptedLocalisationValue, but NOT inside an option yet
                if (option == null)
                {
                    // Handle depth-1 special keys (id, default, opening a focus block)
                    if (depth == 1)
                    {
                        // Some files use 'name' instead of 'id' for defined_text entries
                        if (cleanKey == "name" && op == "=")
                        {
                            ScriptedLocalisationValue.Id = cleanValue;
                            DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Info, $"ScriptedLocalisation id: {cleanValue}");
                            return;
                        }

  
                        if (cleanKey == "text" && op == "=")
                        {
                            _currentFocus.Value = new Focus();
                            DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Info, "Opening text block...");

      
                            if (cleanKey == "text" && isOneLiner)
                            {
                                var inner = rawValueTrimmed.Trim();
                                if (inner.StartsWith("{")) inner = inner.Substring(1).Trim();
                                if (inner.EndsWith("}")) inner = inner.Substring(0, inner.Length - 1).Trim();

                                // Extract localization_key if present
                                var locMatch = Regex.Match(inner, "localization_key\\s*=\\s*['\"]?([A-Za-z0-9_\\-]+)['\"]?", RegexOptions.IgnoreCase);
                                if (locMatch.Success)
                                {
                                    _currentFocus.Value.NameLocKey = locMatch.Groups[1].Value;
                                    DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Info, $"LocalizationKey (one-liner): {_currentFocus.Value.NameLocKey}");
                                }

                                // Extract trigger block if present and save its inner lines (do NOT save the 'trigger =' wrapper).
                                var trigMatch = Regex.Match(inner, "trigger\\s*=\\s*\\{([^}]*)\\}", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                if (trigMatch.Success)
                                {
                                    var trigContent = trigMatch.Groups[1].Value.Trim();
                                    var lines = trigContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                                    int saveDepth = Math.Max(0, depth - 1); // store inner lines at one depth less
                                    foreach (var line in lines)
                                    {
                                        var trimmed = line.Trim();
                                        var rawTrigLine = RawLineHelper.BuildAndLog(trimmed.Split(' ')[0], "=", trimmed.Substring(trimmed.IndexOf(' ') + 1).Trim(), saveDepth);
                                        _currentFocus.Value.Lines.Add(new ScriptedLine(rawTrigLine, saveDepth));
                                    }
                                    DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Raw, $"Trigger (one-liner): {trigContent}");

                                    // Mark condition as If or ElseIf depending on previous options
                                    var last = ScriptedLocalisationValue.Options.LastOrDefault();
                                    if (last != null && (last.Condition == ConditionKind.If || last.Condition == ConditionKind.ElseIf))
                                    {
                                        _currentFocus.Value.Condition = ConditionKind.ElseIf;
                                    }
                                    else
                                    {
                                        _currentFocus.Value.Condition = ConditionKind.If;
                                    }
                                    DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Info, $"Condition set to: {_currentFocus.Value.Condition}");
                                }
                                // Close this text option immediately since it was one-liner
                                ScriptedLocalisationValue.Options.Add(_currentFocus.Value);
                                DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Info, $"Closed text (one-liner): {_currentFocus.Value.NameLocKey ?? "unnamed"}");
                                _currentFocus.Value = null;
                                return;
                            }

                            return;
                        }
                    }

                    // For any other root-level or deeper tokens that are not part of an option, save the raw line
                    var rootLine = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, depth);
                    ScriptedLocalisationValue.Lines.Add(new ScriptedLine(rootLine, depth));
                    DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Raw, $"Saved Root Line (D{depth}): {rootLine}");
                    return;
                }
                // 4. We ARE inside an option block
                else
                {
                    // Did we just close the option? (The '}' for a depth 2 block is passed as depth 1)
                        if (depth == 1 && cleanKey == "}" && !isOneLiner)
                    {
                        ScriptedLocalisationValue.Options.Add(option);
                        DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Info, $"Closed text: {option.NameLocKey ?? "unnamed"}");
                        _currentFocus.Value = null;
                        return;
                    }

                    if (depth >= 2)
                    {

                        // Only care about localization_key and trigger/raw lines inside text blocks
                        if (cleanKey == "localization_key" && op == "=")
                        {
                            option.NameLocKey = cleanValue;
                            DebugLogger.Log("ScriptedLocalisation", fileName, LogLevel.Info, $"LocalizationKey: {cleanValue}");
                            // Do NOT save the raw localization_key line since we already store the id
                            return;
                        }

                        // Handle trigger opening: do not save the 'trigger =' wrapper, just remember its depth
                        if (cleanKey == "trigger" && op == "=")
                        {
                            option.TriggerOpenDepth = depth;

                            var last = ScriptedLocalisationValue.Options.LastOrDefault();
                            if (last != null && (last.Condition == ConditionKind.If || last.Condition == ConditionKind.ElseIf))
                            {
                                option.Condition = ConditionKind.ElseIf;
                            }
                            else
                            {
                                option.Condition = ConditionKind.If;
                            }
                            DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Info, $"Trigger opened at depth {depth}, Condition set to: {option.Condition}");
                            return;
                        }

                        // If we are inside an open trigger block, save inner lines at one depth less and skip closing brace
                        if (option.TriggerOpenDepth >= 0)
                        {
                            // If this token is the closing brace for the trigger block, close it
                            if (cleanKey == "}" && depth == option.TriggerOpenDepth && !isOneLiner)
                            {
                                option.TriggerOpenDepth = -1;
                                return;
                            }

                            if (depth > option.TriggerOpenDepth)
                            {
                                var rawLine = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, Math.Max(0, depth - 1));
                                option.Lines.Add(new ScriptedLine(rawLine, Math.Max(0, depth - 1)));
                                DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Raw, $"Saved Trigger Inner Line (D{Math.Max(0, depth - 1)}): {rawLine}");
                                return;
                            }
                        }
                    }

                    // 5. Construct and save the raw lines (triggers and other raw content).
                    // Do not save 'localization_key' here because it's stored in NameLocKey.
                    var savedLine = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, depth);
                    option.Lines.Add(new ScriptedLine(savedLine, depth));
                    DebugLogger.Log("ScriptedLocalisationImporter", fileName, LogLevel.Raw, $"Saved Line (D{depth}): {savedLine}");
                }
            }
        }
    }
}