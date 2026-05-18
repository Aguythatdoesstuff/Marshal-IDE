using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace importer
{
    public class IdeasImporter : BaseImporter
    {
        public override string FolderSubPath => Path.Combine("common", "ideas");
        public override IEnumerable<string> FileExtensions => new[] { ".txt" };
        public override bool RequiresLocalisation => true;

        public class ScriptedLine
        {
            public string Content { get; set; }
            public int Depth { get; set; }
            public ScriptedLine(string content, int depth) { Content = content; Depth = depth; }
        }

        public class Ideas
        {
            public string Id { get; set; }
            public string Category { get; set; }
            public string Sprite { get; set; }
            public string NameLocKey { get; set; }
            public string DescLocKey { get; set; }
            public string FileName { get; set; }
            public List<ScriptedLine> Lines { get; set; } = new List<ScriptedLine>();
        }

        private readonly AsyncLocal<string> _currentCategory = new AsyncLocal<string>();
        private readonly AsyncLocal<Ideas> _currentIdea = new AsyncLocal<Ideas>();
        public ConcurrentBag<Ideas> Results { get; } = new ConcurrentBag<Ideas>();

        private readonly object _sync = new object();

        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            lock (_sync)
            {
                string cleanKey = key?.Trim() ?? "";
                string rawValue = value?.Trim() ?? "";
                string cleanValue = rawValue.Trim('\"', '\'');

                // 0. Ignore the root wrapper
                if (depth == 0 && cleanKey == "ideas") return;

                // 1. Category Level (country, political, etc.)
                if (depth == 1 && op == "=" && (string.IsNullOrEmpty(rawValue) || rawValue == "{"))
                {
                    _currentCategory.Value = cleanKey;
                    DebugLogger.Log("IdeasImporter", fileName, LogLevel.Info, $"Entering Category: {cleanKey}");
                    return;
                }

                // 2. Start of a specific Idea
                if (depth == 2 && op == "=" && (string.IsNullOrEmpty(rawValue) || rawValue == "{"))
                {
                    _currentIdea.Value = new Ideas
                    {
                        Id = cleanKey,
                        Category = _currentCategory.Value,
                        NameLocKey = cleanKey,
                        DescLocKey = cleanKey + "_desc",
                        FileName = fileName
                    };
                    DebugLogger.Log("IdeasImporter", fileName, LogLevel.Info, $"Found Idea: {cleanKey} (Cat: {_currentCategory.Value})");
                    return;
                }

                var idea = _currentIdea.Value;
                if (idea == null) return;

                // 3. Closing an Idea
                if (depth == 2 && cleanKey == "}")
                {
                    DebugLogger.Log("IdeasImporter", fileName, LogLevel.Info, $"Finished Idea: {idea.Id}. Total Lines Saved: {idea.Lines.Count}");
                    Results.Add(idea);
                    _currentIdea.Value = null;
                    return;
                }

                // 4. Property Mapping & Line Logging
                if (depth == 3 && cleanKey == "picture" && op == "=")
                {
                    idea.Sprite = cleanValue;
                    DebugLogger.Log("IdeasImporter", fileName, LogLevel.Info, $"Mapping Sprite: {cleanValue}");
                    return;
                }

                var rawLine = RawLineHelper.BuildAndLog(cleanKey, op, rawValue, depth);
                idea.Lines.Add(new ScriptedLine(rawLine, depth));
                DebugLogger.Log("IdeasImporter", fileName, LogLevel.Raw, $"Storing Line (D{depth}): {rawLine}");
            }
        }
    }
}