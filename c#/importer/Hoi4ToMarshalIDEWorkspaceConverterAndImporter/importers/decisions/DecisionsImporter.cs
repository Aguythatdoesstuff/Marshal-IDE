using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace importer
{
    public class DecisionsImporter : BaseImporter
    {
        // Target the base decisions folder
        public override string FolderSubPath => Path.Combine("common", "decisions");
        public override IEnumerable<string> FileExtensions => new[] { ".txt" };
        //public override SearchOption DirectorySearchOption => SearchOption.TopDirectoryOnly;
        public override bool RequiresLocalisation => true;

        public class ScriptedLine
        {
            public string Content { get; set; }
            public int Depth { get; set; }
            public ScriptedLine(string content, int depth) { Content = content; Depth = depth; }
        }

        public class Decision
        {
            public string Id { get; set; }
            public string Category { get; set; }
            public string Sprite { get; set; }
            public string NameLocKey { get; set; }
            public string DescLocKey { get; set; }
            public string FileName { get; set; }
            public string priority { get; set; } = string.Empty;
            public string cost { get; set; } = string.Empty;
            public List<ScriptedLine> Lines { get; set; } = new List<ScriptedLine>();
        }

        private readonly AsyncLocal<string> _currentCategory = new AsyncLocal<string>();
        private readonly AsyncLocal<Decision> _currentDecision = new AsyncLocal<Decision>();
        public ConcurrentBag<Decision> Results { get; } = new ConcurrentBag<Decision>();

        private readonly object _sync = new object();

        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            // Safeguard: Do not process category files here if BaseImporter reads recursively
            if (fileName.Contains(Path.DirectorySeparatorChar + "categories" + Path.DirectorySeparatorChar) ||
                fileName.Contains("/categories/"))
            {
                return;
            }

            lock (_sync)
            {
                string cleanKey = key?.Trim() ?? "";
                string rawValue = value?.Trim() ?? "";
                string cleanValue = rawValue.Trim('\"', '\'');

                // 1. Category Wrapper (Depth 0 in decision files)
                if (depth == 0 && op == "=" && (string.IsNullOrEmpty(rawValue) || rawValue == "{"))
                {
                    _currentCategory.Value = cleanKey;
                    DebugLogger.Log("DecisionsImport", fileName, LogLevel.Info, $"ENTERING CATEGORY WRAPPER: {cleanKey} (File: {fileName}");
                    return;
                }

                // 2. Start of a specific Decision (Depth 1)
                if (depth == 1 && op == "=" && (string.IsNullOrEmpty(rawValue) || rawValue == "{"))
                {
                    _currentDecision.Value = new Decision
                    {
                        Id = cleanKey,
                        Category = _currentCategory.Value,
                        NameLocKey = cleanKey,
                        DescLocKey = cleanKey + "_desc",
                        FileName = fileName
                    };
                    DebugLogger.Log("DecisionsImport", fileName, LogLevel.Info, $"FOUND DECISION: {cleanKey} (Inside Cat: {_currentCategory.Value})");
                    return;
                }

                var decision = _currentDecision.Value;
                if (decision == null) return;

                // 3. Closing a Decision
                if (depth == 1 && cleanKey == "}")
                {
                    DebugLogger.Log("DecisionsImport", fileName, LogLevel.Info, $"<- FINISHED DECISION: {decision.Id}. Total Lines Saved: {decision.Lines.Count}");
                    Results.Add(decision);
                    _currentDecision.Value = null;
                    return;
                }

                // 4. Property Mapping (Icon) - Decision properties are at Depth 2
                if (depth == 2 && cleanKey == "icon" && op == "=")
                {
                    decision.Sprite = cleanValue;
                    DebugLogger.Log("DecisionsImport", fileName, LogLevel.Info, $"- Mapping Sprite: {cleanValue} for Decision {decision.Id}");
                    return;
                }

                if (depth == 2 && cleanKey == "priority" && op == "=")
                {
                    decision.priority = cleanValue;
                    DebugLogger.Log("DecisionsImport", fileName, LogLevel.Info, $"- Mapping Priority: {cleanValue} for Decision {decision.Id}");
                    return;
                }

                if (depth == 2 && cleanKey == "cost" && op == "=")
                {
                    decision.cost = cleanValue;
                    DebugLogger.Log("DecisionsImport", fileName, LogLevel.Info, $"- Mapping cost: {cleanValue} for Decision {decision.Id}");
                    return;
                }

                // Construct line content for the raw list
                var rawLine = RawLineHelper.BuildAndLog(cleanKey, op, rawValue, depth);
                decision.Lines.Add(new ScriptedLine(rawLine, depth));
                DebugLogger.Log("DecisionsImport", fileName, LogLevel.Info, $"- Storing Line (D{depth}): {rawLine}");
            }
        }
    }
}