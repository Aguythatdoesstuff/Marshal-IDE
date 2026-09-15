using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Compiler
{
    // Represents a single line's data
    public class EquipmentLineData
    {
        public int LineNumber { get; set; }
        public string Id { get; set; }
        public string Misc { get; set; }
        public List<string> MiscList { get; set; } = new();
    }

    // The main object passed to the parser
    public class EquipmentMetadata
    {
        public Dictionary<int, EquipmentLineData> Lines { get; set; } = new();
    }

    public class EquipmentValidator : BaseValidator
    {
        protected override BaseParser Parser => new EquipmentParser();

        public EquipmentMetadata Metadata { get; private set; } = new EquipmentMetadata();

        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            // ==========================================
            // HANDLE TYPE DEFINITIONS (DEPTH 0)
            // ==========================================
            if (trimmedLine.StartsWith("define type ", StringComparison.OrdinalIgnoreCase))
            {
                if (currentDepth != 0)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! ROOT-LEVEL SYNTAX AT NON-ZERO DEPTH: Type definitions must be at depth 0, but found at depth {currentDepth}."
                    ));
                }

                string content = trimmedLine.Substring("define type ".Length).Trim();
                int availableFromIndex = content.IndexOf(" available from ", StringComparison.OrdinalIgnoreCase);

                if (availableFromIndex == -1)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! MISSING REQUIRED YEAR: 'available from <year>' is mandatory for type definitions."
                    ));
                }
                else
                {
                    string typeId = content.Substring(0, availableFromIndex).Trim();
                    string yearValue = content.Substring(availableFromIndex + " available from ".Length).Trim();

                    if (!IsValidId(typeId, fileName, lineNumber, ComponentName, DotsAllowed))
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID TYPE ID: '{typeId}' must be a valid identifier."
                        ));
                    }
                    else if (!int.TryParse(yearValue, out _))
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID YEAR VALUE: '{yearValue}' must be a valid integer."
                        ));
                    }
                    else
                    {
                        Metadata.Lines[lineNumber] = new EquipmentLineData
                        {
                            LineNumber = lineNumber,
                            Id = typeId,
                            Misc = yearValue
                        };
                    }
                }

                ExpectedDepth = currentDepth + 1;
                return true;
            }

            // ==========================================
            // HANDLE SPRITE (DEPTH 1+)
            // ==========================================
            if (trimmedLine.StartsWith("sprite ", StringComparison.OrdinalIgnoreCase))
            {
                string content = trimmedLine.Substring("sprite ".Length).Trim();

                if (content.StartsWith("\"") && content.EndsWith("\""))
                {
                    string inner = content.Substring(1, content.Length - 2);
                    if (!IsValidGfxName(inner, false, fileName, lineNumber, ComponentName))
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID SPRITE NAME: '{inner}' must be a valid GFX name."
                        ));
                    }
                    else
                    {
                        Metadata.Lines[lineNumber] = new EquipmentLineData
                        {
                            LineNumber = lineNumber,
                            Id = inner,
                            Misc = string.Empty
                        };
                    }
                }

                return true;
            }

            // ==========================================
            // HANDLE FOR UNITS BLOCKS (DEPTH 1)
            // ==========================================
            if (trimmedLine.StartsWith("for units", StringComparison.OrdinalIgnoreCase))
            {
                if (currentDepth != 1)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID SYNTAX DEPTH: 'for units' declaration must be at depth 1, but found at depth {currentDepth}."
                    ));
                }

                // Check for inline syntax: "for units <id>"
                string content = trimmedLine.Substring("for units".Length).Trim();
                string inlineId = null;

                if (!string.IsNullOrWhiteSpace(content))
                {
                    if (IsValidId(content, fileName, lineNumber, ComponentName, DotsAllowed))
                    {
                        inlineId = content;
                    }
                }

                // Create metadata entry for the header line
                Metadata.Lines[lineNumber] = new EquipmentLineData
                {
                    LineNumber = lineNumber,
                    Id = inlineId ?? string.Empty,
                    Misc = string.Empty,
                    MiscList = new List<string>()
                };

                if (inlineId != null)
                {
                    Metadata.Lines[lineNumber].MiscList.Add(inlineId);
                }

                ExpectedDepth = currentDepth + 1;
                return true;
            }

            // Validate unit type IDs inside "for units" blocks
            if (IsInsideBlock(lineNumber, currentDepth, "for units") && !trimmedLine.StartsWith("equipment ", StringComparison.OrdinalIgnoreCase))
            {
                if (IsValidId(trimmedLine, fileName, lineNumber, ComponentName, DotsAllowed))
                {
                    // Create the child's own metadata entry
                    Metadata.Lines[lineNumber] = new EquipmentLineData
                    {
                        LineNumber = lineNumber,
                        Id = trimmedLine,
                        Misc = string.Empty
                    };

                    // Find parent header and add to its MiscList
                    int headerLineNumber = FindBlockHeaderLine(lineNumber, currentDepth, "for units");
                    if (headerLineNumber != -1 && Metadata.Lines.ContainsKey(headerLineNumber))
                    {
                        Metadata.Lines[headerLineNumber].MiscList.Add(trimmedLine);
                    }

                    return true;
                }
                else
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID UNIT TYPE ID: '{trimmedLine}' must be a valid identifier."
                    ));
                }
            }

            // ==========================================
            // HANDLE LOCALIZED NAME/DESC (DEPTH 2+)
            // ==========================================
            if (trimmedLine.StartsWith("name for ", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("desc for ", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("short desc for ", StringComparison.OrdinalIgnoreCase))
            {
                string prefix = "";
                if (trimmedLine.StartsWith("name for ", StringComparison.OrdinalIgnoreCase))
                    prefix = "name for ";
                else if (trimmedLine.StartsWith("desc for ", StringComparison.OrdinalIgnoreCase))
                    prefix = "desc for ";
                else if (trimmedLine.StartsWith("short desc for ", StringComparison.OrdinalIgnoreCase))
                    prefix = "short desc for ";

                string content = trimmedLine.Substring(prefix.Length).Trim();
                int spaceIndex = content.IndexOf(' ');
                if (spaceIndex != -1)
                {
                    string countryTag = content.Substring(0, spaceIndex).Trim();
                    if (!IsValidCountryId(countryTag, fileName, lineNumber, "Equipment validator"))
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID COUNTRY TAG: '{countryTag}' must be exactly 3 alphanumeric characters long."
                        ));
                    }
                    else
                    {
                        Metadata.Lines[lineNumber] = new EquipmentLineData
                        {
                            LineNumber = lineNumber,
                            Id = countryTag,
                            Misc = string.Empty
                        };
                    }
                }

                return true;
            }

            // ==========================================
            // HANDLE EQUIPMENT DEFINITIONS (DEPTH 2+)
            // ==========================================
            if (trimmedLine.StartsWith("equipment ", StringComparison.OrdinalIgnoreCase))
            {
                if (currentDepth < 2)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID SYNTAX DEPTH: 'equipment' declarations must be at depth 2 or deeper, but found at depth {currentDepth}."
                    ));
                }

                string content = trimmedLine.Substring("equipment ".Length).Trim();
                int availableFromIndex = content.IndexOf(" available from ", StringComparison.OrdinalIgnoreCase);

                if (availableFromIndex == -1)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! MISSING REQUIRED YEAR: 'available from <year>' is mandatory for equipment definitions."
                    ));
                }
                else
                {
                    string equipmentId = content.Substring(0, availableFromIndex).Trim();
                    string yearValue = content.Substring(availableFromIndex + " available from ".Length).Trim();

                    if (!IsValidId(equipmentId, fileName, lineNumber, ComponentName, DotsAllowed))
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID EQUIPMENT ID: '{equipmentId}' must be a valid identifier."
                        ));
                    }
                    else if (!int.TryParse(yearValue, out _))
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID YEAR VALUE: '{yearValue}' must be a valid integer."
                        ));
                    }
                    else
                    {
                        Metadata.Lines[lineNumber] = new EquipmentLineData
                        {
                            LineNumber = lineNumber,
                            Id = equipmentId,
                            Misc = yearValue
                        };
                    }
                }

                ExpectedDepth = currentDepth + 1;
                return true;
            }

            return false;
        }
    }
}
