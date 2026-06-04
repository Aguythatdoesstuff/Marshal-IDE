using System;
using System.Collections.Generic;

namespace Compiler
{
    public class GameRuleOption
    {
        public string name;
        public bool isDefault;
    }

    public class GameRule
    {
        public string id;
        public string name;
        public string group;
        public List<GameRuleOption> options = new List<GameRuleOption>();
        public List<RawLine> rawLines = new List<RawLine>();
    }

    public class ScriptedEffect
    {
        public string id;
        public List<RawLine> rawLines = new List<RawLine>();
    }

    public class ScriptedTrigger
    {
        public string id;
        public List<RawLine> rawLines = new List<RawLine>();
    }

    public class OnAction
    {
        public List<RawLine> rawLines = new List<RawLine>();
    }

    public class ScriptParser : BaseParser
    {
        public ScriptParser()
        {
            Compiler.Logging.Logger.LogComponent("Parser", "ScriptParser initialized.");
        }
        public List<ScriptedEffect> ScriptedEffects { get; } = new List<ScriptedEffect>();
        public List<GameRule> GameRules { get; } = new List<GameRule>();
        public List<ScriptedTrigger> ScriptedTriggers { get; } = new List<ScriptedTrigger>();
        public List<OnAction> OnActions { get; } = new List<OnAction>();
        // Store the most recently parsed file for other components to use
        public ParsedScriptFile LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {
            object current = null; // one of GameRule, ScriptedEffect, ScriptedTrigger, OnAction
            string currentType = null;
            int baseDepth = 0;
            RawLine currentRaw = null;

            for (int i = 0; i < preprocessedLines.Count; i++)
            {
                var pl = preprocessedLines[i];

                // Detect headers at root
                if (pl.Depth == 0)
                {
                    // commit previous
                    if (current != null)
                    {
                        // add to appropriate list already done at creation; nothing extra
                        current = null;
                        currentType = null;
                        currentRaw = null;
                    }

                    if (pl.TrimmedLine.StartsWith("scripted effect ", StringComparison.OrdinalIgnoreCase))
                    {
                        var se = new ScriptedEffect();
                        se.id = pl.TrimmedLine.Substring("scripted effect ".Length).Trim();
                        ScriptedEffects.Add(se);
                        current = se;
                        currentType = "scripted effect";
                        baseDepth = pl.Depth;
                        currentRaw = null;
                        continue;
                    }
                    else if (pl.TrimmedLine.StartsWith("game rule ", StringComparison.OrdinalIgnoreCase))
                    {
                        var gr = new GameRule();
                        gr.id = pl.TrimmedLine.Substring("game rule ".Length).Trim();
                        GameRules.Add(gr);
                        current = gr;
                        currentType = "game rule";
                        baseDepth = pl.Depth;
                        currentRaw = null;
                        continue;
                    }
                    else if (pl.TrimmedLine.Equals("on action", StringComparison.OrdinalIgnoreCase))
                    {
                        var oa = new OnAction();
                        OnActions.Add(oa);
                        current = oa;
                        currentType = "on action";
                        baseDepth = pl.Depth;
                        currentRaw = null;
                        continue;
                    }
                    else if (pl.TrimmedLine.StartsWith("scripted trigger ", StringComparison.OrdinalIgnoreCase))
                    {
                        var st = new ScriptedTrigger();
                        st.id = pl.TrimmedLine.Substring("scripted trigger ".Length).Trim();
                        ScriptedTriggers.Add(st);
                        current = st;
                        currentType = "scripted trigger";
                        baseDepth = pl.Depth;
                        currentRaw = null;
                        continue;
                    }
                    else
                    {
                        // Unknown root-level content - record parser error
                        Errors.Add(new ParsingError(fileName, pl.LineNumber, $"Unknown root-level script header: '{pl.TrimmedLine}'"));
                        continue;
                    }
                }

                // If no current context, this is unexpected
                if (current == null)
                {
                    Errors.Add(new ParsingError(fileName, pl.LineNumber, "Found content outside of a recognized script block."));
                    continue;
                }

                // Handle GameRule known tokens at depth 1
                if (currentType == "game rule" && pl.Depth == baseDepth + 1)
                {
                    var gr = (GameRule)current;
                    if (pl.TrimmedLine.StartsWith("name", StringComparison.OrdinalIgnoreCase))
                    {
                        gr.name = GetQuotedContent(pl.TrimmedLine.Substring("name".Length).Trim());
                        continue;
                    }
                    if (pl.TrimmedLine.StartsWith("group", StringComparison.OrdinalIgnoreCase))
                    {
                        gr.group = GetQuotedContent(pl.TrimmedLine.Substring("group".Length).Trim());
                        continue;
                    }
                    if (pl.TrimmedLine.StartsWith("default option ", StringComparison.OrdinalIgnoreCase) || pl.TrimmedLine.StartsWith("option ", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isDefault = pl.TrimmedLine.StartsWith("default option ", StringComparison.OrdinalIgnoreCase);
                        string keyword = isDefault ? "default option " : "option ";
                        string opt = GetQuotedContent(pl.TrimmedLine.Substring(keyword.Length).Trim());
                        var go = new GameRuleOption { name = opt, isDefault = isDefault };
                        gr.options.Add(go);
                        continue;
                    }
                }

                // Otherwise treat as raw lines for the active block
                if (pl.Depth > baseDepth)
                {
                    // Preserve each physical line as its own RawLine so depth information is exact
                    currentRaw = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                    switch (currentType)
                    {
                        case "scripted effect": ((ScriptedEffect)current).rawLines.Add(currentRaw); break;
                        case "game rule": ((GameRule)current).rawLines.Add(currentRaw); break;
                        case "on action": ((OnAction)current).rawLines.Add(currentRaw); break;
                        case "scripted trigger": ((ScriptedTrigger)current).rawLines.Add(currentRaw); break;
                    }
                    continue;
                }

                // dedent to same or shallower than base: end current block and reprocess this line
                if (pl.Depth <= baseDepth)
                {
                    // close current and reprocess
                    current = null;
                    currentType = null;
                    currentRaw = null;
                    i--;
                    continue;
                }
            }

            // Build ParsedScriptFile for consumers (compilers)
            var parsed = new ParsedScriptFile { SourceFileName = fileName };
            parsed.ScriptedEffects.AddRange(ScriptedEffects);
            parsed.GameRules.AddRange(GameRules);
            parsed.ScriptedTriggers.AddRange(ScriptedTriggers);
            parsed.OnActions.AddRange(OnActions);
            LastParsedFile = parsed;

        }
    }

    public class ParsedScriptFile
    {
        public string SourceFileName { get; set; } = string.Empty;
        public List<ScriptedEffect> ScriptedEffects { get; } = new List<ScriptedEffect>();
        public List<GameRule> GameRules { get; } = new List<GameRule>();
        public List<ScriptedTrigger> ScriptedTriggers { get; } = new List<ScriptedTrigger>();
        public List<OnAction> OnActions { get; } = new List<OnAction>();
    }
}
