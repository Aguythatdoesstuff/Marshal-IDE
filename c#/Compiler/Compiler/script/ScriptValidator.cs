using System;
using System.Text.RegularExpressions;

namespace Compiler
{
    public class ScriptValidator : BaseValidator
    {
        protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            // Handle standalone "on action" literal (No ID attached)
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
            bool isScriptedEffect = trimmedLine.StartsWith("scripted effect ");
            bool isGameRule = trimmedLine.StartsWith("game rule ");

            if (isScriptedEffect || isGameRule)
            {
                // Isolate the ID by slicing off the prefix length
                int prefixLength = isScriptedEffect ? "scripted effect ".Length : "game rule ".Length;
                string identifier = trimmedLine.Substring(prefixLength).Trim();

                // Strict validation: Must be lower_case_snake_case with no spaces, braces, or special characters
                if (!Regex.IsMatch(identifier, "^[a-z0-9_]+$"))
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! UNALLOWED ID DETECTED: '{identifier}'. IDs must be strictly lowercase, alphanumeric, and contain no spaces or braces."
                    ));
                }

                // Enforce that root-level headers appear at depth 0. If they're
                // found indented, report an error but continue by resetting the
                // expected depth relative to the current line so validation can proceed.
                if (currentDepth != 0)
                {
                    string headerName = isScriptedEffect ? "scripted effect" : "game rule";
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"ERROR! ROOT-LEVEL SYNTAX AT NON-ZERO DEPTH: '{headerName}' must be at depth 0, but found at depth {currentDepth}."
                    ));
                }

                ExpectedDepth = currentDepth + 1;
                return true;
            }

            return false;
        }
    }
}