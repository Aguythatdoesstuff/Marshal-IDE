using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace importer
{
    public class DecisionCategoriesImporter : BaseImporter
    {
        // Target ONLY the categories subfolder
        public override string FolderSubPath => Path.Combine("common", "decisions", "categories");
        public override IEnumerable<string> FileExtensions => new[] { ".txt" };
        public override bool RequiresLocalisation => true;

        public class ScriptedLine
        {
            public string Content { get; set; }
            public int Depth { get; set; }
            public ScriptedLine(string content, int depth) { Content = content; Depth = depth; }
        }

        public class DecisionCategory
        {
            public string Id { get; set; }
            public string Sprite { get; set; }
            public string NameLocKey { get; set; }
            public string DescLocKey { get; set; }
            public string FileName { get; set; }
            public List<ScriptedLine> Lines { get; set; } = new List<ScriptedLine>();
        }

        private readonly AsyncLocal<DecisionCategory> _currentCategory = new AsyncLocal<DecisionCategory>();
        public ConcurrentBag<DecisionCategory> Results { get; } = new ConcurrentBag<DecisionCategory>();

        private readonly object _sync = new object();

        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            lock (_sync)
            {
                string cleanKey = key?.Trim() ?? "";
                string rawValue = value?.Trim() ?? "";
                string cleanValue = rawValue.Trim('\"', '\'');

                // 1. Start of a Category (Depth 0 in category files)
                if (depth == 0 && op == "=" && (string.IsNullOrEmpty(rawValue) || rawValue == "{"))
                {
                    _currentCategory.Value = new DecisionCategory
                    {
                        Id = cleanKey,
                        NameLocKey = cleanKey,
                        DescLocKey = cleanKey + "_desc",
                        FileName = fileName
                    };
                    DebugLogger.Log("CategoryImport", "", LogLevel.Info, $"FOUND CATEGORY: {cleanKey} (File: {fileName})");
                    return;
                }

                var category = _currentCategory.Value;
                if (category == null) return;

                // 2. Closing the Category
                if (depth == 0 && cleanKey == "}")
                {
                    DebugLogger.Log("CategoryImport", "", LogLevel.Info, $"FINISHED CATEGORY: {{category.Id}}. Total Lines Saved: {{category.Lines.Count");
                    Results.Add(category);
                    _currentCategory.Value = null;
                    return;
                }

                // 3. Property Mapping (Icon)
                if (depth == 1 && cleanKey == "icon" && op == "=")
                {
                    category.Sprite = cleanValue;
                    DebugLogger.Log("CategoryImport", "", LogLevel.Info, $"Mapping Sprite: {cleanValue} for Category {category.Id}");
                    return;
                }

                // Construct line content for the raw list
                string lineContent;
                if (cleanKey == "}")
                {
                    lineContent = "}";
                }
                else if (rawValue.Contains("{"))
                {
                    lineContent = $"{cleanKey} {op} {rawValue}";
                }
                else if (string.IsNullOrEmpty(rawValue) || rawValue == "{")
                {
                    lineContent = $"{cleanKey} {op} {{";
                }
                else
                {
                    lineContent = $"{cleanKey} {op} {rawValue}";
                }

                category.Lines.Add(new ScriptedLine(lineContent, depth));
                DebugLogger.Log("CategoryImport", "", LogLevel.Info, $"Storing Line (D{depth}): {lineContent}");
            }
        }
    }
}