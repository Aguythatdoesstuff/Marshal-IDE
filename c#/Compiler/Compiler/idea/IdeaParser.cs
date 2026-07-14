using System;
using System.Collections.Generic;

namespace Compiler
{

    public class IdeaModifier
    {
        public List<RawLine> rawLines { get; } = new List<RawLine>();
    }

    public class Idea
    {
        // dynamic idea type stored in 'type' string
        public string type;
        public string id;
        public string name;
        public string desc;
        public string sprite;
        public List<RawLine> rawLines { get; } = new List<RawLine>();
        public IdeaModifier modifier = new IdeaModifier();
    }

    public class ParsedIdeaFile
    {
        public string SourceFileName { get; set; } = string.Empty;
        public List<Idea> Ideas { get; set; } = new List<Idea>();
    }

    public class IdeaParser : BaseParser
    {
        public IdeaParser()
        {
            Compiler.Logging.Logger.LogComponent("Parser", "IdeaParser initialized.");
        }
        public List<Idea> Ideas { get; } = new List<Idea>();
        // Store the most recently parsed file for other components to use
        public ParsedIdeaFile LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {
            Idea currentIdea = null;
            Ideas.Clear();

            // active block tracking
            string activeBlock = null; // "modifier"
            int blockBaseDepth = 0;
            RawLine currentBlockRaw = null;

            // generic raw grouping when not inside a named block
            bool inGeneric = false;
            RawLine currentGenericRaw = null;

            for (int i = 0; i < preprocessedLines.Count; i++)
            {
                var pl = preprocessedLines[i];

                // Detect idea header at root - support dynamic types in the form '<type> idea <id>'
                if (pl.Depth == 0)
                {
                    var sepIndex = pl.TrimmedLine.IndexOf(" idea ", StringComparison.OrdinalIgnoreCase);
                    if (sepIndex >= 0)
                    {
                        if (currentIdea != null)
                        {
                            Ideas.Add(currentIdea);
                        }

                        currentIdea = new Idea();
                        currentIdea.rawLines.Clear();
                        currentIdea.modifier = new IdeaModifier();

                        // extract type and id
                        currentIdea.type = pl.TrimmedLine.Substring(0, sepIndex).Trim();
                        currentIdea.id = pl.TrimmedLine.Substring(sepIndex + " idea ".Length).Trim();

                        // reset states
                        activeBlock = null;
                        currentBlockRaw = null;
                        inGeneric = false;
                        currentGenericRaw = null;
                        continue;
                    }
                }

                if (currentIdea == null)
                {
                    Errors.Add(new ParsingError(fileName, pl.LineNumber, "Found content outside of an idea block."));
                    continue;
                }

                // If inside modifier block
                if (!string.IsNullOrEmpty(activeBlock))
                {
                    if (pl.Depth > blockBaseDepth)
                    {
                        if (currentBlockRaw == null || pl.Depth <= currentBlockRaw.depth)
                        {
                            currentBlockRaw = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                            if (activeBlock == "modifier")
                            {
                                currentIdea.modifier.rawLines.Add(currentBlockRaw);
                            }
                        }
                        else
                        {
                            currentBlockRaw.trimmedLine += "\n" + pl.TrimmedLine;
                        }
                        continue;
                    }
                    else
                    {
                        // block ended, reset and reprocess
                        activeBlock = null;
                        currentBlockRaw = null;
                        i--;
                        continue;
                    }
                }

                // name / desc / sprite handling
                if (pl.TrimmedLine.StartsWith("name", StringComparison.OrdinalIgnoreCase))
                {
                    currentIdea.name = GetQuotedContent(pl.TrimmedLine.Substring("name".Length).Trim());
                    continue;
                }
                if (pl.TrimmedLine.StartsWith("desc", StringComparison.OrdinalIgnoreCase))
                {
                    currentIdea.desc = GetQuotedContent(pl.TrimmedLine.Substring("desc".Length).Trim());
                    continue;
                }
                if (pl.TrimmedLine.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
                {
                    currentIdea.sprite = GetQuotedContent(pl.TrimmedLine.Substring("sprite".Length).Trim());
                    continue;
                }

                // modifier block start
                if (pl.TrimmedLine.Equals("modifier", StringComparison.OrdinalIgnoreCase))
                {
                    activeBlock = "modifier";
                    blockBaseDepth = pl.Depth;
                    currentBlockRaw = null;
                    continue;
                }

                // Any deeper content not recognized above should be treated as raw content
                if (pl.Depth >= 1)
                {
                    if (!inGeneric)
                    {
                        inGeneric = true;
                        currentGenericRaw = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                        currentIdea.rawLines.Add(currentGenericRaw);
                        continue;
                    }

                    if (pl.Depth > currentGenericRaw.depth)
                    {
                        currentGenericRaw.trimmedLine += "\n" + pl.TrimmedLine;
                        continue;
                    }
                    else if (pl.Depth == currentGenericRaw.depth)
                    {
                        currentGenericRaw = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                        currentIdea.rawLines.Add(currentGenericRaw);
                        continue;
                    }
                    else
                    {
                        inGeneric = false;
                        currentGenericRaw = null;
                        i--;
                        continue;
                    }
                }

                // All other lines ignored
            }

            if (currentIdea != null)
            {
                Ideas.Add(currentIdea);
            }

            // Build ParsedIdeaFile for consumers (compilers)
            var parsed = new ParsedIdeaFile { SourceFileName = fileName };
            parsed.Ideas.AddRange(Ideas);
            LastParsedFile = parsed;
        }
    }
}
