using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Compiler
{
    public class RawLine
    {
        public string trimmedLine;
        public int depth;
    }
    public record ParsingError(string FileName, int LineNumber, string ErrorMessage);

    public abstract class BaseParser
    {
        // The validator must populate this before calling ParseFile so parsers
        // always have the source file name available. Parsers may rely on
        // this value when producing parsing output or errors.
        public string SourceFileName { get; set; } = string.Empty;

        protected void EnsureSourceFileNameSet()
        {
            if (string.IsNullOrWhiteSpace(SourceFileName))
            {
                throw new InvalidOperationException("Parser SourceFileName was not provided by the validator.");
            }
        }

        public List<ParsingError> Errors { get; protected set; } = new List<ParsingError>();
        // Parser receives a preprocessed representation of the file produced by
        // the validator: trimmed line text, calculated depth, line number, and file name.
        // Specialized parsers MUST implement this method.
        public abstract void ParseFile(string filePath, string fileName, List<BaseValidator.PreprocessedLine> preprocessedLines);
        public static string GetQuotedContent(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Trim();

            // Fully quoted string
            if (s.Length >= 2 && s.StartsWith("\"") && s.EndsWith("\""))
            {
                return s.Substring(1, s.Length - 2);
            }

            // Find the first quoted segment inside the string
            int first = s.IndexOf('"');
            if (first >= 0)
            {
                int second = s.IndexOf('"', first + 1);
                if (second > first)
                {
                    return s.Substring(first + 1, second - first - 1);
                }
            }

            return s;
        }
    }
}
