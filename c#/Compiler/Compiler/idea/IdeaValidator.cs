using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Compiler
{
    public class IdeaValidator : BaseValidator
    {
        protected override BaseParser Parser => new IdeaParser();
        protected override Dictionary<string, int[]> AllowedBlockDepths => new(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = new[] { 1 },
            ["desc"] = new[] { 1 },
            ["sprite"] = new[] { 1 },
            ["modifier"] = new[] { 1 },
        };

        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            // Handle idea definitions of the form '<type> idea <id>' where type can be any token.
            var sepIndex = trimmedLine.IndexOf(" idea ", StringComparison.OrdinalIgnoreCase);
            if (sepIndex >= 0)
            {
                // Enforce root-level header for ideas
                if (currentDepth != 0)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! ROOT-LEVEL SYNTAX AT NON-ZERO DEPTH: '<type> idea' must be at depth 0, but found at depth {currentDepth}."
                    ));
                }

                var typeToken = trimmedLine.Substring(0, sepIndex).Trim();
                var id = trimmedLine.Substring(sepIndex + " idea ".Length).Trim();

                if (string.IsNullOrEmpty(typeToken))
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        "ERROR! MISSING IDEA TYPE: expected '<type> idea <id>' with a non-empty type token."
                    ));
                }
                else if (!IsValidId(typeToken, fileName, lineNumber, ComponentName, DotsAllowed))
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID IDEA TYPE: '{typeToken}'. Idea types must follow the same rules as IDs (lowercase, alphanumeric, underscores, no spaces)."
                    ));
                }

                if (string.IsNullOrEmpty(id))
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        "ERROR! MISSING IDEA ID: '<type> idea' requires an identifier (lowercase, alphanumeric and underscores)."
                    ));
                }
                else if (!IsValidId(id, fileName, lineNumber, ComponentName, DotsAllowed))
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! INVALID IDEA ID: '{id}'. IDs must be lowercase, alphanumeric, and may contain underscores."
                    ));
                }

                // Expect inner content (name/desc/sprite/modifier/etc.) indented one level
                ExpectedDepth = currentDepth + 1;
                return true;
            }

            return false;
        }
    }
}