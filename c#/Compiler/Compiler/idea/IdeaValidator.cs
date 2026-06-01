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
        };

        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            // Handle idea definitions. We only support 'country idea <id>' as the
            // documentation mandates.
            if (trimmedLine.StartsWith("country idea ", StringComparison.OrdinalIgnoreCase))
            {
                // Enforce root-level header for ideas
                if (currentDepth != 0)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! ROOT-LEVEL SYNTAX AT NON-ZERO DEPTH: 'country idea' must be at depth 0, but found at depth {currentDepth}."
                    ));
                }

                string id = trimmedLine.Substring("country idea ".Length).Trim();
                if (string.IsNullOrEmpty(id))
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        "ERROR! MISSING IDEA ID: 'country idea' requires an identifier (lowercase, alphanumeric and underscores)."
                    ));
                }
                else if (!IsValidId(id))
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