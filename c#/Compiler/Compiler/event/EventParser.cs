using System;
using System.Collections.Generic;
using System.Linq;

namespace Compiler
{

    public class Option
    {
        // store a sequence of raw lines for the option (may contain grouped entries)
        public List<RawLine> rawLine = new List<RawLine>();
        public string name;
    }

    public enum EventType
    {
        Country,
        News
    }

    public class ParsedEventFile
    {
        public string SourceFileName { get; set; } = string.Empty;

        public List<Event> Events { get; set; } = new List<Event>();
    }

    // note: 'event' is a C# keyword, so the class is named 'Event'
    public class Event
    {
        public string name;
        public string desc;
        public string sprite;
        public string id;
        public EventType type;
        // store a sequence of raw lines for the event body (may contain grouped entries)
        public List<RawLine> rawLine = new List<RawLine>();
        public List<Option> option = new List<Option>();
    }
    public class EventParser : BaseParser
    {
        public EventParser()
        {
            Compiler.Logging.Logger.LogComponent("Parser", "EventParser initialized.");
        }
        // store the most recently parsed file for other components to use
        public ParsedEventFile LastParsedFile { get; private set; }

        // Receive preprocessed lines from the validator (BaseValidator.PreprocessedLine). Implementation left empty on purpose.
        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {

            // Use ParsedEventFile to collect all events from this file and store the file name
            var parsedFile = new ParsedEventFile { SourceFileName = fileName };

            //Console.WriteLine($"[PARSER] Parsing file: {filePath}");
            int count = 0;
            string isInsideEvent = null;
            Event NewEvent = null;
            bool inOption = false;
            Option currentOption = null;
            int optionBaseDepth = 0;

            for (int i = 0; i < preprocessedLines.Count; i++)
            {
                var pl = preprocessedLines[i];
                count++;
                //Console.WriteLine($"[PARSER] Line {pl.LineNumber} Depth {pl.Depth}: {pl.TrimmedLine}");

                // Always detect the start of a new event. If another event was open, commit it first.
                if (pl.TrimmedLine.StartsWith("country event", StringComparison.OrdinalIgnoreCase))
                {
                    if (NewEvent != null) parsedFile.Events.Add(NewEvent);
                    NewEvent = new Event() { type = EventType.Country };
                    NewEvent.id = pl.TrimmedLine.Substring("country event".Length).Trim();
                    isInsideEvent = EventType.Country.ToString();
                    continue;
                }
                else if (pl.TrimmedLine.StartsWith("news event", StringComparison.OrdinalIgnoreCase))
                {
                    if (NewEvent != null) parsedFile.Events.Add(NewEvent);
                    NewEvent = new Event() { type = EventType.News };
                    NewEvent.id = pl.TrimmedLine.Substring("news event".Length).Trim();
                    isInsideEvent = EventType.News.ToString();
                    continue;
                }

                if (NewEvent == null)
                {
                    Errors.Add(new ParsingError(fileName, pl.LineNumber, "Unknown or unsupported event type, only supported types are 'country event' and 'news event'"));
                    continue;
                }

                // If we're currently collecting option content
                if (inOption)
                {
                    if (pl.Depth > optionBaseDepth)
                    {
                        // inside option: store each physical line as its own RawLine so depths are preserved
                        var rl = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                        currentOption.rawLine.Add(rl);
                        continue;
                    }
                    else
                    {
                        // option ended; commit and reprocess this line as a root-level line
                        NewEvent.option.Add(currentOption);
                        currentOption = null;
                        inOption = false;
                        // reprocess current line
                        i--;
                        continue;
                    }
                }

                // not in option: handle root-level depth 1 entries
                if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("option", StringComparison.OrdinalIgnoreCase))
                {
                    // 'option' may include a quoted name on the same line: option "Join the Revolution"
                    string remainder = pl.TrimmedLine.Length > "option".Length ? pl.TrimmedLine.Substring("option".Length).Trim() : string.Empty;
                    currentOption = new Option();
                    if (!string.IsNullOrEmpty(remainder))
                    {
                        currentOption.name = GetQuotedContent(remainder);
                    }

                    optionBaseDepth = pl.Depth;

                    // If the option has a block following (deeper depth), begin collecting; otherwise commit immediately
                    if (i + 1 < preprocessedLines.Count && preprocessedLines[i + 1].Depth > optionBaseDepth)
                    {
                        inOption = true;
                    }
                    else
                    {
                        NewEvent.option.Add(currentOption);
                        currentOption = null;
                    }

                    continue;
                }
                else if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("name", StringComparison.OrdinalIgnoreCase))
                {
                    NewEvent.name = GetQuotedContent(pl.TrimmedLine.Substring("name".Length).Trim());
                    continue;
                }
                else if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("desc", StringComparison.OrdinalIgnoreCase))
                {
                    NewEvent.desc = GetQuotedContent(pl.TrimmedLine.Substring("desc".Length).Trim());
                    continue;
                }
                else if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
                {
                    NewEvent.sprite = GetQuotedContent(pl.TrimmedLine.Substring("sprite".Length).Trim());
                    continue;
                }
                else
                {
                    // depth > 1 but not currently in an option: record each physical raw line so depths are preserved
                    var rl = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                    NewEvent.rawLine.Add(rl);
                    continue;
                }
            }
            // if file ends while still in an option, commit it
            if (inOption && currentOption != null && NewEvent != null)
            {
                NewEvent.option.Add(currentOption);
            }

            // commit last open event
            if (NewEvent != null)
            {
                parsedFile.Events.Add(NewEvent);
            }

            // save parsed result
            LastParsedFile = parsedFile;
        }
    }
}
