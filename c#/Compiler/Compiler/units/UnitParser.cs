using System;
using System.Collections.Generic;
using System.ComponentModel;
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

            public List<RawLine> RawLines { get; set; } = new List<RawLine>();

            // Represents the upgraded/better version nested inside this equipment
        }

        public class ParsedUnitFile
        {
            public string SourceFileName { get; set; } = string.Empty;
            public List<Unit> Units { get; set; } = new List<Unit>();
        }
        // store the most recently parsed file for other components to use
        public ParsedUnitFile LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {

            // Use ParsedUnitFile to collect all archetypes from this file and store the file name
            var parsedFile = new ParsedUnitFile { SourceFileName = fileName };


            // Track if we are currently reading unit IDs under a "for units" block
            bool insideUnitsTypes = false;

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
                    insideUnitsTypes = false;
                    if (pl.TrimmedLine.StartsWith("unit ", StringComparison.OrdinalIgnoreCase))
                    {
                        // Look up the metadata for this line using the line number
                        if (Metadata.Lines.TryGetValue(pl.LineNumber, out var lineData))
                        {
                            // Create a new Unit with the ID and year from the metadata
                            var archetype = new Unit
                            {
                                Id = lineData.Id
                            };

                            parsedFile.Units.Add(archetype);
                        }
                    }
                    // Depth 0 content that isn't recognized is invalid syntax
                    continue;
                }


            // save parsed result
            LastParsedFile = parsedFile;
            }
        }
    }
}
