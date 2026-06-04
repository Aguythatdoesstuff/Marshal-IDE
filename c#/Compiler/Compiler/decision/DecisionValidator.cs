using System.Text.RegularExpressions;

namespace Compiler
{
    public class DecisionValidator : BaseValidator
    {
        public DecisionValidator()
        {
            ComponentName = "Decision";
            Compiler.Logging.Logger.LogComponent(ComponentName, "Validator initialized for Decision files.");
        }
        // Provide a concrete parser so validated decision files are handed off
        // to a specialized parser for further processing.
        protected override BaseParser Parser => new DecisionParser();

        protected override Dictionary<string, int[]> AllowedBlockDepths => new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = new[] { 1, 2 },
            ["desc"] = new[] { 1, 2 },
            ["sprite"] = new[] { 1, 2 },
        };
        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            if (trimmedLine.StartsWith("priority") || trimmedLine.StartsWith("cost"))
            {
                // Determine which keyword we are currently dealing with
                string keyword = trimmedLine.StartsWith("priority") ? "priority" : "cost";

                // Enforce that it only appears at depth 1 or depth 2
                if (currentDepth != 1 && currentDepth != 2)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID SYNTAX DEPTH: '{keyword}' must be at depth 1 or 2, but found at depth {currentDepth}."
                    ));
                }

                // Slice off the keyword prefix to get the value behind it
                string valuePart = trimmedLine.Substring(keyword.Length).Trim();

                // Validate that the value is strictly a space-free integer using your helper
                if (!IsInt(valuePart))
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID {keyword.ToUpper()} VALUE: '{valuePart}' is not a valid integer."
                    ));
                }

                ExpectedDepth = currentDepth + 1;
                return true;
            }
            if (trimmedLine == "allowed" || trimmedLine == "on click" || trimmedLine == "available")
            {
                if (currentDepth != 1 && currentDepth != 2)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID SYNTAX DEPTH: '{trimmedLine}' must be at depth 1 or 2, but found at depth {currentDepth}."
                    ));
                }

                ExpectedDepth = currentDepth + 1;
                return true;
            }
            if (trimmedLine == "on action")
            {
                // Enforce that this root-level syntax appears at depth 0.
                if (currentDepth != 0)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! ROOT-LEVEL SYNTAX AT NON-ZERO DEPTH: 'on action' must be at depth 0, but found at depth {currentDepth}."
                    ));
                }

                // Set the expected depth relative to the current line's depth
                // This allows the validator to continue validating the block
                // even after reporting the indentation error.
                ExpectedDepth = currentDepth + 1;
                return true;
            }

            // Handle block headers that require a strict ID
            bool isCategory = trimmedLine.StartsWith("category ");
            bool isDecision = trimmedLine.StartsWith("decision ");

            if (isCategory || isDecision)
            {
                // Isolate the ID by slicing off the prefix length
                int prefixLength = isCategory ? "category ".Length : "decision ".Length;
                string identifier = trimmedLine.Substring(prefixLength).Trim();

                // Validate using the helper
                if (!IsValidId(identifier))
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! UNALLOWED ID DETECTED: '{identifier}'. IDs must be strictly lowercase, alphanumeric, and contain no spaces or braces."
                    ));
                }

                // Enforce depth: category must be at depth 0, decision must be at depth 1
                int expectedCurrentDepth = isCategory ? 0 : 1;
                if (currentDepth != expectedCurrentDepth)
                {
                    string typeName = isCategory ? "category" : "decision";
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID SYNTAX DEPTH: '{typeName}' must be at depth {expectedCurrentDepth}, but found at depth {currentDepth}."
                    ));
                }

                ExpectedDepth = currentDepth + 1;
                return true;
            }

            return false;
        }
    }
}

