using System;
using System.Collections.Generic;

namespace Compiler
{

    public class Option
    {
        public RawLine rawLine;
    }

    public enum EventType
    {
        Country,
        News
    }

    // note: 'event' is a C# keyword, so the class is named 'Event'
    public class Event
    {
        public string name;
        public string desc;
        public string sprite;
        public string id;
        public EventType type;
        public RawLine rawLine;
        public List<Option> option = new List<Option>();
    }
    public class EventParser : BaseParser
    {
        // Receive preprocessed lines from the validator (BaseValidator.PreprocessedLine). Implementation left empty on purpose.
        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {
            this.SourceFileName = fileName;

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

                if (NewEvent == null)
                {
                    // look for event start
                    if (pl.TrimmedLine.StartsWith("country event", StringComparison.OrdinalIgnoreCase))
                    {
                        NewEvent = new Event() { type = EventType.Country };
                        NewEvent.id = pl.TrimmedLine.Substring("country event".Length).Trim();
                        isInsideEvent = EventType.Country.ToString();
                        continue;
                    }
                    else if (pl.TrimmedLine.StartsWith("news event", StringComparison.OrdinalIgnoreCase))
                    {
                        NewEvent = new Event() { type = EventType.News };
                        NewEvent.id = pl.TrimmedLine.Substring("news event".Length).Trim();
                        isInsideEvent = EventType.News.ToString();
                        continue;
                    }
                    else
                    {
                        Errors.Add(new ParsingError(fileName, pl.LineNumber, "Unknown or unsupported event type, only supported types are 'country event' and 'news event'"));
                        continue;
                    }
                }

                // If we're currently collecting option content
                if (inOption)
                {
                    if (pl.Depth > optionBaseDepth)
                    {
                        // inside option: append into the Option.rawLine (create if needed)
                        if (currentOption.rawLine == null)
                        {
                            currentOption.rawLine = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                        }
                        else
                        {
                            currentOption.rawLine.trimmedLine += "\n" + pl.TrimmedLine;
                        }
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
                if (pl.Depth == 1 && pl.TrimmedLine == "option")
                {
                    // start collecting option contents (depth 2 and beyond)
                    inOption = true;
                    optionBaseDepth = pl.Depth;
                    currentOption = new Option();
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
                    // depth > 1 but not currently in an option: append into Event.rawLine
                    if (NewEvent.rawLine == null)
                    {
                        NewEvent.rawLine = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                    }
                    else
                    {
                        NewEvent.rawLine.trimmedLine += "\n" + pl.TrimmedLine;
                    }
                    continue;
                }
            }

            // if file ends while still in an option, commit it
            if (inOption && currentOption != null && NewEvent != null)
            {
                NewEvent.option.Add(currentOption);
            }
        }
    }
}
