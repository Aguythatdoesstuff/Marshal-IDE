using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace importer
{
    internal class ScriptedTriggerImporter : BaseImporter
    {
        public override string FolderSubPath => Path.Combine("common", "scripted_triggers");
        public override IEnumerable<string> FileExtensions => new[] { ".txt" };

        public class ScriptedLine
        {
            public string Content { get; set; }
            public int Depth { get; set; }
            public ScriptedLine(string content, int depth) { Content = content; Depth = depth; }
        }

        public class ScriptedTrigger
        {
            public string Name { get; set; }
            public string FileName { get; set; }
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        public List<ScriptedTrigger> Results { get; } = new List<ScriptedTrigger>();
        private ScriptedTrigger _currentTrigger;

        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            string cleanValue = value?.Trim() ?? "";

            if (depth == 0 && op == "=" && (string.IsNullOrEmpty(cleanValue) || cleanValue == "{"))
            {
                _currentTrigger = new ScriptedTrigger { Name = key, FileName = fileName };
                Results.Add(_currentTrigger);
                return;
            }

            // snapshot to avoid repeated field access and potential races
            var trigger = _currentTrigger;
            if (trigger == null) return;

            var rawLine = RawLineHelper.BuildAndLog(key, op, cleanValue, depth);
            trigger.Lines.Add(new ScriptedLine(rawLine, depth));
            DebugLogger.Log("DecisionsImport", fileName, LogLevel.Info, $"   -> Saved (Depth {depth}): {rawLine}");
            if (key == "}" && depth == 0) _currentTrigger = null;
        }
    }
}
