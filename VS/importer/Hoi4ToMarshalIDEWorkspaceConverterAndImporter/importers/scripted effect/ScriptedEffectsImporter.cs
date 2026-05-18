using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace importer
{
    public class ScriptedEffectImporter : BaseImporter
    {
        public override string FolderSubPath => Path.Combine("common", "scripted_effects");
        public override IEnumerable<string> FileExtensions => new[] { ".txt" };

        public class ScriptedLine
        {
            public string Content { get; set; }
            public int Depth { get; set; }
            public ScriptedLine(string content, int depth) { Content = content; Depth = depth; }
        }

        public class ScriptedEffect
        {
            public string Name { get; set; }
            public string FileName { get; set; }
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        private readonly AsyncLocal<ScriptedEffect> _currentEffect = new AsyncLocal<ScriptedEffect>();
        public ConcurrentBag<ScriptedEffect> Results { get; } = new ConcurrentBag<ScriptedEffect>();

        private readonly object _sync = new object();
        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            lock (_sync)
            {
                string cleanValue = value?.Trim() ?? "";

                if (depth == 0 && op == "=" && (string.IsNullOrEmpty(cleanValue) || cleanValue == "{"))
                {
                    _currentEffect.Value = new ScriptedEffect { Name = key, FileName = fileName };
                    Results.Add(_currentEffect.Value);
                    DebugLogger.Log("GameRules", "", LogLevel.Info, $"Created Effect: {key} in {fileName}");
                    return;
                }

                var current = _currentEffect.Value;
                if (current != null)
                {
                    var rawLine = RawLineHelper.BuildAndLog(key, op, cleanValue, depth);
                    current.Lines.Add(new ScriptedLine(rawLine, depth));
                    DebugLogger.Log("GameRules", "", LogLevel.Info, $"-> Saved (Depth {depth}): {rawLine}");
                    if (key == "}" && depth == 0)
                    {
                        _currentEffect.Value = null;
                        return;
                    }
                }
            }
        }
    }
}
