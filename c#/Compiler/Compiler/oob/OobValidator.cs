using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace Compiler
{
    // Represents a single line's data
    public class OobLineData : IMiscListLineData
    {
        public int LineNumber { get; set; }
        public string Id { get; set; }
        public string Coordinates { get; set; }
        public string CountryTag { get; set; }
        public string Misc { get; set; }
        public List<string> MiscList { get; set; } = new();
    }

    // The main object passed to the parser
    public class OobMetadata
    {
        public Dictionary<int, OobLineData> Lines { get; set; } = new();
    }

    public class OobValidator : BaseValidator
    {
        private OobParser _parser;

        protected override BaseParser Parser
        {
            get
            {
                if (_parser == null)
                {
                    _parser = new OobParser { Metadata = Metadata };
                }
                return _parser;
            }
        }

        public OobMetadata Metadata { get; private set; } = new OobMetadata();

        protected override Dictionary<string, int[]> AllowedBlockDepths => new(StringComparer.OrdinalIgnoreCase)
        {
            ["place fleet"] = new[] { 0 },
            ["place airwings"] = new[] { 0 },
            ["place division"] = new[] { 0 },
            ["division template"] = new[] { 0 },
            ["add production"] = new[] { 0 },
            ["support units"] = new[] { 1 },
            ["place taskforce"] = new[] { 1 },
            ["ship "] = new[] { 2 },
            ["using design "] = new[] { 3 },
        };

        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            if (trimmedLine.StartsWith("add production ", StringComparison.OrdinalIgnoreCase) && currentDepth == 0)
            {
                int forIndex = trimmedLine.LastIndexOf(" for ", StringComparison.OrdinalIgnoreCase);

                if (forIndex != -1)
                {
                    string equipmentId = trimmedLine.Substring("add production ".Length, forIndex - "add production ".Length).Trim();
                    string countryTag = trimmedLine.Substring(forIndex + " for ".Length).Trim();

                    bool isEquipmentValid = IsValidId(equipmentId, fileName, lineNumber, ComponentName, DotsAllowed);
                    bool isTagValid = IsValidCountryId(countryTag, fileName, lineNumber, ComponentName);

                    if (!isEquipmentValid)
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID EQUIPMENT ID: '{equipmentId}' must be a valid identifier."
                        ));
                    }

                    if (!isTagValid)
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID COUNTRY TAG: '{countryTag}' must be a valid 3-character country tag."
                        ));
                    }

                    if (isEquipmentValid && isTagValid)
                    {
                        Metadata.Lines[lineNumber] = new OobLineData
                        {
                            LineNumber = lineNumber,
                            Id = equipmentId,
                            CountryTag = countryTag
                        };
                    }
                }
                else
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! MALFORMED SYNTAX: Expected format 'add production <equipment_id> for <TAG>'."
                    ));
                }

                ExpectedDepth = currentDepth + 1;
                return true;
            }
            if (trimmedLine.StartsWith("ship ", StringComparison.OrdinalIgnoreCase) && currentDepth == 2)
            {
                int archIndex = trimmedLine.IndexOf(" using archetype ", StringComparison.OrdinalIgnoreCase);
                int forIndex = trimmedLine.LastIndexOf(" for ", StringComparison.OrdinalIgnoreCase);

                if (archIndex != -1 && forIndex != -1 && forIndex > archIndex)
                {
                    int archStart = archIndex + " using archetype ".Length;
                    string archetypeId = trimmedLine.Substring(archStart, forIndex - archStart).Trim();
                    string countryTag = trimmedLine.Substring(forIndex + " for ".Length).Trim();

                    bool isArchValid = IsValidId(archetypeId, fileName, lineNumber, ComponentName, DotsAllowed);
                    bool isTagValid = IsValidCountryId(countryTag, fileName, lineNumber, ComponentName);

                    if (!isArchValid)
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID ARCHETYPE ID: '{archetypeId}' must be a valid identifier."
                        ));
                    }

                    if (!isTagValid)
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID COUNTRY TAG: '{countryTag}' must be a valid 3-character country tag."
                        ));
                    }

                    if (isArchValid && isTagValid)
                    {
                        Metadata.Lines[lineNumber] = new OobLineData
                        {
                            LineNumber = lineNumber,
                            Id = archetypeId,
                            CountryTag = countryTag
                        };
                    }
                }
                ExpectedDepth = currentDepth + 1;
                return true;
            }
            if (trimmedLine.StartsWith("place taskforce ", StringComparison.OrdinalIgnoreCase) && currentDepth == 1)
            {
                int atIndex = trimmedLine.LastIndexOf("\" at ", StringComparison.OrdinalIgnoreCase);
                if (atIndex != -1)
                {
                    string coordStr = trimmedLine.Substring(atIndex + 4).Trim();
                    if (int.TryParse(coordStr, out _))
                    {
                        Metadata.Lines[lineNumber] = new OobLineData
                        {
                            LineNumber = lineNumber,
                            Coordinates = coordStr
                        };
                    }
                    else
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID COORDINATE: '{coordStr}' is not a valid integer."
                        ));
                    }
                }
                ExpectedDepth = currentDepth + 1;
                return true;
            }
            if (trimmedLine.StartsWith("place fleet ", StringComparison.OrdinalIgnoreCase) && currentDepth == 0)
            {
                int atIndex = trimmedLine.LastIndexOf("\" at ", StringComparison.OrdinalIgnoreCase);
                if (atIndex != -1)
                {
                    string coordStr = trimmedLine.Substring(atIndex + 4).Trim();
                    if (int.TryParse(coordStr, out _))
                    {
                        Metadata.Lines[lineNumber] = new OobLineData
                        {
                            LineNumber = lineNumber,
                            Coordinates = coordStr
                        };
                    }
                    else
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID COORDINATE: '{coordStr}' is not a valid integer."
                        ));
                    }
                }
                ExpectedDepth = currentDepth + 1;
                return true;
            }
            if (trimmedLine.StartsWith("place airwings at ", StringComparison.OrdinalIgnoreCase) && currentDepth == 0)
            {
                string remainingStr = trimmedLine.Substring("place airwings at ".Length).Trim();

                if (int.TryParse(remainingStr, out _))
                {
                    Metadata.Lines[lineNumber] = new OobLineData
                    {
                        LineNumber = lineNumber,
                        Coordinates = remainingStr
                    };
                }
                else
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID COORDINATE: '{remainingStr}' is not a valid integer."
                    ));
                }
            }

            if (trimmedLine.StartsWith("place division ", StringComparison.OrdinalIgnoreCase) && currentDepth == 0)
            {
                int atIndex = trimmedLine.IndexOf("\" at ", StringComparison.OrdinalIgnoreCase);
                int usingIndex = trimmedLine.IndexOf(" using template", StringComparison.OrdinalIgnoreCase);

                if (atIndex != -1 && usingIndex != -1 && usingIndex > atIndex)
                {
                    int startPos = atIndex + "\" at ".Length;
                    string coordStr = trimmedLine.Substring(startPos, usingIndex - startPos).Trim();

                    if (int.TryParse(coordStr, out _))
                    {
                        Metadata.Lines[lineNumber] = new OobLineData
                        {
                            LineNumber = lineNumber,
                            Coordinates = coordStr
                        };
                    }
                    else
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID COORDINATE: '{coordStr}' is not a valid integer."
                        ));
                    }
                }
            }














































            // ==========================================
            // HANDLE "unit types"
            // ==========================================
            if (ValidateBlockSection(
                    trimmedLine, currentDepth, lineNumber, fileName,
                    keyword: "unit types",
                    expectedHeaderDepth: 1,
                    parentBlockHeader: "unit",
                    errorMessagePrefix: "UNIT TYPE",
                    metadataLines: Metadata.Lines))
                {
                    ExpectedDepth = currentDepth + 1;
                    return true;
                }

            // ==========================================
            // HANDLE "unit categories"
            // ==========================================
                if (ValidateBlockSection(
                    trimmedLine, currentDepth, lineNumber, fileName,
                    keyword: "unit categories",
                    expectedHeaderDepth: 1,
                    parentBlockHeader: "unit",
                    errorMessagePrefix: "UNIT CATEGORY",
                    metadataLines: Metadata.Lines))
                {
                    ExpectedDepth = currentDepth + 1;
                    return true;
                }

            // ==========================================
            // HANDLE "required equipment"
            // ==========================================
                if (ValidateBlockSection(
                    trimmedLine, currentDepth, lineNumber, fileName,
                    keyword: "required equipment",
                    expectedHeaderDepth: 1,
                    parentBlockHeader: "unit",
                    errorMessagePrefix: "REQUIRED EQUIPMENT",
                    metadataLines: Metadata.Lines))
                {
                    ExpectedDepth = currentDepth + 1;
                    return true;
                }

            // ==========================================
            // HANDLE TYPE DEFINITIONS (DEPTH 0)
            // ==========================================
            if (trimmedLine.StartsWith("unit ", StringComparison.OrdinalIgnoreCase) &&
               !trimmedLine.StartsWith("unit model", StringComparison.OrdinalIgnoreCase))
            {
                if (currentDepth != 0)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! ROOT-LEVEL SYNTAX AT NON-ZERO DEPTH: Type definitions must be at depth 0, but found at depth {currentDepth}."
                    ));
                }

                string unitId = trimmedLine.Substring("unit ".Length).Trim();
                if (!IsValidId(unitId, fileName, lineNumber, ComponentName, DotsAllowed))
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID UNIT ID: '{unitId}' must be a valid identifier."
                    ));
                }
                else
                {
                    Metadata.Lines[lineNumber] = new OobLineData
                    {
                        LineNumber = lineNumber,
                        Id = unitId
                    };
                }

                ExpectedDepth = currentDepth + 1;
                return true;
            }

            return false;
        }
    }
}