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
            ["picture sprite"] = new[] { 1 },
        };
        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            // Handle category-only picture sprite attribute
            if (trimmedLine.StartsWith("picture sprite", StringComparison.OrdinalIgnoreCase))
            {
                // picture sprite is category-only; depth 1 expected. If found at depth 2 it's inside a decision and must error.
                if (currentDepth == 2)
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, "ERROR! 'picture sprite' is only allowed in category scope and must not appear inside a decision block."));
                    return true;
                }

                // Extract the value after 'picture sprite'
                string remainder = trimmedLine.Length > "picture sprite".Length ? trimmedLine.Substring("picture sprite".Length).Trim() : string.Empty;

                if (string.IsNullOrEmpty(remainder) || !IsQuotedString(remainder))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, "Malformed picture sprite: expected a quoted GFX_ identifier after 'picture sprite'."));
                    return true;
                }

                string inner = remainder.Substring(1, remainder.Length - 2);

                // picture sprite strictly requires GFX_ prefix; helper will emit any non-ASCII warnings
                if (!IsValidGfxName(inner, true, fileName, lineNumber, ComponentName))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, $"Invalid picture sprite name: '{inner}'. picture sprite requires a quoted name beginning with 'GFX_'."));
                }

                // Ensure nothing exists outside the quoted string
                string outside = RemoveQuotedContent(remainder);
                if (!string.IsNullOrWhiteSpace(outside))
                {
                    Errors.Add(new ValidationError(fileName, lineNumber, "Malformed picture sprite: unexpected content outside quoted string."));
                }

                ExpectedDepth = currentDepth;
                return true;
            }
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

                // Validate using the helper (IDs are now case-insensitive / allow uppercase letters).
                // The helper will emit a non-blocking warning if the identifier contains non-ASCII characters.
                if (!IsValidId(identifier, fileName, lineNumber, ComponentName, DotsAllowed))
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! UNALLOWED ID DETECTED: '{identifier}'. IDs must be alphanumeric (A-Z, a-z, 0-9, and underscores), and contain no spaces or braces."
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

