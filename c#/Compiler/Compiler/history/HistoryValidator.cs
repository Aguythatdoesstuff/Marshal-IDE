using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace Compiler
{
    // Known condition types for history conditions
    public enum ConditionType
    {
        Ideology, // sub-ideology is allias of ideology!!!
        Subject,
        Overlord,
        Party,
        Autonomy
    }

    public class RGBColor
    {
        public int Red { get; set; }
        public int Green { get; set; }
        public int Blue { get; set; }
    }

    // Represents a single line's data
    public class HistoryLineData : IMiscListLineData
    {
        public int LineNumber { get; set; }
        public string Id { get; set; }
        public string Misc { get; set; }
        public RGBColor RgbValue { get; set; }
        public List<Condition> NameConditions { get; set; } = new();
        public List<string> MiscList { get; set; } = new();

        public class Condition
        {
            public ConditionType Type { get; set; }
            public bool isdef { get; set; } = false;
            public bool isAdj { get; set; } = false;
            public string Value { get; set; } // e.g. liberalism
        }
    }

    // The main object passed to the parser
    public class HistoryMetadata
    {
        public Dictionary<int, HistoryLineData> Lines { get; set; } = new();
    }

    public class HistoryValidator : BaseValidator
    {
        private HistoryParser _parser;

        protected override BaseParser Parser
        {
            get
            {
                if (_parser == null)
                {
                    _parser = new HistoryParser { Metadata = Metadata };
                }
                return _parser;
            }
        }

        public HistoryMetadata Metadata { get; private set; } = new HistoryMetadata();

        protected override Dictionary<string, int[]> AllowedBlockDepths => new(StringComparer.OrdinalIgnoreCase)
        {
            ["country \""] = new[] { 0 },
            ["name "] = new[] { 1 },
            ["party name "] = new[] { 1 },
            ["capital "] = new[] { 1 },
            ["ui color "] = new[] { 1 },
            ["map color "] = new[] { 1 },
            
        };

        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            if (trimmedLine.StartsWith("country ", StringComparison.OrdinalIgnoreCase) && currentDepth == 0)
            {
                int forIndex = trimmedLine.LastIndexOf(" with tag ", StringComparison.OrdinalIgnoreCase);

                if (forIndex != -1)
                {
                    string countryTag = trimmedLine.Substring(forIndex + " with tag ".Length).Trim();

                    bool isTagValid = IsValidCountryId(countryTag, fileName, lineNumber, ComponentName);


                    if (!isTagValid)
                    {
                        Errors.Add(new ValidationError(
                            fileName,
                            lineNumber,
                            $"ERROR! INVALID COUNTRY TAG: '{countryTag}' must be a valid 3-character country tag."
                        ));
                    }

                    if (isTagValid)
                    {
                        Metadata.Lines[lineNumber] = new HistoryLineData
                        {
                            LineNumber = lineNumber,
                            Id = countryTag,
                        };
                    }
                }
                else
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! MALFORMED SYNTAX: Expected format 'country \"<country name>\" with tag <Country tag>'."
                    ));
                }

                ExpectedDepth = currentDepth + 1;
                return true;
            }
            else if (trimmedLine.StartsWith("name ", StringComparison.OrdinalIgnoreCase) && currentDepth == 1)
            {
                var withoutQuotes = RemoveQuotedContent(trimmedLine);
                var parts = withoutQuotes.Split(" ", System.StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 1)
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, "ERROR! Empty name statement."));
                    return true;
                }

                // parts[0] is "name" - ignore
                int index = 1;

                var historyLine = new HistoryLineData { LineNumber = lineNumber };
                bool hasDefOrAdj = false;
                bool hasDefKeyword = false;
                bool hasAdjKeyword = false;

                // Check if we only have "name def" or "name adj"
                if (parts.Length == 2 && (parts[1].Equals("def", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("adj", StringComparison.OrdinalIgnoreCase)))
                {
                    hasDefOrAdj = true;
                    if (parts[1].Equals("def", StringComparison.OrdinalIgnoreCase))
                    {
                        hasDefKeyword = true;
                    }
                    else
                    {
                        hasAdjKeyword = true;
                    }
                }
                else
                {
                    // Parse conditions (potentially multiple with "and")
                    while (index < parts.Length)
                    {
                        // Check for "if" keyword (optional)
                        if (index < parts.Length && parts[index].Equals("if", StringComparison.OrdinalIgnoreCase))
                        {
                            index++;
                        }

                        // Optional "is" after "if"
                        if (index < parts.Length && parts[index].Equals("is", StringComparison.OrdinalIgnoreCase))
                        {
                            index++;
                        }

                        // Next should be condition type
                        if (index >= parts.Length)
                        {
                            var supportedTypes = string.Join(", ", "subject", "ideology", "overlord", "autonomy", "party", "sub-ideology");
                            Errors.Add(new ValidationError(fileName, lineNumber,
                                $"ERROR! Expected condition type after 'if'. Supported types from ConditionType enum: {supportedTypes}."));
                            return true;
                        }

                        string conditionTypeRaw = parts[index].ToLower().Replace("_", "-");
                        string conditionType = conditionTypeRaw;
                        if (conditionType == "sub-ideology")
                        {
                            conditionType = "ideology"; // sub-ideology is alias of ideology
                        }

                        var validTypes = new[] { "subject", "ideology", "overlord", "autonomy", "party" };
                        if (!validTypes.Contains(conditionType))
                        {
                            var supportedTypes = string.Join(", ", validTypes);
                            Errors.Add(new ValidationError(fileName, lineNumber,
                                $"ERROR! Invalid condition type: '{parts[index]}'. Received: '{parts[index]}'. Supported types from ConditionType enum: {supportedTypes}."));
                            return true;
                        }
                        index++;

                        // Optional "is" or "of" after condition type
                        if (index < parts.Length && (parts[index].Equals("is", StringComparison.OrdinalIgnoreCase) || parts[index].Equals("of", StringComparison.OrdinalIgnoreCase)))
                        {
                            index++;
                        }

                        // Next should be the condition value
                        if (index >= parts.Length)
                        {
                            var supportedTypes = string.Join(", ", "subject, ideology, overlord, autonomy, party");
                            Errors.Add(new ValidationError(fileName, lineNumber,
                                $"ERROR! Expected condition value after '{conditionType}'. Supported types: {supportedTypes}."));
                            return true;
                        }

                        string conditionValue = parts[index];
                        if (!IsValidId(conditionValue, fileName, lineNumber, ComponentName, DotsAllowed))
                        {
                            Errors.Add(new ValidationError(fileName, lineNumber,
                                $"ERROR! Invalid condition value: '{conditionValue}' must be a valid identifier (alphanumeric, underscore, and optional dots). Received: '{conditionValue}'."));
                            return true;
                        }

                        // Parse condition type enum
                        ConditionType typeEnum = conditionType switch
                        {
                            "ideology" => ConditionType.Ideology,
                            "subject" => ConditionType.Subject,
                            "overlord" => ConditionType.Overlord,
                            "autonomy" => ConditionType.Autonomy,
                            "party" => ConditionType.Party,
                            _ => ConditionType.Ideology
                        };

                        var condition = new HistoryLineData.Condition
                        {
                            Type = typeEnum,
                            Value = conditionValue
                        };

                        historyLine.NameConditions.Add(condition);
                        index++;

                        // Check for "def" or "adj" after condition value
                        if (index < parts.Length)
                        {
                            string nextPart = parts[index].ToLower();
                            if (nextPart == "def")
                            {
                                if (hasDefKeyword || hasAdjKeyword)
                                {
                                    Errors.Add(new ValidationError(fileName, lineNumber,
                                        "ERROR! Only one of 'def' or 'adj' can be specified in name statement."));
                                    return true;
                                }
                                hasDefKeyword = true;
                                hasDefOrAdj = true;
                                index++;
                            }
                            else if (nextPart == "adj")
                            {
                                if (hasDefKeyword || hasAdjKeyword)
                                {
                                    Errors.Add(new ValidationError(fileName, lineNumber,
                                        "ERROR! Only one of 'def' or 'adj' can be specified in name statement."));
                                    return true;
                                }
                                hasAdjKeyword = true;
                                hasDefOrAdj = true;
                                index++;
                            }
                        }

                        // Check for "and" to continue with next condition
                        if (index < parts.Length && parts[index].Equals("and", StringComparison.OrdinalIgnoreCase))
                        {
                            index++;
                            // Loop continues to parse next condition
                        }
                        else
                        {
                            // No more conditions
                            break;
                        }
                    }
                }

                // Apply def/adj flags to all conditions in this line
                if (hasDefKeyword || hasAdjKeyword)
                {
                    foreach (var condition in historyLine.NameConditions)
                    {
                        condition.isdef = hasDefKeyword;
                        condition.isAdj = hasAdjKeyword;
                    }
                }

                // Save to metadata
                Metadata.Lines[lineNumber] = historyLine;
                ExpectedDepth = currentDepth;
                return true;
            }
            else if (trimmedLine.StartsWith("capital ", StringComparison.OrdinalIgnoreCase) && currentDepth == 1)
            {
                string capitalValue = trimmedLine.Substring("capital ".Length).Trim();
                if (!int.TryParse(capitalValue, out int stateId))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber,
                        $"ERROR! Invalid capital value: '{capitalValue}' must be a valid state ID (integer)."));
                }
                else
                {
                    Metadata.Lines[lineNumber] = new HistoryLineData 
                    { 
                        LineNumber = lineNumber,
                        Id = capitalValue 
                    };
                }
            }
            else if (trimmedLine.StartsWith("map color ", StringComparison.OrdinalIgnoreCase) && currentDepth == 1)
            {
                string colorValue = trimmedLine.Substring("map color ".Length).Trim();
                if (!TryParseRGBColor(colorValue, out var rgb))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber,
                        $"ERROR! Invalid map color value: '{colorValue}' must be three RGB values between 0-255 (e.g., 255 128 64)."));
                }
                else
                {
                    Metadata.Lines[lineNumber] = new HistoryLineData 
                    { 
                        LineNumber = lineNumber,
                        RgbValue = new RGBColor { Red = rgb.Red, Green = rgb.Green, Blue = rgb.Blue }
                    };
                }
            }
            else if (trimmedLine.StartsWith("ui color ", StringComparison.OrdinalIgnoreCase) && currentDepth == 1)
            {
                string colorValue = trimmedLine.Substring("ui color ".Length).Trim();
                if (!TryParseRGBColor(colorValue, out var rgb))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber,
                        $"ERROR! Invalid ui color value: '{colorValue}' must be three RGB values between 0-255 (e.g., 255 128 64)."));
                }
                else
                {
                    Metadata.Lines[lineNumber] = new HistoryLineData 
                    { 
                        LineNumber = lineNumber,
                        RgbValue = new RGBColor { Red = rgb.Red, Green = rgb.Green, Blue = rgb.Blue }
                    };
                }
            }
        return false;
        }
    }
}