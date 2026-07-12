using System;
using System.Globalization;
using System.IO;

namespace Compiler
{
    public abstract class BaseCompiler
    {
        // Compilers may add non-fatal errors here. ProcessManager will collect
        // these after compilation and include them in the global report.
        public List<ValidationError> CompilerErrors { get; } = new List<ValidationError>();

        // Tracks files that have already been created by any compiler during this
        // run. The first write to a given output path will truncate/create the
        // file; subsequent writes will append. This is static so multiple
        // compiler instances writing the same output file behave consistently.
        private static readonly object _writtenFilesLock = new object();
        private static readonly HashSet<string> _createdOutputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public static string OutputPath { get; set; }
        public string SourceFileName { get; set; } = string.Empty;
        // Absolute path to the original source file. Populated by ProcessManager
        // for cases where compilers need to read or copy the original file.
        public string SourceAbsolutePath { get; set; } = string.Empty;

        /// Calculates the Hearts of Iron IV 'cost' value based on a duration and time unit.
        public static string GetHoi4Cost(double value, string unit)
        {
            // Clean up the unit string to avoid casing or spacing issues
            string timeUnit = unit.Trim().ToLower();

            double finalCost;

            if (timeUnit == "weeks" || timeUnit == "week")
            {
                // If it's in weeks, 1 cost = 1 week
                finalCost = value;
            }


            else
            {
                // Default to 'days' calculation (1 cost unit = 7 days)
                double exactCost = value / 7.0;

                // Add the small buffer for HOI4 UI display accuracy
                double safeCost = exactCost + 0.001;

                // Round to 3 decimal places
                finalCost = (Math.Round(safeCost, 3))-0.02; // -0.02 to ensure the value is slightly lower then the exact cost/rounded up wich could cause a issue that 1 day could show up ad 2 days ingame
            }

            // "G" format string keeps it compact (e.g., 1.43 instead of 1.430)
            // InvariantCulture guarantees the dot (.) separator
            return finalCost.ToString("G", CultureInfo.InvariantCulture);
        }

        // overload that exposes the 'created' flag so callers can write one-time
        // headers when a file is created for the first time during a run.
        protected void WriteFile(string relativeSubfolder, string fileName, string extension, Action<StreamWriter, bool> writeAction)
        {
            if (writeAction == null) return;

            if (string.IsNullOrWhiteSpace(extension)) extension = ".txt";
            if (!extension.StartsWith(".")) extension = "." + extension;

            // Determine the output file name. Prefer an explicit fileName supplied by the caller.
            // If none is supplied, fall back to the original source file name when available.
            // If neither is available, use a generic fallback to avoid throwing here.
            string finalName;
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                finalName = Path.GetFileNameWithoutExtension(fileName);
            }
            else if (!string.IsNullOrWhiteSpace(SourceFileName))
            {
                finalName = Path.GetFileNameWithoutExtension(SourceFileName);
            }
            else
            {
                finalName = "output";
            }

            // Enforce game engine localisation file naming convention for YAML outputs.
            // All .yml localisation files must end with the _l_english suffix before the extension.
            if (extension.Equals(".yml", StringComparison.OrdinalIgnoreCase))
            {
                const string locSuffix = "_l_english";
                if (!finalName.EndsWith(locSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    finalName = finalName + locSuffix;
                }
            }

            var subfolder = string.IsNullOrWhiteSpace(relativeSubfolder) ? OutputPath : Path.Combine(OutputPath, relativeSubfolder);
            Directory.CreateDirectory(subfolder);
            var outPath = Path.Combine(subfolder, finalName + extension);

            bool shouldCreate = false;
            lock (_writtenFilesLock)
            {
                if (!_createdOutputFiles.Contains(outPath))
                {
                    shouldCreate = true;
                    _createdOutputFiles.Add(outPath);
                }
            }

            var fileMode = shouldCreate ? FileMode.Create : FileMode.Append;

            // Golden Rule Check: ONLY .yml files are allowed to have a BOM encoding!
            bool useBOM = extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
            var encoding = new System.Text.UTF8Encoding(useBOM);

            using (var fs = new FileStream(outPath, fileMode, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, encoding) { NewLine = "\n" })
            {
                writeAction(sw, shouldCreate);
            }
        }



        public static string Ident(int level)
        {
            if (level <= 0) return string.Empty;

            int spaceCount = level * 4;

            // Fast path for common indentation depths to bypass allocation completely
            return spaceCount switch
            {
                4 => "    ",
                8 => "        ",
                12 => "            ",
                16 => "                ",
                // Fallback that builds the exact string size perfectly in memory without concatenation loops
                _ => string.Create(spaceCount, spaceCount, (span, count) => span.Fill(' '))
            };
        }

        public abstract void Compile();

        /// <summary>
        /// Clears the internal tracker of output files that have been created during
        /// the current process run. Call at the start of a full processing pass so
        /// files are created/truncated on first write within that pass instead of
        /// being treated as already-created and appended to.
        /// </summary>
        public static void ResetCreatedOutputFiles()
        {
            lock (_writtenFilesLock)
            {
                _createdOutputFiles.Clear();
            }
        }

