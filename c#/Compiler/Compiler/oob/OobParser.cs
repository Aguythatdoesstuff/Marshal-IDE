using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace Compiler
{
    public class OobParser : BaseParser
    {
        public DivisionMetadata Metadata { get; set; }

        public OobParser()
        {
            Compiler.Logging.Logger.LogComponent("Parser", "DivisionParser initialized.");
        }

        public class Division
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public string Abbreviation { get; set; } = string.Empty;
            public string DivisionModel { get; set; } = string.Empty;

            public List<string> DivisionTypes { get; set; } = new List<string>();
            public List<string> DivisionCategories { get; set; } = new List<string>();
            public List<RawLine> RequiredEquipment { get; set; } = new List<RawLine>();

            public List<RawLine> RawLines { get; set; } = new List<RawLine>();
        }

        public class ParsedDivisionFile
        {
            public string SourceFileName { get; set; } = string.Empty;
            public List<Division> Divisions { get; set; } = new List<Division>();
        }

        // Store the most recently parsed file for other components to use
        public ParsedDivisionFile LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {
            // Use ParsedDivisionFile to collect all units from this file and store the file name
            var parsedFile = new ParsedDivisionFile { SourceFileName = fileName };

            Division currentDivision = null;

            // Tracking for block structures
            bool insideDivisionTypes = false;
            int unitTypesDepth = -1;

            bool insideDivisionCategories = false;
            int unitCategoriesDepth = -1;

            bool insideRequiredEquipment = false;
            int requiredEquipmentDepth = -1;

            for (int i = 0; i < preprocessedLines.Count; i++)
            {
                var pl = preprocessedLines[i];
                if (Metadata == null)
                {
                    Errors.Add(new ParsingError(
                        fileName,
                        i,
                        $"ERROR! FAILED TO RETRIEVE ALL FILE METADATA FOR EQUIPMENT IN FILE: {filePath}."
                    ));
                    break;
                }

                // Detect headers at root (depth 0)
                if (pl.Depth == 0)
                {
                    insideDivisionTypes = false;
                    insideDivisionCategories = false;
                    insideRequiredEquipment = false;

                    if (pl.TrimmedLine.StartsWith("unit ", StringComparison.OrdinalIgnoreCase))
                    {
                        // Look up the metadata for this line using the line number
                        if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData))
                        {
                            var unit = new Division
                            {
                                Id = lineData.Id
                            };

                            parsedFile.Divisions.Add(unit);
                            currentDivision = unit;
                        }
                    }
                    // Depth 0 content that isn't recognized is invalid syntax
                    continue;
                }

                // Handle nested content (depth >= 1)
                if (currentDivision == null)
                {
                    Errors.Add(new ParsingError(
                        fileName,
                        pl.LineNumber,
                        $"Content at depth {pl.Depth} found without a parent 'unit <id>' definition."
                    ));
                    continue;
                }

                // ==========================================
                // UNIT TYPES BLOCK HANDLING
                // ==========================================
                if (pl.TrimmedLine.StartsWith("unit types", StringComparison.OrdinalIgnoreCase))
                {
                    insideDivisionTypes = true;
                    unitTypesDepth = pl.Depth;
                    insideDivisionCategories = false;
                    insideRequiredEquipment = false;

                    if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData) && lineData.MiscList != null && lineData.MiscList.Count > 0)
                    {
                        foreach (var tId in lineData.MiscList)
                        {
                            if (!currentDivision.DivisionTypes.Contains(tId))
                            {
                                currentDivision.DivisionTypes.Add(tId);
                            }
                        }
                    }
                    continue;
                }

                if (insideDivisionTypes && pl.Depth <= unitTypesDepth)
                {
                    insideDivisionTypes = false;
                }

                if (insideDivisionTypes && pl.Depth > unitTypesDepth)
                {
                    if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData) && !string.IsNullOrEmpty(lineData.Id))
                    {
                        if (!currentDivision.DivisionTypes.Contains(lineData.Id))
                        {
                            currentDivision.DivisionTypes.Add(lineData.Id);
                        }
                    }
                    else
                    {
                        string typeId = pl.TrimmedLine.Split(' ')[0].Trim();
                        if (!string.IsNullOrEmpty(typeId) && !currentDivision.DivisionTypes.Contains(typeId))
                        {
                            currentDivision.DivisionTypes.Add(typeId);
                        }
                    }
                    continue;
                }

                // ==========================================
                // UNIT CATEGORIES BLOCK HANDLING
                // ==========================================
                if (pl.TrimmedLine.StartsWith("unit categories", StringComparison.OrdinalIgnoreCase))
                {
                    insideDivisionCategories = true;
                    unitCategoriesDepth = pl.Depth;
                    insideDivisionTypes = false;
                    insideRequiredEquipment = false;

                    if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData) && lineData.MiscList != null && lineData.MiscList.Count > 0)
                    {
                        foreach (var cId in lineData.MiscList)
                        {
                            if (!currentDivision.DivisionCategories.Contains(cId))
                            {
                                currentDivision.DivisionCategories.Add(cId);
                            }
                        }
                    }
                    continue;
                }

                if (insideDivisionCategories && pl.Depth <= unitCategoriesDepth)
                {
                    insideDivisionCategories = false;
                }

                if (insideDivisionCategories && pl.Depth > unitCategoriesDepth)
                {
                    if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData) && !string.IsNullOrEmpty(lineData.Id))
                    {
                        if (!currentDivision.DivisionCategories.Contains(lineData.Id))
                        {
                            currentDivision.DivisionCategories.Add(lineData.Id);
                        }
                    }
                    else
                    {
                        string categoryId = pl.TrimmedLine.Split(' ')[0].Trim();
                        if (!string.IsNullOrEmpty(categoryId) && !currentDivision.DivisionCategories.Contains(categoryId))
                        {
                            currentDivision.DivisionCategories.Add(categoryId);
                        }
                    }
                    continue;
                }

                // ==========================================
                // REQUIRED EQUIPMENT BLOCK HANDLING (RAW LINES)
                // ==========================================
                if (pl.TrimmedLine.StartsWith("required equipment", StringComparison.OrdinalIgnoreCase))
                {
                    insideRequiredEquipment = true;
                    requiredEquipmentDepth = pl.Depth;
                    insideDivisionTypes = false;
                    insideDivisionCategories = false;
                    continue;
                }

                if (insideRequiredEquipment && pl.Depth <= requiredEquipmentDepth)
                {
                    insideRequiredEquipment = false;
                }

                if (insideRequiredEquipment && pl.Depth > requiredEquipmentDepth)
                {
                    currentDivision.RequiredEquipment.Add(new RawLine
                    {
                        trimmedLine = pl.TrimmedLine,
                        depth = pl.Depth
                    });
                    continue;
                }
                // ==========================================
                // PROPERTY MATCHING (NAME, DESC, ABBREVIATION, UNIT MODEL)
                // ==========================================
                if (pl.TrimmedLine.StartsWith("name ", StringComparison.OrdinalIgnoreCase))
                {
                    currentDivision.Name = GetQuotedContent(pl.TrimmedLine);
                    continue;
                }

                if (pl.TrimmedLine.StartsWith("desc ", StringComparison.OrdinalIgnoreCase))
                {
                    currentDivision.Desc = GetQuotedContent(pl.TrimmedLine);
                    continue;
                }

                if (pl.TrimmedLine.StartsWith("abbreviation ", StringComparison.OrdinalIgnoreCase))
                {
                    currentDivision.Abbreviation = GetQuotedContent(pl.TrimmedLine);
                    continue;
                }

                if (pl.TrimmedLine.StartsWith("unit model ", StringComparison.OrdinalIgnoreCase))
                {
                    currentDivision.DivisionModel = GetQuotedContent(pl.TrimmedLine);
                    continue;
                }

                // ==========================================
                // FALLBACK / GENERAL RAW LINES
                // ==========================================
                currentDivision.RawLines.Add(new RawLine
                {
                    trimmedLine = pl.TrimmedLine,
                    depth = pl.Depth
                });
            }

            // Save parsed result
            LastParsedFile = parsedFile;
        }
    }
}