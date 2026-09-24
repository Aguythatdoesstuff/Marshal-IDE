using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace Compiler
{
    public class UnitParser : BaseParser
    {
        public UnitMetadata Metadata { get; set; }

        public UnitParser()
        {
            Compiler.Logging.Logger.LogComponent("Parser", "UnitParser initialized.");
        }

        public class Unit
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public string Abbreviation { get; set; } = string.Empty;
            public string UnitModel { get; set; } = string.Empty;

            public List<string> UnitTypes { get; set; } = new List<string>();
            public List<string> UnitCategories { get; set; } = new List<string>();
            public List<RawLine> RequiredEquipment { get; set; } = new List<RawLine>();

            public List<RawLine> RawLines { get; set; } = new List<RawLine>();
        }

        public class ParsedUnitFile
        {
            public string SourceFileName { get; set; } = string.Empty;
            public List<Unit> Units { get; set; } = new List<Unit>();
        }

        // Store the most recently parsed file for other components to use
        public ParsedUnitFile LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {
            // Use ParsedUnitFile to collect all units from this file and store the file name
            var parsedFile = new ParsedUnitFile { SourceFileName = fileName };

            Unit currentUnit = null;

            // Tracking for block structures
            bool insideUnitTypes = false;
            int unitTypesDepth = -1;

            bool insideUnitCategories = false;
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
                    insideUnitTypes = false;
                    insideUnitCategories = false;
                    insideRequiredEquipment = false;

                    if (pl.TrimmedLine.StartsWith("unit ", StringComparison.OrdinalIgnoreCase))
                    {
                        // Look up the metadata for this line using the line number
                        if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData))
                        {
                            var unit = new Unit
                            {
                                Id = lineData.Id
                            };

                            parsedFile.Units.Add(unit);
                            currentUnit = unit;
                        }
                    }
                    // Depth 0 content that isn't recognized is invalid syntax
                    continue;
                }

                // Handle nested content (depth >= 1)
                if (currentUnit == null)
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
                    insideUnitTypes = true;
                    unitTypesDepth = pl.Depth;
                    insideUnitCategories = false;
                    insideRequiredEquipment = false;

                    if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData) && lineData.MiscList != null && lineData.MiscList.Count > 0)
                    {
                        foreach (var tId in lineData.MiscList)
                        {
                            if (!currentUnit.UnitTypes.Contains(tId))
                            {
                                currentUnit.UnitTypes.Add(tId);
                            }
                        }
                    }
                    continue;
                }

                if (insideUnitTypes && pl.Depth <= unitTypesDepth)
                {
                    insideUnitTypes = false;
                }

                if (insideUnitTypes && pl.Depth > unitTypesDepth)
                {
                    if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData) && !string.IsNullOrEmpty(lineData.Id))
                    {
                        if (!currentUnit.UnitTypes.Contains(lineData.Id))
                        {
                            currentUnit.UnitTypes.Add(lineData.Id);
                        }
                    }
                    else
                    {
                        string typeId = pl.TrimmedLine.Split(' ')[0].Trim();
                        if (!string.IsNullOrEmpty(typeId) && !currentUnit.UnitTypes.Contains(typeId))
                        {
                            currentUnit.UnitTypes.Add(typeId);
                        }
                    }
                    continue;
                }

                // ==========================================
                // UNIT CATEGORIES BLOCK HANDLING
                // ==========================================
                if (pl.TrimmedLine.StartsWith("unit categories", StringComparison.OrdinalIgnoreCase))
                {
                    insideUnitCategories = true;
                    unitCategoriesDepth = pl.Depth;
                    insideUnitTypes = false;
                    insideRequiredEquipment = false;

                    if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData) && lineData.MiscList != null && lineData.MiscList.Count > 0)
                    {
                        foreach (var cId in lineData.MiscList)
                        {
                            if (!currentUnit.UnitCategories.Contains(cId))
                            {
                                currentUnit.UnitCategories.Add(cId);
                            }
                        }
                    }
                    continue;
                }

                if (insideUnitCategories && pl.Depth <= unitCategoriesDepth)
                {
                    insideUnitCategories = false;
                }

                if (insideUnitCategories && pl.Depth > unitCategoriesDepth)
                {
                    if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData) && !string.IsNullOrEmpty(lineData.Id))
                    {
                        if (!currentUnit.UnitCategories.Contains(lineData.Id))
                        {
                            currentUnit.UnitCategories.Add(lineData.Id);
                        }
                    }
                    else
                    {
                        string categoryId = pl.TrimmedLine.Split(' ')[0].Trim();
                        if (!string.IsNullOrEmpty(categoryId) && !currentUnit.UnitCategories.Contains(categoryId))
                        {
                            currentUnit.UnitCategories.Add(categoryId);
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
                    insideUnitTypes = false;
                    insideUnitCategories = false;
                    continue;
                }

                if (insideRequiredEquipment && pl.Depth <= requiredEquipmentDepth)
                {
                    insideRequiredEquipment = false;
                }

                if (insideRequiredEquipment && pl.Depth > requiredEquipmentDepth)
                {
                    currentUnit.RequiredEquipment.Add(new RawLine
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
                    currentUnit.Name = GetQuotedContent(pl.TrimmedLine);
                    continue;
                }

                if (pl.TrimmedLine.StartsWith("desc ", StringComparison.OrdinalIgnoreCase))
                {
                    currentUnit.Desc = GetQuotedContent(pl.TrimmedLine);
                    continue;
                }

                if (pl.TrimmedLine.StartsWith("abbreviation ", StringComparison.OrdinalIgnoreCase))
                {
                    currentUnit.Abbreviation = GetQuotedContent(pl.TrimmedLine);
                    continue;
                }

                if (pl.TrimmedLine.StartsWith("unit model ", StringComparison.OrdinalIgnoreCase))
                {
                    currentUnit.UnitModel = GetQuotedContent(pl.TrimmedLine);
                    continue;
                }

                // ==========================================
                // FALLBACK / GENERAL RAW LINES
                // ==========================================
                currentUnit.RawLines.Add(new RawLine
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