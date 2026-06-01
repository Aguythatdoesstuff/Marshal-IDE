using System.Text.RegularExpressions;

namespace Compiler
{
    public class EventValidator : BaseValidator
    {
        protected override BaseParser Parser => new EventParser();

        protected override Dictionary<string, int[]> AllowedBlockDepths => new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = new[] { 1 },
            ["desc"] = new[] { 1 },
            ["sprite"] = new[] { 1 },
        };
        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            bool isCountryEvent = trimmedLine.StartsWith("country event ");
            bool isNewsEvent = trimmedLine.StartsWith("news event ");

            if (isCountryEvent || isNewsEvent)
            {
                // Enforce that headers must be at root level (depth 0)
                if (currentDepth != 0)
                {
                    string headerType = isCountryEvent ? "country event" : "news event";
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! ROOT-LEVEL SYNTAX AT NON-ZERO DEPTH: '{headerType}' must be at depth 0, but found at depth {currentDepth}."
                    ));
                }

                // Isolate and validate the Event ID using the new helper
                string prefix = isCountryEvent ? "country event " : "news event ";
                string eventId = trimmedLine.Substring(prefix.Length).Trim();

                if (!IsValidEventId(eventId))
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID EVENT ID: '{eventId}'. Event IDs must be lowercase snake_case and end with a dot followed by an integer (e.g., id.1)."
                    ));
                }

                ExpectedDepth = currentDepth + 1;
                return true;
            }

            if (trimmedLine.StartsWith("option "))
            {
                string optionValue = trimmedLine.Substring("option ".Length).Trim();

                // Ensure it is enclosed in quotes using your base helper
                if (!IsQuotedString(optionValue))
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID OPTION VALUE: '{optionValue}' must be a valid string enclosed in double quotes."
                    ));
                }

                // Step up the expected depth for whatever logic is written INSIDE this option block
                ExpectedDepth = currentDepth + 1;
                return true;
            }

            return false;
        }
    }
}