        /// Writes a collection of parsed lines (items that expose a depth and trimmedLine)
        /// to the StreamWriter while converting simple syntax keywords into bracketed
        /// blocks (and/or/not -> "and = {" etc, if/else if/else with limit/then handling).
        /// The method accepts optional selectors for depth and text; if not supplied
        /// it will attempt to read properties named "depth" and "trimmedLine" via reflection.
        protected void WriteAllowedWithConversions<T>(StreamWriter sw, IEnumerable<T> allowed, Func<T, int>? depthSelector = null, Func<T, string>? textSelector = null)
        {
            if (sw == null) throw new ArgumentNullException(nameof(sw));
            if (allowed == null) return;

            // Build a simple in-memory list of (depth, text) for easy indexed/recursive processing
            var list = new List<(int depth, string text)>();

            foreach (var item in allowed)
            {
                int depth = 0;
                string text = string.Empty;

                if (depthSelector != null) depth = depthSelector(item);
                if (textSelector != null) text = textSelector(item) ?? string.Empty;

                if (depthSelector == null || textSelector == null)
                {
                    // Try reflection fallback for common property names
                    var t = item?.GetType();
                    if (t != null)
                    {
                        if (depthSelector == null)
                        {
                            var pd = t.GetProperty("depth") ?? t.GetProperty("Depth");
                            if (pd != null)
                            {
                                try { depth = Convert.ToInt32(pd.GetValue(item)); } catch { depth = 0; }
                            }
                        }

                        if (textSelector == null)
                        {
                            var pt = t.GetProperty("trimmedLine") ?? t.GetProperty("TrimmedLine") ?? t.GetProperty("text") ?? t.GetProperty("Text");
                            if (pt != null)
                            {
                                try { text = Convert.ToString(pt.GetValue(item)) ?? string.Empty; } catch { text = string.Empty; }
                            }
                        }
                    }
                }

                // As a last resort use ToString
                if (string.IsNullOrEmpty(text) && item != null) text = item.ToString() ?? string.Empty;

                // Sanitize text now so internal processing (Normalize, regex, comparisons)
                // does not get confused by invisible/formatting characters.
                text = SanitizeForOutput(text).TrimEnd();

                list.Add((depth, text));
            }

            // Normalize whitespace for comparisons
            static string Normalize(string s) => System.Text.RegularExpressions.Regex.Replace(s.Trim(), "\\s+", " ").ToLowerInvariant();

            // Recursive processor: process items from index 'start' until 'end' or until a line with depth <= parentDepth
            int ProcessRange(int start, int end, int parentDepth)
            {
                int i = start;
                while (i < end)
                {
                    var (depth, text) = list[i];
                    if (depth <= parentDepth) break;

                    var lower = Normalize(text);

                    // If the line already has an opening brace, just write as-is
                    if (text.Contains("= {"))
                    {
                        sw.WriteLine(Ident(depth) + text);
                        i++; continue;
                    }

                    // Simple boolean operator blocks. Accept forms where the keyword may
                    // appear alone ("not") or followed by inline content ("not has_flag = yes").
                    // If inline content exists, render it as an immediate child inside the
                    // operator block so the output becomes: not = { has_flag = yes }
                    // Detect boolean operator lines robustly using regex so we capture
                    // the operator and any inline remainder regardless of spacing or case.
                    var opMatch = System.Text.RegularExpressions.Regex.Match(text, "^(\\s*)(and|or|not)\\b(.*)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (opMatch.Success)
                    {
                        string keyword = opMatch.Groups[2].Value.ToLowerInvariant();
                        string remainder = opMatch.Groups[3].Value ?? string.Empty;
                        remainder = remainder.TrimStart();

                        sw.WriteLine(Ident(depth) + keyword + " = {");

                        if (!string.IsNullOrEmpty(remainder))
                        {
                            sw.WriteLine(Ident(depth + 1) + remainder);
                        }

                        // Process any following child lines at the deeper level
                        i = ProcessRange(i + 1, end, depth);
                        sw.WriteLine(Ident(depth) + "}");
                        continue;
                    }

                    // Handle if / else if / else
                    if (lower == "if" || lower == "else if" || lower == "else")
                    {
                        string name = lower == "if" ? "if" : (lower == "else" ? "else" : "else_if");
                        sw.WriteLine(Ident(depth) + name + " = {");

                        // Collect condition lines for 'if' and 'else if' (everything after this with depth > current depth until a 'then' or a line with depth <= current)
                        // Do NOT collect condition lines for a plain 'else' - else never has a limit block.
                        int j = i + 1;
                        var condLines = new List<(int depth, string text)>();
                        if (lower != "else")
                        {
                            while (j < end && list[j].depth > depth && Normalize(list[j].text) != "then")
                            {
                                condLines.Add(list[j]); j++;
                            }
                        }

                        if (condLines.Count > 0)
                        {
                            int limitIndent = condLines.Min(x => x.depth);

                            // To ensure the condition lines receive the same conversions as
                            // regular body content (e.g., converting 'not' into a block),
                            // temporarily bump their depth by one and run them through
                            // ProcessRange. This writes the content one level deeper so it
                            // sits inside the emitted 'limit = { ... }' block.
                            for (int k = i + 1; k < j; k++)
                            {
                                var t = list[k];
                                list[k] = (t.depth + 1, t.text);
                            }

                            sw.WriteLine(Ident(limitIndent) + "limit = {");
                            // Process the adjusted range; parentDepth is limitIndent so
                            // ProcessRange will treat lines with depth > limitIndent as children.
                            ProcessRange(i + 1, j, limitIndent);
                            sw.WriteLine(Ident(limitIndent) + "}");

                            // Restore original depths
                            for (int k = i + 1; k < j; k++)
                            {
                                var t = list[k];
                                list[k] = (t.depth - 1, t.text);
                            }
                        }

                        // Skip a terminating 'then' token if present
                        if (j < end && list[j].depth > depth && Normalize(list[j].text) == "then") j++;

                        // Process the body of the if/else (all following items with depth > current depth)
                        // For 'if' and 'else if' the body effects should be one indentation level less
                        // than the temporary limit block handling produced. To avoid treating the
                        // following 'else' (which has the same depth as the 'if') as a child we
                        // compute the body range first and temporarily decrement the depths of
                        // those body lines before processing. This preserves correct scoping
                        // while producing the desired indentation.
                        if (lower == "if" || lower == "else if")
                        {
                            int bodyStart = j;
                            int bodyEnd = j;
                            while (bodyEnd < end && list[bodyEnd].depth > depth) bodyEnd++;

                            // Temporarily decrease depths for proper output indentation
                            for (int k = bodyStart; k < bodyEnd; k++)
                            {
                                var t = list[k];
                                list[k] = (t.depth - 1, t.text);
                            }

                            j = ProcessRange(bodyStart, bodyEnd, depth - 1);

                            // Restore original depths
                            for (int k = bodyStart; k < bodyEnd; k++)
                            {
                                var t = list[k];
                                list[k] = (t.depth + 1, t.text);
                            }
                        }
                        else
                        {
                            j = ProcessRange(j, end, depth);
                        }

                        sw.WriteLine(Ident(depth) + "}");
                        i = j;
                        continue;
                    }

                    // Default: write the original line
                    sw.WriteLine(Ident(depth) + text);
                    i++;
                }

                return i;
            }

            // Start processing top-level (use parentDepth = -1 so depth 0 items are considered children)
            ProcessRange(0, list.Count, -1);
        }

        // Renders the same output produced by WriteAllowedWithConversions into a string
        // using an in-memory buffer. This avoids duplicating the MemoryStream + StreamWriter
        // boilerplate in every concrete compiler while preserving exact textual output.
        protected string RenderAllowedToString<T>(IEnumerable<T>? items, Func<T, int>? depthSelector = null, Func<T, string>? textSelector = null)
        {
            if (items == null) return string.Empty;
            using var ms = new MemoryStream();
            // Use UTF8 without BOM when rendering to an in-memory buffer to avoid
            // injecting a byte-order-mark into the returned string. Also ensure
            // the temporary writer normalizes newlines to LF so downstream
            // callers see consistent line endings identical to the legacy Node
            // pipeline.
            using (var swTemp = new StreamWriter(ms, new System.Text.UTF8Encoding(false), 1024, true) { NewLine = "\n" })
            {
                // reuse the existing protected method which writes the properly converted lines
                this.WriteAllowedWithConversions(swTemp, items, depthSelector, textSelector);
                swTemp.Flush();
                ms.Position = 0;
                using var sr = new StreamReader(ms);
                return sr.ReadToEnd();
            }
        }

        // Remove invisible/formatting/control characters that should never appear in output
        private static string SanitizeForOutput(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var sb = new System.Text.StringBuilder(input.Length);
            foreach (var c in input)
            {
                // Always allow common whitespace characters
                if (c == '\r' || c == '\n' || c == '\t')
                {
                    sb.Append(c);
                    continue;
                }

                var category = char.GetUnicodeCategory(c);

                // Skip invisible/formatting chars (e.g., zero-width space/joiner, RTL/LTR marks)
                if (category == System.Globalization.UnicodeCategory.Format)
                {
                    continue;
                }

                // Skip control characters (except CR/LF/TAB handled above)
                if (category == System.Globalization.UnicodeCategory.Control)
                {
                    continue;
                }

                sb.Append(c);
            }

            return sb.ToString();
        }

        // StreamWriter instances are configured at call sites to use Unix newlines ("\n").
        // The previous SanitizingStreamWriter type only set NewLine and did not sanitize
        // content, so it was removed in favor of using StreamWriter directly.
    }
}
