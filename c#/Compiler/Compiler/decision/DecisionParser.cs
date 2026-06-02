using System;
using System.Collections.Generic;

namespace Compiler
{
    public class Category
    {
        public string id;
        public string name;
        public string desc;
        public string sprite;
        public long priority; // priority must be an integer (validator enforces numeric content)
        public List<RawLine> allowed = new List<RawLine>();
        public List<RawLine> available = new List<RawLine>();
        public List<RawLine> rawLines = new List<RawLine>();
        public List<Decision> decisions = new List<Decision>();
    }

    public class Decision
    {
        public string id;
        public string name;
        public string desc;
        public string sprite;
        public long priority;
        public int cost;
        public List<RawLine> allowed = new List<RawLine>();
        public List<RawLine> available = new List<RawLine>();
        public List<RawLine> onClick = new List<RawLine>();
        public List<RawLine> rawLines = new List<RawLine>();
    }

    public class DecisionParser : BaseParser
    {
        // Parsed results are exposed so callers can examine the AST after parsing.
        public List<Category> Categories { get; } = new List<Category>();

        // Provide a lightweight parsed file wrapper similar to Event/Idea parsers
        public class ParsedDecisionFile
        {
            public string SourceFileName { get; set; } = string.Empty;
            public List<Category> Categories { get; set; } = new List<Category>();
        }

