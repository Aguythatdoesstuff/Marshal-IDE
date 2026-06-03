using System;
using System.Collections.Generic;

namespace Compiler
{
    public class Tree
    {
        public bool isDefault;
        public string id;
        public string countryTag; // optional when tree is country-specific: 'tree xxx for TAG'
        public List<RawLine> rawLines = new List<RawLine>();
        public List<Focus> focuses = new List<Focus>();
    }

    public class ParsedFocusFile
    {
        public string SourceFileName { get; set; } = string.Empty;
        public List<Tree> Trees { get; set; } = new List<Tree>();
    }

    public class Focus
    {
        public string id;
        public int timeValue;
        public string timeUnit;
        public string name;
        public string desc;
        public string sprite;
        public string followPositionOf;
        public string positionX;
        public string positionY;

        // require/prevents id lists (collect both inline single-id and block entries)
        public List<string> requireIds = new List<string>();
        public List<string> preventsIds = new List<string>();

        // on complete raw grouping and other raw content
        public List<RawLine> onComplete = new List<RawLine>();
        public List<RawLine> rawLines = new List<RawLine>();
    }

    public class FocusParser : BaseParser
    {
        public List<Tree> Trees { get; } = new List<Tree>();
        // Provide a last parsed snapshot if consumers need it (file name + trees)
        public ParsedFocusFile LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {
            int count = 0;
            Tree currentTree = null;
            Focus currentFocus = null;

            // Active block tracking for 'on complete'
            string activeBlock = null; // "on complete"
            int blockBaseDepth = 0;

            // Generic raw grouping when not inside a named block
            string genericOwner = null; // "tree" or "focus"
            int genericBaseDepth = 0;

            // require/prevents block state
            bool inRequireBlock = false;
            bool inPreventsBlock = false;
            int reqPrevBaseDepth = 0;

            // prepare parsed file container
            var parsedFile = new ParsedFocusFile { SourceFileName = fileName };

            Trees.Clear();
            for (int i = 0; i < preprocessedLines.Count; i++)
            {
                var pl = preprocessedLines[i];
                count++;

                // Tree start (depth 0)
                if (pl.Depth == 0 && (pl.TrimmedLine.StartsWith("default tree ") || pl.TrimmedLine.StartsWith("tree ")))
                {
                    currentTree = new Tree();
                    currentFocus = null;

                    if (pl.TrimmedLine.StartsWith("default tree "))
                    {
                        currentTree.isDefault = true;
                        currentTree.id = pl.TrimmedLine.Substring("default tree ".Length).Trim();
                    }
                    else // normal tree possibly with 'for TAG'
                    {
                        currentTree.isDefault = false;
                        string header = pl.TrimmedLine.Substring("tree ".Length).Trim();
                        int forIndex = header.LastIndexOf(" for ", StringComparison.OrdinalIgnoreCase);
                        if (forIndex == -1)
                        {
                            currentTree.id = header;
                        }
                        else
                        {
                            currentTree.id = header.Substring(0, forIndex).Trim();
                            currentTree.countryTag = header.Substring(forIndex + " for ".Length).Trim();
                        }
                    }

                    Trees.Add(currentTree);

                    // reset state
                    activeBlock = null;
                    blockBaseDepth = 0;
                    genericOwner = null;
                    genericBaseDepth = 0;
                    inRequireBlock = false;
                    inPreventsBlock = false;
                    continue;
                }

                if (currentTree == null)
                {
                    Errors.Add(new ParsingError(fileName, pl.LineNumber, "Found content outside of a tree"));
                    continue;
                }

                // If currently inside an active named block (on complete)
                if (!string.IsNullOrEmpty(activeBlock))
                {
                    if (pl.Depth > blockBaseDepth)
                    {
                        // Each raw line inside the block is its own RawLine record
                        if (activeBlock == "on complete" && currentFocus != null)
                        {
                            currentFocus.onComplete.Add(new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth });
                        }
                        continue;
                    }
                    else
                    {
                        // block ended; reset and reprocess this line
                        activeBlock = null;
                        i--;
                        continue;
                    }
                }

                // If inside require/prevents block collect identifiers
                if (inRequireBlock || inPreventsBlock)
                {
                    if (pl.Depth > reqPrevBaseDepth)
                    {
                        // Collect any indented lines as ids for require/prevents block
                        if (currentFocus != null)
                        {
                            if (inRequireBlock) currentFocus.requireIds.Add(pl.TrimmedLine);
                            if (inPreventsBlock) currentFocus.preventsIds.Add(pl.TrimmedLine);
                        }
                        continue;
                    }
                    else
                    {
                        // block ended; reset and reprocess
                        inRequireBlock = false;
                        inPreventsBlock = false;
                        i--;
                        continue;
                    }
                }

                // Focus start (depth 1)
                if (pl.Depth == 1 && pl.TrimmedLine.StartsWith("focus ", StringComparison.OrdinalIgnoreCase))
                {
                    currentFocus = new Focus();
                    currentTree.focuses.Add(currentFocus);

                    string content = pl.TrimmedLine.Substring("focus ".Length).Trim();
                    int takesIndex = content.IndexOf(" takes ", StringComparison.OrdinalIgnoreCase);
                    if (takesIndex == -1)
                    {
                        // store id anyway
                        currentFocus.id = content;
                    }
                    else
                    {
                        currentFocus.id = content.Substring(0, takesIndex).Trim();
                        string timingExpression = content.Substring(takesIndex + " takes ".Length).Trim();
                        var parts = timingExpression.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 1)
                        {
                            if (int.TryParse(parts[0], out var v)) currentFocus.timeValue = v;
                        }
                        if (parts.Length >= 2)
                        {
                            var unit = parts[1].ToLowerInvariant();
                            if (unit == "day") unit = "days";
                            else if (unit == "week") unit = "weeks";

                            // Only accept day(s) or week(s); otherwise record a parser error
                            if (unit != "days" && unit != "weeks")
                            {
                                Errors.Add(new ParsingError(fileName, pl.LineNumber, $"Unsupported time unit: '{parts[1]}' in focus '{currentFocus?.id}'. Allowed units: day, days, week, weeks."));
                            }

                            currentFocus.timeUnit = unit;
                        }
                    }

                    // reset generic raw grouping for this focus
                    genericOwner = null;
                    genericBaseDepth = 0;
                    continue;
                }

