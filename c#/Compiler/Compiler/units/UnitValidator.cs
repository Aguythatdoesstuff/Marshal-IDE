using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace Compiler
{
    // Represents a single line's data
    public class UnitLineData : IMiscListLineData
    {
        public int LineNumber { get; set; }
        public string Id { get; set; }
        public string Misc { get; set; }
        public List<string> MiscList { get; set; } = new();
    }

    // The main object passed to the parser
    public class UnitMetadata
    {
        public Dictionary<int, UnitLineData> Lines { get; set; } = new();
    }

    public class UnitValidator : BaseValidator
    {
        private UnitParser _parser;

        protected override BaseParser Parser
        {
            get
            {
                if (_parser == null)
                {
                    _parser = new UnitParser { Metadata = Metadata };
                }
                return _parser;
            }
        }

        public UnitMetadata Metadata { get; private set; } = new UnitMetadata();

        protected override Dictionary<string, int[]> AllowedBlockDepths => new(StringComparer.OrdinalIgnoreCase)
        {
            ["unit types"] = new[] { 1 },
            ["unit categories"] = new[] { 1 },
            ["required equipment"] = new[] { 1 },
            ["name"] = new[] { 1 },
            ["desc"] = new[] { 1 },
            ["unit model"] = new[] { 1 },
            ["abbreviation"] = new[] { 1 },
        };

        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
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
                    Metadata.Lines[lineNumber] = new UnitLineData
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