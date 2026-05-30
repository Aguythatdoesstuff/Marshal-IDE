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

            // ALOT easier to strip out a not necessery for syntax closing bracket then to handle it later on in the pipeline!
            if (line.Trim() == "}") return string.Empty;

            // Fix Unicode hidden spaces and formatting blocks
            line = line.Replace("\u200B", "")   // Zero-Width Space
                       .Replace("\u200C", "")   // Zero-Width Non-Joiner
                       .Replace("\u200D", "")   // Zero-Width Joiner
                       .Replace("\uFEFF", "")   // Byte Order Mark (BOM) if embedded mid-file
                       .Replace("\u00A0", " "); // Non-Breaking Space -> convert to regular space

            return line;
        }
        // Handles line-by-line validation, tracking dynamic indentation rules
        protected List<string> FileLines { get; private set; } = new List<string>();
        protected int CurrentLineIndex { get; private set; } = 0;

        protected string GetLineAt(int offset)
        {
            int targetIndex = CurrentLineIndex + offset;
            if (targetIndex >= 0 && targetIndex < FileLines.Count)
            {
                return FileLines[targetIndex];
            }
            return null;
        }

        protected int GetDepthAt(int offset)
        {
            int targetIndex = CurrentLineIndex + offset;
            int direction = offset >= 0 ? 1 : -1;

            while (targetIndex >= 0 && targetIndex < FileLines.Count)
            {
                if (!string.IsNullOrWhiteSpace(FileLines[targetIndex]))
                {
                    return GetLeadingSpaceCount(FileLines[targetIndex]) / 4;
                }
                targetIndex += direction;
            }
            return 0;
        }

        protected virtual void ValidateLines(List<string> lines, string filePath)
        {
            if (lines.Count == 0)
            {
                Console.WriteLine($"[WARNING] File is empty after sanitization: {filePath}");
                return;
            }

            FileLines = lines;
            string fileName = Path.GetFileName(filePath);

            for (CurrentLineIndex = 0; CurrentLineIndex < FileLines.Count; CurrentLineIndex++)
            {
                string line = FileLines[CurrentLineIndex];
                int lineNumber = CurrentLineIndex + 1;

                if (string.IsNullOrWhiteSpace(line)) continue;

                int spaceCount = GetLeadingSpaceCount(line);

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
        protected virtual bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            return false;
        }
        protected int ExpectedDepth = 0;
        protected virtual void ValidateLineContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        {
            bool lineRecognized = false;

            // Give child validators first chance to recognize and handle the line.
            // This avoids producing spurious indentation errors for valid custom
            // root-level constructs (e.g., 'scripted effect ...') before the
            // custom validator can adjust ExpectedDepth or report its own errors.
            lineRecognized = ValidateCustomContent(trimmedLine, currentDepth, lineNumber, fileName);
            if (lineRecognized)
            {
                int followingDepth = GetDepthAt(1);
                if (followingDepth < currentDepth)
                {
                    ExpectedDepth = followingDepth;
                }
                return;
            }

            var scopeKeywords = new[] { "if", "else if", "else", "then", "not", "and", "or" };

            if (scopeKeywords.Contains(trimmedLine))
            {
                // scope keywords must appear at the current expected depth
                if (currentDepth != ExpectedDepth)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"Unexpected indentation: expected depth {ExpectedDepth}, but got {currentDepth}."
                    ));
                }

                lineRecognized = true;
                ExpectedDepth += 1;
            }
            else
            {
                int lineOpens = 0;
                int lineCloses = 0;
                bool inString = false;
                bool hasContentAfterOpen = false;

                for (int idx = 0; idx < trimmedLine.Length; idx++)
                {
                    char c = trimmedLine[idx];
                    if (c == '"')
                    {
                        inString = !inString;
                        continue;
                    }

                    if (!inString)
                    {
                        if (c == '{')
                        {
                            lineOpens++;
                            for (int k = idx + 1; k < trimmedLine.Length; k++)
                            {
                                if (!char.IsWhiteSpace(trimmedLine[k]) && trimmedLine[k] != '}')
                                {
                                    hasContentAfterOpen = true;
                                    break;
                                }
                            }
                        }
                        else if (c == '}')
                        {
                            lineCloses++;
                        }
                    }
                }

                var blockPrefixes = new[] { "desc", "name", "sprite" };
                string[] parts = trimmedLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // "scripted effect open_random_ad" so that custom validators can handle
                // them (for example to increase ExpectedDepth).
                if (lineOpens > 0 || lineCloses > 0 || (parts.Length == 3 && (parts[1] == "=" || parts[1] == "<" || parts[1] == ">")) || blockPrefixes.Any(p => trimmedLine.StartsWith(p)))
                {
                    lineRecognized = true;

                    // If tokens indicate a pass-through block like: identifier = {
                    // but the character scan didn't detect the '{' (possible due to tokenization or formatting),
                    // account for the opening brace here so expected depth increases for following lines.
                    if (parts.Length == 3 && parts[1] == "=" && parts[2] == "{" && lineOpens == 0)
                    {
                        lineOpens = 1;
                    }
                }

                if (hasContentAfterOpen && lineOpens != lineCloses)
                {
                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        "Malformed one-liner: One-line expressions require explicit closing brackets '}'."
                    ));
                }

                // Allow closing-only or mixed lines to be indented at the previous depth as long as
                // after accounting for opens/closes the expected depth matches. For example, a line
                // with a single '}' may be indented at the same level as its inner content; after
                // processing the brace the effective expected depth will be one less.
                int expectedBefore = ExpectedDepth;
                int expectedAfter = expectedBefore + lineOpens - lineCloses;

                int minExpected = Math.Min(expectedBefore, expectedAfter);
                int maxExpected = Math.Max(expectedBefore, expectedAfter);

                if (currentDepth < minExpected || currentDepth > maxExpected)
                {
                    string expectedText = minExpected == maxExpected
                        ? $"expected depth {minExpected}"
                        : $"expected depth between {minExpected} and {maxExpected}";

                    Errors.Add(new ValidationError(
                        fileName,
                        lineNumber,
                        $"Unexpected indentation: {expectedText}, but got {currentDepth}."
                    ));
                }

                ExpectedDepth = expectedAfter;
            }

            if (!lineRecognized)
            {
                lineRecognized = ValidateCustomContent(trimmedLine, currentDepth, lineNumber, fileName);
            }

            if (!lineRecognized)
            {
                Errors.Add(new ValidationError(
                    fileName,
                    lineNumber,
                    $"Unknown action or malformed expression: '{trimmedLine}'."
                ));
            }

            int nextDepth = GetDepthAt(1);
            if (nextDepth < currentDepth)
            {
                ExpectedDepth = nextDepth;
            }
        }

        // example how to implement custom calidation in a child class
        //protected override bool ValidateCustomContent(string trimmedLine, int currentDepth, int lineNumber, string fileName)
        //{
        //    if (trimmedLine.StartsWith("custom_command"))
        //    {
        //        // run checks
        //        return true; // Mark as recognized
        //    }
        //    return false; // Not handled by this child validator
        //}
    }
}
