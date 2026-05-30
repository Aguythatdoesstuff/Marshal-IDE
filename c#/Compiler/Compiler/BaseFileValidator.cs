using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Compiler
{
    public record ValidationError(string FileName, int LineNumber, string ErrorMessage);

    public abstract class BaseValidator
    {
        public List<ValidationError> Errors { get; protected set; } = new List<ValidationError>();

        public void ValidateFile(string filePath, string fileName)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"[ERROR] File does not exist: {filePath}");
                return;
            }
            try
            {
                var lines = File.ReadAllLines(filePath);

                var sanitizedLines = lines.Select(SanitizeLine).ToList();

                var withoutComments = StripComments(sanitizedLines, fileName);

                ValidateLines(withoutComments, filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to read file: {filePath}. Exception: {ex.Message}");
            }
        }
        // Helper method to count spaces at the start of the line
        private int GetLeadingSpaceCount(string line)
        {
            if (string.IsNullOrEmpty(line)) return 0;
            return line.TakeWhile(c => c == ' ').Count();
        }

        private List<string> StripComments(List<string> lines, string fileName)
        {
            var result = new List<string>();
            bool inBlockComment = false;

            for (int i = 0; i < lines.Count; i++)
            {
                char[] chars = lines[i].ToCharArray();
                bool inString = false;

                for (int j = 0; j < chars.Length; j++)
                {
                    if (inBlockComment)
                    {
                        if (chars[j] == '#' && j + 1 < chars.Length && chars[j + 1] == '}')
                        {
                            inBlockComment = false;
                            chars[j] = ' ';
                            chars[j + 1] = ' ';
                            j++;
                        }
                        else
                        {
                            chars[j] = ' ';
                        }
                        continue;
                    }

                    if (chars[j] == '"')
                    {
                        inString = !inString;
                        continue;
                    }

                    if (inString) continue;

                    if (chars[j] == '#')
                    {
                        if (j + 1 < chars.Length && chars[j + 1] == '{')
                        {
                            inBlockComment = true;
                            chars[j] = ' ';
                            chars[j + 1] = ' ';
                            j++;
                            continue;
                        }

                        for (int k = j; k < chars.Length; k++)
                        {
                            chars[k] = ' ';
                        }
                        break;
                    }
                }

                if (inString)
                {
                    Errors.Add(new ValidationError(fileName, i + 1, "Unclosed string literal. Strings must be closed on the same line."));
                }

                result.Add(new string(chars));
            }

            return result;
        }

        private string SanitizeLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;

            // Fix Unicode hidden spaces and formatting blocks
            line = line.Replace("\u200B", "")   // Zero-Width Space
                       .Replace("\u200C", "")   // Zero-Width Non-Joiner
                       .Replace("\u200D", "")   // Zero-Width Joiner
                       .Replace("\uFEFF", "")   // Byte Order Mark (BOM) if embedded mid-file
                       .Replace("\u00A0", " "); // Non-Breaking Space -> convert to regular space

            return line;
        }
        // Handles line-by-line validation, tracking dynamic indentation rules
        protected virtual void ValidateLines(List<string> lines, string filePath)
        {
            if (lines.Count == 0)
            {
                Console.WriteLine($"[WARNING] File is empty after sanitization: {filePath}");
                return;
            }

            string fileName = Path.GetFileName(filePath);

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                int lineNumber = i + 1;

                // Skip blank lines
                if (string.IsNullOrWhiteSpace(line)) continue;

                int spaceCount = GetLeadingSpaceCount(line);

                // If remainder exists when divided by 4, it's an indentation error (digit with a comma)
                if (spaceCount % 4 != 0)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"Indentation error: {spaceCount} spaces is not a multiple of 4."
                    ));
                }

                int currentDepth = spaceCount / 4;
                string trimmedLine = line.Trim();

                ValidateLineContent(trimmedLine, currentDepth, lineNumber, fileName);
            }
        }

        protected virtual void ValidateLineContent(string cleanLine, int currentDepth, int lineNumber, string fileName)
        {
            // Example of what you can easily do in the future:
            // if (cleanLine.StartsWith("scripted_effects") && currentDepth != 2) { ... }
        }

    }
}
