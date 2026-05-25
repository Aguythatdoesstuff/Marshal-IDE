using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;

namespace importer
{
    public class OnActionsImporter : BaseImporter
    {
        public override string FolderSubPath => Path.Combine("common", "on_actions");
        public override IEnumerable<string> FileExtensions => new[] { ".txt" };

        public class ScriptedLine
        {
            public string Content { get; set; }
            public int Depth { get; set; }
            public ScriptedLine(string content, int depth) { Content = content; Depth = depth; }
        }

        public class OnAction
        {
            public string Name { get; set; }
            public string FileName { get; set; }
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        private readonly AsyncLocal<OnAction> _currentOnAction = new AsyncLocal<OnAction>();
        public ConcurrentBag<OnAction> Results { get; } = new ConcurrentBag<OnAction>();

        private readonly object _sync = new object();
        protected override void OnTokenFound(string key, string op, string value, int depth, string fileName, bool isOneLiner)
        {
            lock (_sync)
            {
                string cleanValue = value?.Trim() ?? "";

                if (depth == 0 && op == "=" && (string.IsNullOrEmpty(cleanValue) || cleanValue == "{"))
                {
                    if (key == "on_actions")
                    {
                        // start collecting everything inside the on_actions wrapper into a single OnAction
                        _currentOnAction.Value = new OnAction { Name = key, FileName = fileName };
                        Results.Add(_currentOnAction.Value);
                        DebugLogger.Log("OnActionsImporter", fileName, LogLevel.Info, $"Started collecting on_actions from {fileName}");
                    }
                    else
                    {
                        DebugLogger.Log("OnActionsImporter", fileName, LogLevel.Warning, $"on_actions wrapper missing, saw: {key} {op} {cleanValue}");
                    }
                    return;
                }

                var current = _currentOnAction.Value;
                if (current != null)
                {
                    string lineContent;

                    var rawLine = RawLineHelper.BuildAndLog(key, op, cleanValue, depth);
                    if (key == "}" && depth == 0)
                    {
                        // end of the wrapper; stop collecting and do not save the wrapper brace
                        _currentOnAction.Value = null;
                        DebugLogger.Log("OnActionsImporter", fileName, LogLevel.Info, $"Finished collecting on_actions from {fileName}");
                        return;
                    }

                    current.Lines.Add(new ScriptedLine(rawLine, depth));
                }
                
            }
        }
    }
}
