using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Compiler
{
    public record ParsingError(string FileName, int LineNumber, string ErrorMessage);

    public abstract class BaseParser
    {
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
