using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace importer
{
    public class GameRulesImporter : BaseImporter
    {
        public override string FolderSubPath => Path.Combine("common", "game_rules");
        public override IEnumerable<string> FileExtensions => new[] { ".txt" };

        // We still wait for Loc so the GameRules has a guarantee the dictionary is full
        public override bool RequiresLocalisation => true;

        public class ScriptedLine
        {
            public string Content { get; set; }
            public int Depth { get; set; }
            public ScriptedLine(string content, int depth) { Content = content; Depth = depth; }
        }

        public class GameRuleOption
        {
            public string Id { get; set; } // e.g., ENABLE or DISABLE
            public string NameLocKey { get; set; } // The value from 'text = ...'
            public string DescriptionLocKey { get; set; } // The value from 'desc = ...'
            public bool IsDefault { get; set; }
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        public class GameRule
        {
            public string Id { get; set; } // e.g., enable_france
            public string NameLocKey { get; set; } // e.g., RULE_ENABLE_FRANCE
            public string GroupLocKey { get; set; } // e.g., GROUP_ENABLE_FRANCE
            public string FileName { get; set; }
            public List<GameRuleOption> Options { get; } = new List<GameRuleOption>();
        }

        private readonly AsyncLocal<GameRule> _currentRule = new AsyncLocal<GameRule>();
        private readonly AsyncLocal<GameRuleOption> _currentOption = new AsyncLocal<GameRuleOption>();
        public ConcurrentBag<GameRule> Results { get; } = new ConcurrentBag<GameRule>();

        private readonly object _sync = new object();

        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            lock (_sync)
            {
                string rawValueTrimmed = value?.Trim() ?? "";
                string cleanValue = rawValueTrimmed.Trim('\"', '\'');
                string cleanKey = key?.Trim() ?? "";

                // 1. Root Level: Start of Rule (Depth 0)
                if (depth == 0 && op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{"))
                {
                    _currentRule.Value = new GameRule { Id = cleanKey, FileName = fileName };
                    DebugLogger.Log("GameRules", "", LogLevel.Info, $">>> Started Rule: {cleanKey}");
                    return;
                }

                var rule = _currentRule.Value;
                if (rule == null) return;

                // 2. Root Level: End of Rule (Depth 0)
                // Because depth is decremented BEFORE the token is passed, the root '}' is at Depth 0
                if (depth == 0 && cleanKey == "}")
                {
                    Results.Add(rule);
                    DebugLogger.Log("GameRules", "", LogLevel.Info, $"<<< Finished Rule: {rule.Id} (Saved {rule.Options.Count} options");
                    _currentRule.Value = null;
                    return;
                }

                var option = _currentOption.Value;

                // 3. We are inside the rule, but NOT inside an option yet
                if (option == null)
                {
                    if (depth == 1)
                    {
                        if (cleanKey == "name" && op == "=")
                        {
                            rule.NameLocKey = cleanValue;
                            DebugLogger.Log("GameRules", "", LogLevel.Info, $"Rule Name Loc: {cleanValue}");
                            return;
                        }
                        if (cleanKey == "group" && op == "=")
                        {
                            rule.GroupLocKey = cleanValue;
                            DebugLogger.Log("GameRules", "", LogLevel.Info, $"Rule Group Loc: {cleanValue}");
                            return;
                        }
                        if ((cleanKey == "default" || cleanKey == "option") && op == "=")
                        {
                            _currentOption.Value = new GameRuleOption { IsDefault = (cleanKey == "default") };
                            DebugLogger.Log("GameRules", "", LogLevel.Info, $"-> Opening {(cleanKey == "default" ? "Default" : "Option")} block..");
                            return;
                        }
                    }
                }
                // 4. We ARE inside an option block
                else
                {
                    // Did we just close the option? (The '}' for a depth 2 block is passed as depth 1)
                    if (depth == 1 && cleanKey == "}")
                    {
                        rule.Options.Add(option);
                        DebugLogger.Log("GameRules", "", LogLevel.Info, $"-> Closed Option: {option.Id ?? "unnamed"}");
                        _currentOption.Value = null;
                        return;
                    }

                    // Extract the ID and LocKeys, but DON'T return for 'name', so it gets saved as a raw line
                    if (depth == 2)
                    {
                        if (cleanKey == "name" && op == "=")
                        {
                            return; // this is bassically junk data we dont need but hoi4 needs
                        }
                        if (cleanKey == "text" && op == "=")
                        {
                            option.NameLocKey = cleanValue;
                            DebugLogger.Log("GameRules", "", LogLevel.Info, $"Option Loc Key identified: {cleanValue}");
                            return; // Return here so 'text = ...' doesn't get saved to raw lines
                        }
                        else if (cleanKey == "desc" && op == "=")
                        {
                            option.DescriptionLocKey = cleanValue;
                            DebugLogger.Log("GameRules", "", LogLevel.Info, $"Option Desc Loc identified: {cleanValue}");
                            return; // Return here so 'desc = ...' doesn't get saved to raw lines
                        }
                    }

                    // 5. Construct and save the raw lines (just like OnActionsImporter)
                    var rawLine = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, depth);
                    option.Lines.Add(new ScriptedLine(rawLine, depth));
                    DebugLogger.Log("GameRules", "", LogLevel.Info, $"Saved Line (D{depth}): {rawLine}");
                }
            }
        }
    }
}