                // Handle simple tokens inside focus - always clear any generic grouping so they are not consumed
                if (currentFocus != null && pl.TrimmedLine.StartsWith("name", StringComparison.OrdinalIgnoreCase))
                {
                    genericOwner = null; genericBaseDepth = 0;
                    currentFocus.name = GetQuotedContent(pl.TrimmedLine.Substring("name".Length).Trim());
                    continue;
                }
                if (currentFocus != null && pl.TrimmedLine.StartsWith("desc", StringComparison.OrdinalIgnoreCase))
                {
                    genericOwner = null; genericBaseDepth = 0;
                    currentFocus.desc = GetQuotedContent(pl.TrimmedLine.Substring("desc".Length).Trim());
                    continue;
                }
                if (currentFocus != null && pl.TrimmedLine.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
                {
                    genericOwner = null; genericBaseDepth = 0;
                    currentFocus.sprite = GetQuotedContent(pl.TrimmedLine.Substring("sprite".Length).Trim());
                    continue;
                }

                // follow position of
                if (currentFocus != null && pl.TrimmedLine.StartsWith("follow position of", StringComparison.OrdinalIgnoreCase))
                {
                    genericOwner = null; genericBaseDepth = 0;
                    string val = pl.TrimmedLine.Substring("follow position of".Length).Trim();
                    currentFocus.followPositionOf = val;
                    continue;
                }

                // position
                if (currentFocus != null && pl.TrimmedLine.StartsWith("position ", StringComparison.OrdinalIgnoreCase))
                {
                    genericOwner = null; genericBaseDepth = 0;
                    string coordsPart = pl.TrimmedLine.Substring("position ".Length).Trim();
                    var coords = coordsPart.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (coords.Length >= 1) currentFocus.positionX = coords[0];
                    if (coords.Length >= 2) currentFocus.positionY = coords[1];
                    continue;
                }

                // require / prevents handling (may be inline or block)
                if (currentFocus != null && (pl.TrimmedLine.StartsWith("require", StringComparison.OrdinalIgnoreCase) || pl.TrimmedLine.StartsWith("prevents", StringComparison.OrdinalIgnoreCase)))
                {
                    // clear any generic raw grouping so these lines are not swallowed
                    genericOwner = null; genericBaseDepth = 0;

                    bool isRequire = pl.TrimmedLine.StartsWith("require", StringComparison.OrdinalIgnoreCase);
                    string keyword = isRequire ? "require" : "prevents";
                    string remainder = pl.TrimmedLine.Length > keyword.Length ? pl.TrimmedLine.Substring(keyword.Length).Trim() : string.Empty;

                    reqPrevBaseDepth = pl.Depth;

                    if (!string.IsNullOrEmpty(remainder))
                    {
                        var tokens = remainder.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length == 1)
                        {
                            if (isRequire) currentFocus.requireIds.Add(tokens[0]);
                            else currentFocus.preventsIds.Add(tokens[0]);

                            // If the next line is indented deeper, that means the header also has a block of children
                            if (i + 1 < preprocessedLines.Count && preprocessedLines[i + 1].Depth > pl.Depth)
                            {
                                if (isRequire) inRequireBlock = true; else inPreventsBlock = true;
                            }
                        }
                        else
                        {
                            // not a single id - store as raw
                            currentFocus.rawLines.Add(new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth });
                        }

                        continue;
                    }
                    else
                    {
                        // block form: following indented id lines belong here
                        if (isRequire) inRequireBlock = true; else inPreventsBlock = true;
                        continue;
                    }
                }