        // Store the most recently parsed file for other components to use
        public ParsedDecisionFile LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {

            //Console.WriteLine($"[PARSER] Parsing file: {filePath}");
            int count = 0;
            Category currentCategory = null;
            Decision currentDecision = null;

            // active block tracking
            string activeBlock = null; // "allowed", "available", "on click"
            string activeBlockOwner = null; // "category" or "decision"
            int blockBaseDepth = 0;
            // Raw lines inside named blocks are recorded as individual RawLine entries

            // generic raw grouping when not inside a named block
            string genericOwner = null; // "category" or "decision"
            int genericBaseDepth = -1;

            for (int i = 0; i < preprocessedLines.Count; i++)
            {
                var pl = preprocessedLines[i];
                count++;
                //Console.WriteLine($"[PARSER] Line {pl.LineNumber} Depth {pl.Depth}: {pl.TrimmedLine}");

                // Start of a category
                if (pl.Depth == 0 && pl.TrimmedLine.StartsWith("category ", StringComparison.OrdinalIgnoreCase))
                {
                    // commit any previously open category
                    if (currentCategory != null)
                    {
                        Categories.Add(currentCategory);
                    }

                    currentCategory = new Category();
                    currentCategory.id = pl.TrimmedLine.Substring("category ".Length).Trim();
                    currentDecision = null;
                    // reset any block state
                    activeBlock = null;
                    genericOwner = null;
                    continue;
                }

                // If we don't have a category yet, skip / record error
                if (currentCategory == null)
                {
                    Errors.Add(new ParsingError(fileName, pl.LineNumber, "Found content outside of a category"));
                    continue;
                }

                // If currently inside an active named block (allowed/available/on click)
                if (!string.IsNullOrEmpty(activeBlock))
                {
                    if (pl.Depth > blockBaseDepth)
                    {
                        // Record each physical line as its own RawLine so indentation is preserved
                        var rl = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                        if (activeBlockOwner == "category")
                        {
                            if (activeBlock == "allowed") currentCategory.allowed.Add(rl);
                            else if (activeBlock == "available") currentCategory.available.Add(rl);
                            else if (activeBlock == "on click")
                            {
                                Errors.Add(new ParsingError(fileName, pl.LineNumber, "'on click' block is not allowed at category level."));
                            }
                        }
                        else if (activeBlockOwner == "decision" && currentDecision != null)
                        {
                            if (activeBlock == "available") currentDecision.available.Add(rl);
                            else if (activeBlock == "on click") currentDecision.onClick.Add(rl);
                            else if (activeBlock == "allowed") currentDecision.allowed.Add(rl);
                        }
                        continue;
                    }
                    else
                    {
                        // block ended; reset and reprocess this line
                        activeBlock = null;
                        activeBlockOwner = null;
                        i--;
                        continue;
                    }
                }

                // Decision start
                if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("decision ", StringComparison.OrdinalIgnoreCase))
                {
                    currentDecision = new Decision();
                    currentDecision.id = pl.TrimmedLine.Substring("decision ".Length).Trim();
                    currentCategory.decisions.Add(currentDecision);
                    // reset generic raw grouping for decision
                    genericOwner = null;
                    continue;
                }

                // Handle name/desc/sprite at category (depth 1) or decision (depth >=2)
                if (pl.TrimmedLine.StartsWith("name", StringComparison.OrdinalIgnoreCase))
                {
                    var val = GetQuotedContent(pl.TrimmedLine.Substring("name".Length).Trim());
                    if (pl.Depth == 1)
                    {
                        currentCategory.name = val;
                    }
                    else
                    {
                        if (currentDecision != null) currentDecision.name = val;
                    }
                    continue;
                }
                if (pl.TrimmedLine.StartsWith("desc", StringComparison.OrdinalIgnoreCase))
                {
                    var val = GetQuotedContent(pl.TrimmedLine.Substring("desc".Length).Trim());
                    if (pl.Depth == 1)
                    {
                        currentCategory.desc = val;
                    }
                    else
                    {
                        if (currentDecision != null) currentDecision.desc = val;
                    }
                    continue;
                }
                if (pl.TrimmedLine.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
                {
                    var val = GetQuotedContent(pl.TrimmedLine.Substring("sprite".Length).Trim());
                    if (pl.Depth == 1)
                    {
                        currentCategory.sprite = val;
                    }
                    else
                    {
                        if (currentDecision != null) currentDecision.sprite = val;
                    }
                    continue;
                }

                // priority (may be large -> keep as string)
                if (pl.TrimmedLine.StartsWith("priority", StringComparison.OrdinalIgnoreCase))
                {
                    var val = pl.TrimmedLine.Substring("priority".Length).Trim();
                if (long.TryParse(val, out var p))
                    {
                        if (pl.Depth == 1)
                        {
                            currentCategory.priority = p;
                        }
                        else
                        {
                            if (currentDecision != null) currentDecision.priority = p;
                        }
                    }
                    else
                    {
                        Errors.Add(new ParsingError(fileName, pl.LineNumber, $"Invalid integer value for priority: '{val}'"));
                        if (pl.Depth == 1)
                        {
                            currentCategory.priority = 0;
                        }
                        else
                        {
                            if (currentDecision != null) currentDecision.priority = 0;
                        }
                    }
                    continue;
                }

                // cost (decision only)
                if (pl.TrimmedLine.StartsWith("cost", StringComparison.OrdinalIgnoreCase))
                {
                    var val = pl.TrimmedLine.Substring("cost".Length).Trim();
                    if (currentDecision != null)
                    {
                        if (int.TryParse(val, out var cost))
                        {
                            currentDecision.cost = cost;
                        }
                        else
                        {
                            Errors.Add(new ParsingError(fileName, pl.LineNumber, $"Could not parse cost value '{val}' as int; falling back to 0."));
                            currentDecision.cost = 0;
                        }
                    }
                    continue;
                }

                // Block starters: allowed, available, on click
                if (pl.TrimmedLine == "allowed" || pl.TrimmedLine == "available" || pl.TrimmedLine == "on click")
                {
                    activeBlock = pl.TrimmedLine;
                    blockBaseDepth = pl.Depth;
                    // determine owner: depth 1 -> category, depth >=2 -> decision
                    activeBlockOwner = pl.Depth == 1 ? "category" : "decision";
                    continue;
                }

                // Any deeper content not recognized above should be treated as raw content
                if (pl.Depth >= 2)
                {
                    string owner = (pl.Depth == 1 || currentDecision == null) ? "category" : "decision";

                    if (genericOwner == null)
                    {
                        genericOwner = owner;
                        genericBaseDepth = pl.Depth; // record the starting depth for this generic block
                    }

                    if (pl.Depth >= genericBaseDepth)
                    {
                        var rl = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                        if (genericOwner == "category") currentCategory.rawLines.Add(rl);
                        else if (currentDecision != null) currentDecision.rawLines.Add(rl);
                        continue;
                    }
                    else
                    {
                        // dedent: close generic raw and reprocess
                        genericOwner = null;
                        genericBaseDepth = -1;
                        i--;
                        continue;
                    }
                }

                // Any other unrecognized root-level lines are ignored for now
            }

            // Parsing complete: expose categories if any
            if (currentCategory != null)
            {
                Categories.Add(currentCategory);
            }

            // Build a parsed file wrapper for consumers (compilers)
            var parsed = new ParsedDecisionFile { SourceFileName = fileName };
            parsed.Categories.AddRange(Categories);
            LastParsedFile = parsed;
        }
    }
}
