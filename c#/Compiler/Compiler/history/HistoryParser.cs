using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace Compiler
{
    public class HistoryParser : BaseParser
    {
        public HistoryMetadata Metadata { get; set; }

        public HistoryParser()
        {
            Compiler.Logging.Logger.LogComponent("Parser", "HistoryParser initialized.");
        }

        public class Historys
        {
            public List<Country> Countrys { get; set; } = new List<Country>();
        }

        public class Country
        {
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Tag { get; set; } = string.Empty;
            public string Capital { get; set; } = string.Empty;
            public RGBColor MapColor { get; set; } = null;
            public RGBColor UiColor { get; set; } = null;
            public List<RawLine> RawLines { get; set; } = new List<RawLine>();
            public List<ConditionalName> ConditionalNames { get; set; } = new List<ConditionalName>();
            public List<ConditionalPartyName> ConditionalPartyNames { get; set; } = new List<ConditionalPartyName>();
        }

        public class Condition
        {
            public string Type { get; set; } = string.Empty;
            public string ConditionValue { get; set; } = string.Empty;
        }

        public class ConditionalName
        {
            public string Name { get; set; } = string.Empty;
            public List<Condition> Conditions { get; set; } = new List<Condition>();
        }

        public class ConditionalPartyName
        {
            public string Name { get; set; } = string.Empty;
            public List<Condition> Conditions { get; set; } = new List<Condition>();
        }


        public class ParsedHistoryFile
        {
            public string SourceFileName { get; set; } = string.Empty;
            public List<Historys> Historys { get; set; } = new List<Historys>();
        }

        // Store the most recently parsed file for other components to use
        public ParsedHistoryFile LastParsedFile { get; private set; }

        public override void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines)
        {
            var parsedFile = new ParsedHistoryFile { SourceFileName = fileName };
            var historys = new Historys();
            Country currentCountry = null;

            foreach (var preprocessedLine in preprocessedLines)
            {
                var trimmedLine = preprocessedLine.TrimmedLine;
                var depth = preprocessedLine.Depth;
                var lineNumber = preprocessedLine.LineNumber;

                // Handle country line
                if (trimmedLine.StartsWith("country \"", StringComparison.OrdinalIgnoreCase) && depth == 0)
                {
                    var countryName = GetQuotedContent(trimmedLine);
                    currentCountry = new Country
                    {
                        Name = countryName,
                    };

                    // Get tag from metadata
                    if (Metadata?.Lines != null && Metadata.Lines.TryGetValue(lineNumber, out var metadataLine))
                    {
                        currentCountry.Id = metadataLine.Id;
                        currentCountry.Tag = metadataLine.Id;
                    }

                    historys.Countrys.Add(currentCountry);
                }
                // Handle capital line
                else if (trimmedLine.StartsWith("capital ", StringComparison.OrdinalIgnoreCase) && depth == 1 && currentCountry != null)
                {
                    if (Metadata?.Lines != null && Metadata.Lines.TryGetValue(lineNumber, out var metadataLine))
                    {
                        currentCountry.Capital = metadataLine.Id;
                    }
                }
                // Handle map color line
                else if (trimmedLine.StartsWith("map color ", StringComparison.OrdinalIgnoreCase) && depth == 1 && currentCountry != null)
                {
                    if (Metadata?.Lines != null && Metadata.Lines.TryGetValue(lineNumber, out var metadataLine))
                    {
                        if (metadataLine.RgbValue != null)
                        {
                            currentCountry.MapColor = new RGBColor
                            {
                                Red = metadataLine.RgbValue.Red,
                                Green = metadataLine.RgbValue.Green,
                                Blue = metadataLine.RgbValue.Blue
                            };
                        }
                    }
                }
                // Handle ui color line
                else if (trimmedLine.StartsWith("ui color ", StringComparison.OrdinalIgnoreCase) && depth == 1 && currentCountry != null)
                {
                    if (Metadata?.Lines != null && Metadata.Lines.TryGetValue(lineNumber, out var metadataLine))
                    {
                        if (metadataLine.RgbValue != null)
                        {
                            currentCountry.UiColor = new RGBColor
                            {
                                Red = metadataLine.RgbValue.Red,
                                Green = metadataLine.RgbValue.Green,
                                Blue = metadataLine.RgbValue.Blue
                            };
                        }
                    }
                }
                // Handle name line with conditions
                else if (trimmedLine.StartsWith("name ", StringComparison.OrdinalIgnoreCase) && depth == 1 && currentCountry != null)
                {
                    if (Metadata?.Lines != null && Metadata.Lines.TryGetValue(lineNumber, out var metadataLine))
                    {
                        // Get the quoted name content
                        var nameContent = GetQuotedContent(trimmedLine);

                        // If there are conditions, create a single ConditionalName with all conditions
                        if (metadataLine.NameConditions != null && metadataLine.NameConditions.Count > 0)
                        {
                            var conditionalName = new ConditionalName
                            {
                                Name = nameContent,
                                Conditions = metadataLine.NameConditions
                                    .Select(condition => new Condition
                                    {
                                        Type = condition.Type.ToString().ToLower(),
                                        ConditionValue = condition.Value
                                    })
                                    .ToList()
                            };
                            currentCountry.ConditionalNames.Add(conditionalName);
                        }
                        else
                        {
                            // No conditions, just set the default name
                            currentCountry.Name = nameContent;
                        }
                    }
                }
                // Handle party name line with conditions
                else if (trimmedLine.StartsWith("party name ", StringComparison.OrdinalIgnoreCase) && depth == 1 && currentCountry != null)
                {
                    if (Metadata?.Lines != null && Metadata.Lines.TryGetValue(lineNumber, out var metadataLine))
                    {
                        // Get the quoted name content
                        var nameContent = GetQuotedContent(trimmedLine);

                        // If there are conditions, create a single ConditionalPartyName with all conditions
                        if (metadataLine.NameConditions != null && metadataLine.NameConditions.Count > 0)
                        {
                            var conditionalPartyName = new ConditionalPartyName
                            {
                                Name = nameContent,
                                Conditions = metadataLine.NameConditions
                                    .Select(condition => new Condition
                                    {
                                        Type = condition.Type.ToString().ToLower(),
                                        ConditionValue = condition.Value
                                    })
                                    .ToList()
                            };
                            currentCountry.ConditionalPartyNames.Add(conditionalPartyName);
                        }
                    }
                }
                // Save all other lines as raw lines (inside country blocks)
                else if (depth >= 1 && currentCountry != null && trimmedLine != "}")
                {
                    currentCountry.RawLines.Add(new RawLine
                    {
                        trimmedLine = trimmedLine,
                        depth = depth
                    });
                }
            }

            parsedFile.Historys.Add(historys);
            LastParsedFile = parsedFile;
        }
    }
}