                // on complete
                if (currentFocus != null && pl.TrimmedLine == "on complete")
                {
                    genericOwner = null; genericBaseDepth = 0;
                    activeBlock = "on complete";
                    blockBaseDepth = pl.Depth;

                    continue;
                }

                // Any deeper or unrecognized content should be treated as raw content for grouping.
                // We intentionally allow tree-level headers at depth 1 (e.g. "initial_show_position = {")
                // and focus-level headers at depth 2 to start generic raw grouping.
                {
                    string owner = currentFocus == null ? "tree" : "focus";

                    // Minimum depth required to start a generic raw block depends on the current owner.
                    int minStartDepth = currentFocus == null ? 1 : 2;

                    if (pl.Depth >= minStartDepth)
                    {
                        // Start a new generic raw grouping if one is not active
                        if (genericOwner == null)
                        {
                            genericOwner = owner;
                            genericBaseDepth = pl.Depth;
                            var rl = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                            if (genericOwner == "tree") currentTree.rawLines.Add(rl);
                            else if (currentFocus != null) currentFocus.rawLines.Add(rl);
                            continue;
                        }

                        // If we're already collecting, append any line at or deeper than the base depth
                        if (pl.Depth >= genericBaseDepth)
                        {
                            var rl = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                            if (genericOwner == "tree") currentTree.rawLines.Add(rl);
                            else if (currentFocus != null) currentFocus.rawLines.Add(rl);
                            continue;
                        }
                        else
                        {
                            // dedent: close generic raw grouping and reprocess this line
                            genericOwner = null;
                            genericBaseDepth = 0;
                            i--;
                            continue;
                        }
                    }
                }

                // Any other unrecognized root-level lines are ignored for now
            }

            // Save a snapshot of parsed trees for external consumers
            parsedFile.Trees = new List<Tree>(Trees);
            LastParsedFile = parsedFile;
        }
    }
}
