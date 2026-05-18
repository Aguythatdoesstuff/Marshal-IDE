using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using static importer.EventsImporter;

namespace importer
{
    public class EventsImporter : BaseImporter
    {
        public override string FolderSubPath => Path.Combine("events");
        public override IEnumerable<string> FileExtensions => new[] { ".txt" };

        // We still wait for Loc so the Compiler has a guarantee the dictionary is full
        public override bool RequiresLocalisation => true;

        public class ScriptedLine
        {
            public string Content { get; set; }
            public int Depth { get; set; }
            public ScriptedLine(string content, int depth) { Content = content; Depth = depth; }
        }

        public class EventOption
        {
            public string NameLocKey { get; set; } // The value from 'text = ...'
            public List<ScriptedLine> Lines { get; } = new List<ScriptedLine>();
        }

        public class Event
        {
            public string Id { get; set; } // e.g., enable_france
            public string Type { get; set; } // e.g., enable_france
            public string Sprite { get; set; } // The event sprite
            public string NameLocKey { get; set; } // e.g., RULE_ENABLE_FRANCE
            public string DescLocKey { get; set; } // e.g., GROUP_ENABLE_FRANCE
            public string FileName { get; set; }
            public List<EventOption> Options { get; } = new List<EventOption>();
        }

        private readonly AsyncLocal<Event> _currentEvent = new AsyncLocal<Event>();
        private readonly AsyncLocal<EventOption> _currentOption = new AsyncLocal<EventOption>();
        public ConcurrentBag<Event> Results { get; } = new ConcurrentBag<Event>();

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
                // 1. Root Level: Start of event (Depth 0)
                if (depth == 0 && op == "=" && (string.IsNullOrEmpty(rawValueTrimmed) || rawValueTrimmed == "{"))
                {
                    string eventType = cleanKey.Replace("_event", "");

                    _currentEvent.Value = new Event { Type = eventType, FileName = fileName };
                    DebugLogger.Log("EventsImporter", fileName, LogLevel.Info, $"Started event type: {eventType}");
                    return;
                }

                var eventValue = _currentEvent.Value;
                if (eventValue == null) return;

                // 2. Root Level: End of event (Depth 0)
                // Because depth is decremented BEFORE the token is passed, the root '}' is at Depth 0
                if (depth == 0 && cleanKey == "}")
                {
                    Results.Add(eventValue);
                    DebugLogger.Log("EventsImporter", fileName, LogLevel.Info, $"Finished event: {eventValue.Type} (Saved {eventValue.Options.Count} options) with the id: {eventValue.Id}");
                    _currentEvent.Value = null;
                    return;
                }

                var option = _currentOption.Value;

                // 3. We are inside the eventValue, but NOT inside an option yet
                if (option == null)
                {
                    if (depth == 1)
                    {
                    if (cleanKey == "title" && op == "=")
                        {
                            eventValue.NameLocKey = cleanValue;
                            DebugLogger.Log("EventsImporter", fileName, LogLevel.Info, $"event Name Loc: {cleanValue}");
                            return;
                        }
                        if (cleanKey == "picture" && op == "=")
                        {
                            eventValue.Sprite = cleanValue;
                            DebugLogger.Log("EventsImporter", fileName, LogLevel.Info, $"event picture: {cleanValue}");
                            return;
                        }
                        if (cleanKey == "id" && op == "=")
                        {
                            eventValue.Id = cleanValue;
                            DebugLogger.Log("EventsImporter", fileName, LogLevel.Info, $"event id: {cleanValue}");
                            return;
                        }
                        if (cleanKey == "desc" && op == "=")
                        {
                            eventValue.DescLocKey = cleanValue;
                            DebugLogger.Log("EventsImporter", fileName, LogLevel.Info, $"event desc Loc: {cleanValue}");
                            return;
                        }
                        if (cleanKey == "is_triggered_only" && op == "=" && cleanValue == "yes")
                        {
                            return; // junk data we dont need but hoi4 needs
                        }
                        // Inside section 3 (if (depth == 1))
                        if (cleanKey == "option" && op == "=")
                        {
                            // This is the trigger that "opens" the option block
                            _currentOption.Value = new EventOption();
                            DebugLogger.Log("EventsImporter", fileName, LogLevel.Info, "Opening Option block...");
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
                        eventValue.Options.Add(option);
                        DebugLogger.Log("EventsImporter", fileName, LogLevel.Info, $"Closed Option: {option.NameLocKey ?? "unnamed"}");
                        _currentOption.Value = null;
                        return;
                    }

                    if (depth == 2)
                    {
                        if (cleanKey == "name" && op == "=")
                        {
                            option.NameLocKey = cleanValue;
                            DebugLogger.Log("EventsImporter", fileName, LogLevel.Info, $"Option Loc Key identified: {cleanValue}");
                            return;
                        }
                    }

                    // 5. Construct and save the raw lines (just like OnActionsImporter)
                    var rawLine = RawLineHelper.BuildAndLog(cleanKey, op, rawValueTrimmed, depth);
                    option.Lines.Add(new ScriptedLine(rawLine, depth));
                    DebugLogger.Log("EventsImporter", fileName, LogLevel.Raw, $"Saved Line (D{depth}): {rawLine}");
                }
            }
        }
    }
}