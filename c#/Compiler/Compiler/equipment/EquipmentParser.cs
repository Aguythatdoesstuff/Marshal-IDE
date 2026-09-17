using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Compiler
{
    public class EquipmentParser : BaseParser
       {
        public EquipmentMetadata Metadata { get; set; }

        public EquipmentParser()
        {
            Compiler.Logging.Logger.LogComponent("Parser", "EquipmentParser initialized.");
        }
        public class LocalizedText
        {
            public string Value { get; set; } = string.Empty;
            public string? CountryTag { get; set; } // Optional country tag for localizing the equipment for a specific country
        }

        public class Equipment
        {
            public string Id { get; set; } = string.Empty;
            public int AvailableFromYear { get; set; }
            public string Picture { get; set; } = string.Empty;

            public List<LocalizedText> Names { get; set; } = new List<LocalizedText>();
            public List<LocalizedText> Descriptions { get; set; } = new List<LocalizedText>();
            public List<LocalizedText> ShortDescriptions { get; set; } = new List<LocalizedText>();

            public List<RawLine> RawLines { get; set; } = new List<RawLine>();

            // Represents the upgraded/better version nested inside this equipment
            public Equipment? UpgradedEquipment { get; set; }
        }

        public class Archetype
        {
            public string Id { get; set; } = string.Empty;
            public int AvailableFromYear { get; set; }
            public string Picture { get; set; } = string.Empty;

            public List<string> ForUnits { get; set; } = new List<string>(); // Unit IDs

            public List<LocalizedText> Names { get; set; } = new List<LocalizedText>();
            public List<LocalizedText> Descriptions { get; set; } = new List<LocalizedText>();
            public List<LocalizedText> ShortDescriptions { get; set; } = new List<LocalizedText>();

            public List<RawLine> RawLines { get; set; } = new List<RawLine>();

            // The base/first usable equipment iteration for this archetype
            public Equipment? BaseEquipment { get; set; }
        }

        public class ParsedEquipmentFile
        {
            public string SourceFileName { get; set; } = string.Empty;
            public List<Archetype> Archetypes { get; set; } = new List<Archetype>();
        }
        // store the most recently parsed file for other components to use
        public ParsedEquipmentFile LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {

            // Use ParsedEquipmentFile to collect all archetypes from this file and store the file name
            var parsedFile = new ParsedEquipmentFile { SourceFileName = fileName };

            // Track equipment by depth for proper nesting hierarchy
            var equipmentByDepth = new Dictionary<int, Equipment>();
            Archetype currentArchetype = null;

            // Track if we are currently reading unit IDs under a "for units" block
            bool insideForUnitsBlock = false;
            int forUnitsDepth = -1;

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
                    insideForUnitsBlock = false;
                    if (pl.TrimmedLine.StartsWith("define type ", StringComparison.OrdinalIgnoreCase))
                    {
                        // Look up the metadata for this line using the line number
                        if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData))
                        {
                            // Create a new Archetype with the ID and year from the metadata
                            var archetype = new Archetype
                            {
                                Id = lineData.Id,
                                AvailableFromYear = int.Parse(lineData.Misc)
                            };

                            parsedFile.Archetypes.Add(archetype);
                            currentArchetype = archetype;
                            equipmentByDepth.Clear(); // Reset equipment tracking for new archetype
                        }
                    }
                    // Depth 0 content that isn't recognized is invalid syntax
                    continue;
                }

                // Handle nested content (depth >= 1)
                if (currentArchetype == null)
                {
                    Errors.Add(new ParsingError(
                        fileName,
                        pl.LineNumber,
                        $"Content at depth {pl.Depth} found without a parent 'define type <id> available from <year>' archetype."
                    ));
                    continue;
                }

                if (pl.TrimmedLine.StartsWith("sprite ", StringComparison.OrdinalIgnoreCase))
                {
                    string gfxDefinition = GetQuotedContent(pl.TrimmedLine);

                    // If depth is 1, assign directly to the archetype
                    if (pl.Depth == 1)
                    {
                        currentArchetype.Picture = gfxDefinition;
                    }
                    // If depth is >= 2, assign to the currently tracked equipment at that depth level
                    else if (pl.Depth >= 2)
                    {
                        int targetEquipmentDepth = equipmentByDepth.Keys.Where(k => k < pl.Depth).DefaultIfEmpty(-1).Max();
                        if (targetEquipmentDepth != -1 && equipmentByDepth.TryGetValue(targetEquipmentDepth, out var equipment))
                        {
                            equipment.Picture = gfxDefinition;
                        }
                    }
                    continue;
                }

                // ==========================================
                // 1. FOR UNITS BLOCK HANDLING
                // ==========================================
                if (pl.TrimmedLine.StartsWith("for units", StringComparison.OrdinalIgnoreCase))
                {
                    insideForUnitsBlock = true;
                    forUnitsDepth = pl.Depth;

                    if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData) && lineData.MiscList.Count > 0)
                    {
                        foreach (var uId in lineData.MiscList)
                        {
                            if (!currentArchetype.ForUnits.Contains(uId))
                            {
                                currentArchetype.ForUnits.Add(uId);
                            }
                        }
                    }
                    continue;
                }

                if (insideForUnitsBlock && pl.Depth <= forUnitsDepth)
                {
                    insideForUnitsBlock = false;
                }

                if (insideForUnitsBlock && pl.Depth > forUnitsDepth && !pl.TrimmedLine.StartsWith("equipment ", StringComparison.OrdinalIgnoreCase))
                {
                    if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData) && !string.IsNullOrEmpty(lineData.Id))
                    {
                        if (!currentArchetype.ForUnits.Contains(lineData.Id))
                        {
                            currentArchetype.ForUnits.Add(lineData.Id);
                        }
                    }
                    else
                    {
                        string unitId = pl.TrimmedLine.Split(' ')[0].Trim();
                        if (!string.IsNullOrEmpty(unitId) && !currentArchetype.ForUnits.Contains(unitId))
                        {
                            currentArchetype.ForUnits.Add(unitId);
                        }
                    }
                    continue;
                }

                // Handle nested equipment definitions
                if (pl.TrimmedLine.StartsWith("equipment ", StringComparison.OrdinalIgnoreCase))
                {
                    insideForUnitsBlock = false;
                    // Look up the metadata for this line using the line number
                    if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData))
                    {
                        // Create a new Equipment with the ID and year from the metadata
                        var equipment = new Equipment
                        {
                            Id = lineData.Id,
                            AvailableFromYear = int.Parse(lineData.Misc)
                        };

                        if (equipmentByDepth.Count == 0)
                        {
                            // This is the first equipment (base equipment)
                            currentArchetype.BaseEquipment = equipment;
                            equipmentByDepth[pl.Depth] = equipment;
                        }
                        else
                        {
                            // Find the shallower equipment to nest this equipment into
                            int shallowerDepth = equipmentByDepth.Keys.Where(k => k < pl.Depth).DefaultIfEmpty(-1).Max();

                            if (shallowerDepth != -1)
                            {
                                // Add this equipment as an upgrade to the shallower equipment
                                equipmentByDepth[shallowerDepth].UpgradedEquipment = equipment;
                            }
                            else
                            {
                                // No shallower equipment found, add as base
                                currentArchetype.BaseEquipment = equipment;
                            }

                            // Update tracking for shallower depths when we encounter greater depth
                            var keysToRemove = equipmentByDepth.Keys.Where(k => k >= pl.Depth).ToList();
                            foreach (var key in keysToRemove)
                            {
                                equipmentByDepth.Remove(key);
                            }
                            equipmentByDepth[pl.Depth] = equipment;
                        }
                    }
                    continue;
                }

                // Handle localized text (name for, desc for, short desc for) with country tags
                if (pl.TrimmedLine.StartsWith("name for ", StringComparison.OrdinalIgnoreCase) ||
                    pl.TrimmedLine.StartsWith("desc for ", StringComparison.OrdinalIgnoreCase) ||
                    pl.TrimmedLine.StartsWith("short desc for ", StringComparison.OrdinalIgnoreCase))
                {
                    // Determine the type of localization
                    string localizationType = "";
                    if (pl.TrimmedLine.StartsWith("name for ", StringComparison.OrdinalIgnoreCase))
                    {
                        localizationType = "name";
                    }
                    else if (pl.TrimmedLine.StartsWith("desc for ", StringComparison.OrdinalIgnoreCase))
                    {
                        localizationType = "desc";
                    }
                    else if (pl.TrimmedLine.StartsWith("short desc for ", StringComparison.OrdinalIgnoreCase))
                    {
                        localizationType = "short_desc";
                    }

                    // Look up the metadata for country tag
                    if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData))
                    {
                        string countryTag = lineData.Id;

                        // Extract the quoted value from the line using BaseParser helper
                        string localizedText = GetQuotedContent(pl.TrimmedLine);

                        var localizedEntry = new LocalizedText
                        {
                            Value = localizedText,
                            CountryTag = countryTag
                        };

                        // Determine if this belongs to archetype (depth 1) or equipment (depth >= 2)
                        if (pl.Depth == 1)
                        {
                            // Direct child of archetype
                            if (localizationType == "name")
                                currentArchetype.Names.Add(localizedEntry);
                            else if (localizationType == "desc")
                                currentArchetype.Descriptions.Add(localizedEntry);
                            else if (localizationType == "short_desc")
                                currentArchetype.ShortDescriptions.Add(localizedEntry);
                        }
                        else if (pl.Depth >= 2)
                        {
                            // Belongs to equipment - find the deepest equipment at shallower depth
                            int shallowerDepth = equipmentByDepth.Keys.Where(k => k < pl.Depth).DefaultIfEmpty(-1).Max();
                            if (shallowerDepth != -1 && equipmentByDepth.TryGetValue(shallowerDepth, out var equipment))
                            {
                                if (localizationType == "name")
                                    equipment.Names.Add(localizedEntry);
                                else if (localizationType == "desc")
                                    equipment.Descriptions.Add(localizedEntry);
                                else if (localizationType == "short_desc")
                                    equipment.ShortDescriptions.Add(localizedEntry);
                            }
                        }
                    }
                    continue;
                }

                // Handle unlocalized text (name, desc, short desc) without "for" - no country tag lookup
                // Can be at any depth within the archetype
                if (pl.TrimmedLine.StartsWith("name", StringComparison.OrdinalIgnoreCase) && !pl.TrimmedLine.StartsWith("name for", StringComparison.OrdinalIgnoreCase))
                {
                    string remainder = pl.TrimmedLine.Length > "name".Length ? pl.TrimmedLine.Substring("name".Length).Trim() : string.Empty;
                    if (!string.IsNullOrEmpty(remainder))
                    {
                        string nameText = GetQuotedContent(remainder);
                        var localizedEntry = new LocalizedText
                        {
                            Value = nameText,
                            CountryTag = null
                        };

                        if (pl.Depth == 1)
                        {
                            currentArchetype.Names.Add(localizedEntry);
                        }
                        else if (pl.Depth >= 2)
                        {
                            // Find the deepest equipment at shallower depth
                            int shallowerDepth = equipmentByDepth.Keys.Where(k => k < pl.Depth).DefaultIfEmpty(-1).Max();
                            if (shallowerDepth != -1 && equipmentByDepth.TryGetValue(shallowerDepth, out var equipment))
                            {
                                equipment.Names.Add(localizedEntry);
                            }
                        }
                    }
                    continue;
                }

                if (pl.TrimmedLine.StartsWith("desc", StringComparison.OrdinalIgnoreCase) && !pl.TrimmedLine.StartsWith("desc for", StringComparison.OrdinalIgnoreCase))
                {
                    string remainder = pl.TrimmedLine.Length > "desc".Length ? pl.TrimmedLine.Substring("desc".Length).Trim() : string.Empty;
                    if (!string.IsNullOrEmpty(remainder))
                    {
                        string descText = GetQuotedContent(remainder);
                        var localizedEntry = new LocalizedText
                        {
                            Value = descText,
                            CountryTag = null
                        };

                        if (pl.Depth == 1)
                        {
                            currentArchetype.Descriptions.Add(localizedEntry);
                        }
                        else if (pl.Depth >= 2)
                        {
                            // Find the deepest equipment at shallower depth
                            int shallowerDepth = equipmentByDepth.Keys.Where(k => k < pl.Depth).DefaultIfEmpty(-1).Max();
                            if (shallowerDepth != -1 && equipmentByDepth.TryGetValue(shallowerDepth, out var equipment))
                            {
                                equipment.Descriptions.Add(localizedEntry);
                            }
                        }
                    }
                    continue;
                }

                if (pl.TrimmedLine.StartsWith("short desc", StringComparison.OrdinalIgnoreCase) && !pl.TrimmedLine.StartsWith("short desc for", StringComparison.OrdinalIgnoreCase))
                {
                    string remainder = pl.TrimmedLine.Length > "short desc".Length ? pl.TrimmedLine.Substring("short desc".Length).Trim() : string.Empty;
                    if (!string.IsNullOrEmpty(remainder))
                    {
                        string shortDescText = GetQuotedContent(remainder);
                        var localizedEntry = new LocalizedText
                        {
                            Value = shortDescText,
                            CountryTag = null
                        };

                        if (pl.Depth == 1)
                        {
                            currentArchetype.ShortDescriptions.Add(localizedEntry);
                        }
                        else if (pl.Depth >= 2)
                        {
                            // Find the deepest equipment at shallower depth
                            int shallowerDepth = equipmentByDepth.Keys.Where(k => k < pl.Depth).DefaultIfEmpty(-1).Max();
                            if (shallowerDepth != -1 && equipmentByDepth.TryGetValue(shallowerDepth, out var equipment))
                            {
                                equipment.ShortDescriptions.Add(localizedEntry);
                            }
                        }
                    }
                    continue;
                }

                // Unrecognized content: save as raw line
                var rawLine = new RawLine { trimmedLine = pl.TrimmedLine, depth = pl.Depth };

                int parentEquipmentDepth = equipmentByDepth.Keys.Where(k => k < pl.Depth).DefaultIfEmpty(-1).Max();
                if (parentEquipmentDepth != -1 && equipmentByDepth.TryGetValue(parentEquipmentDepth, out var parentEquipment))
                {
                    parentEquipment.RawLines.Add(rawLine);
                }
                else
                {
                    currentArchetype.RawLines.Add(rawLine);
                }
            }

            // save parsed result
            LastParsedFile = parsedFile;
        }
    }
}
