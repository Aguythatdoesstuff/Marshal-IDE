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
            ["unit"] = new[] { 0 },
            ["unit types"] = new[] { 1 },
            ["unit categories"] = new[] { 1 },
        };

        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            // ==========================================
            // HANDLE TYPE DEFINITIONS (DEPTH 0)
            // ==========================================
            if (trimmedLine.StartsWith("unit ", StringComparison.OrdinalIgnoreCase))
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

                ExpectedDepth = currentDepth;
                return true;
            }

            // ==========================================
            // HANDLE "unit types"
            // ==========================================
            if (!trimmedLine.StartsWith("equipment ", StringComparison.OrdinalIgnoreCase))
            {
                if (ValidateBlockSection(
                    trimmedLine, currentDepth, lineNumber, fileName,
                    keyword: "unit types",
                    expectedHeaderDepth: 1,
                    parentBlockHeader: "for units",
                    errorMessagePrefix: "UNIT TYPE",
                    metadataLines: Metadata.Lines))
                {
                    return true;
                }
            }

            // ==========================================
            // HANDLE "unit categories"
            // ==========================================
            if (ValidateBlockSection(
                trimmedLine, currentDepth, lineNumber, fileName,
                keyword: "unit categories",
                expectedHeaderDepth: 1,
                parentBlockHeader: "for units",
                errorMessagePrefix: "UNIT CATEGORY",
                metadataLines: Metadata.Lines))
            {
                return true;
            }

            


            return false;
        }
    }
}
