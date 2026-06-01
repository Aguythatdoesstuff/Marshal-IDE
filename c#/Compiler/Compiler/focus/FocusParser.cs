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

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {
            int count = 0;
            Tree currentTree = null;
            Focus currentFocus = null;

            // Active block tracking for 'on complete'
            string activeBlock = null; // "on complete"
            int blockBaseDepth = 0;
            RawLine currentBlockRaw = null;

            // Generic raw grouping when not inside a named block
            string genericOwner = null; // "tree" or "focus"
            RawLine currentGenericRaw = null;

            // require/prevents block state
            bool inRequireBlock = false;
            bool inPreventsBlock = false;
            int reqPrevBaseDepth = 0;

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
                    currentBlockRaw = null;
                    genericOwner = null;
                    currentGenericRaw = null;
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
                        // Group top-level entries inside the block into separate RawLine records
                        if (currentBlockRaw == null || pl.Depth <= currentBlockRaw.depth)
                        {
                            currentBlockRaw = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                            if (activeBlock == "on complete" && currentFocus != null)
                            {
                                currentFocus.onComplete.Add(currentBlockRaw);
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
                        // block ended; reset and reprocess this line
                        activeBlock = null;
                        currentBlockRaw = null;
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
                    currentGenericRaw = null;
                    continue;
                }

                // Handle simple tokens inside focus
                if (currentFocus != null && pl.TrimmedLine.StartsWith("name", StringComparison.OrdinalIgnoreCase))
                {
                    currentFocus.name = GetQuotedContent(pl.TrimmedLine.Substring("name".Length).Trim());
                    continue;
                }
                if (currentFocus != null && pl.TrimmedLine.StartsWith("desc", StringComparison.OrdinalIgnoreCase))
                {
                    currentFocus.desc = GetQuotedContent(pl.TrimmedLine.Substring("desc".Length).Trim());
                    continue;
                }
                if (currentFocus != null && pl.TrimmedLine.StartsWith("sprite", StringComparison.OrdinalIgnoreCase))
                {
                    currentFocus.sprite = GetQuotedContent(pl.TrimmedLine.Substring("sprite".Length).Trim());
                    continue;
                }

                // follow position of
                if (currentFocus != null && pl.TrimmedLine.StartsWith("follow position of", StringComparison.OrdinalIgnoreCase))
                {
                    string val = pl.TrimmedLine.Substring("follow position of".Length).Trim();
                    currentFocus.followPositionOf = val;
                    continue;
                }

                // position
                if (currentFocus != null && pl.TrimmedLine.StartsWith("position ", StringComparison.OrdinalIgnoreCase))
                {
                    string coordsPart = pl.TrimmedLine.Substring("position ".Length).Trim();
                    var coords = coordsPart.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (coords.Length >= 1) currentFocus.positionX = coords[0];
                    if (coords.Length >= 2) currentFocus.positionY = coords[1];
                    continue;
                }

                // require / prevents handling (may be inline or block)
                if (currentFocus != null && (pl.TrimmedLine.StartsWith("require", StringComparison.OrdinalIgnoreCase) || pl.TrimmedLine.StartsWith("prevents", StringComparison.OrdinalIgnoreCase)))
                {
                    bool isRequire = pl.TrimmedLine.StartsWith("require", StringComparison.OrdinalIgnoreCase);
                    string keyword = isRequire ? "require" : "prevents";
                    string remainder = pl.TrimmedLine.Length > keyword.Length ? pl.TrimmedLine.Substring(keyword.Length).Trim() : string.Empty;

                    if (!string.IsNullOrEmpty(remainder))
                    {
                        var tokens = remainder.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length == 1)
                        {
                            if (isRequire) currentFocus.requireIds.Add(tokens[0]);
                            else currentFocus.preventsIds.Add(tokens[0]);
                        }
                        else
                        {
                            // not a single id - store as raw
                            currentFocus.rawLines.Add(new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth });
                        }

                        // start block expectation if the header exists (still may have children)
                        reqPrevBaseDepth = pl.Depth;
                        // do not set inRequireBlock/inPreventsBlock here - only if block form (no remainder)
                        continue;
                    }
                    else
                    {
                        // block form: following indented id lines belong here
                        reqPrevBaseDepth = pl.Depth;
                        if (isRequire) inRequireBlock = true; else inPreventsBlock = true;
                        continue;
                    }
                }

                // on complete
                if (currentFocus != null && pl.TrimmedLine == "on complete")
                {
                    activeBlock = "on complete";
                    blockBaseDepth = pl.Depth;
                    currentBlockRaw = null;
                    continue;
                }

                // Any deeper content not recognized above should be treated as raw content
                if (pl.Depth >= 2)
                {
                    string owner = currentFocus == null ? "tree" : "focus";

                    if (genericOwner == null)
                    {
                        genericOwner = owner;
                        currentGenericRaw = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };
                        if (genericOwner == "tree") currentTree.rawLines.Add(currentGenericRaw);
                        else if (currentFocus != null) currentFocus.rawLines.Add(currentGenericRaw);
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
                        if (genericOwner == "tree") currentTree.rawLines.Add(currentGenericRaw);
                        else if (currentFocus != null) currentFocus.rawLines.Add(currentGenericRaw);
                        continue;
                    }
                    else
                    {
                        // dedent: close generic raw and reprocess this line
                        genericOwner = null;
                        currentGenericRaw = null;
                        i--;
                        continue;
                    }
                }

                // Any other unrecognized root-level lines are ignored for now
            }

            // No explicit exposure aside from Trees property
        }
    }
}
