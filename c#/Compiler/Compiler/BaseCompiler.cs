using System;
using System.Globalization;
using System.IO;

namespace Compiler
{
    public abstract class BaseCompiler
    {
        // Tracks files that have already been created by any compiler during this
        // run. The first write to a given output path will truncate/create the
        // file; subsequent writes will append. This is static so multiple
        // compiler instances writing the same output file behave consistently.
        private static readonly object _writtenFilesLock = new object();
        private static readonly HashSet<string> _createdOutputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public static string OutputPath { get; set; }
        public string SourceFileName { get; set; } = string.Empty;

        /// Calculates the Hearts of Iron IV 'cost' value based on a duration and time unit.
        public static string GetHoi4Cost(double value, string unit)
        {
            // Clean up the unit string to avoid casing or spacing issues
            string timeUnit = unit.Trim().ToLower();

            double finalCost;

            if (timeUnit == "weeks")
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
                finalCost = Math.Round(safeCost, 3);
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
            using (var sw = new StreamWriter(fs, encoding))
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

        /* ============================================================================
           EXAMPLE USAGE INSIDE A SPECIALIZED COMPILER
           ============================================================================

           public class ScriptedGuiCompiler : BaseCompiler
           {
               // Assume this data was handed down after the Validator/Parser dropped old lists
               public ScriptedGUI PassedData { get; set; } 

               public override void Compile()
               {
                   // 1. Call WriteFile and pass a lambda expression 'sw => { ... }'
                   WriteFile("common/scripted_gui", "my_mod_gui", ".txt", sw =>
                   {
                       // Write the initial structural header
                       sw.WriteLine("scripted_gui = {");

                       // 2. Loop through your parsed data structures
                       foreach (var window in PassedData.Windows)
                       {
                           // 3. Use .NET 10 Raw String Literals (""") to easily output multi-line 
                           // templates from a single input element without clogging your RAM.
                           sw.Write($"""
                       	        {window.Id} = {{
                       		        size = {{ x = {window.Size?.x ?? 500} y = {window.Size?.y ?? 400} }}
                       		        reset_on_closure = yes
                       	        }}
                               """);

                           // You can mix single lines and raw string blocks perfectly. 
                           // The StreamWriter buffers it all automatically before touching the SSD.
                           if (window.Draggable)
                           {
                               sw.WriteLine("\t\tmoveable = yes");
                           }
                       }

                       // Close the core structure
                       sw.WriteLine("}");
                   }); 
                   // At this closing parenthesis, the file stream is flushed, closed, and saved to disk.
               }
           }
        */

        public abstract void Compile();

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

                list.Add((depth, text.TrimEnd()));
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

                    // Simple boolean operator blocks
                    if (lower == "and" || lower == "or" || lower == "not")
                    {
                        sw.WriteLine(Ident(depth) + lower + " = {");
                        i = ProcessRange(i + 1, end, depth);
                        sw.WriteLine(Ident(depth) + "}");
                        continue;
                    }

                    // Handle if / else if / else
                    if (lower == "if" || lower == "else if" || lower == "else")
                    {
                        string name = lower == "if" ? "if" : (lower == "else" ? "else" : "else_if");
                        sw.WriteLine(Ident(depth) + name + " = {");

                        // Collect condition lines (everything after this with depth > current depth until a 'then' or a line with depth <= current)
                        int j = i + 1;
                        var condLines = new List<(int depth, string text)>();
                        while (j < end && list[j].depth > depth && Normalize(list[j].text) != "then")
                        {
                            condLines.Add(list[j]); j++;
                        }

                        if (condLines.Count > 0)
                        {
                            int limitIndent = condLines.Min(x => x.depth);
                            sw.WriteLine(Ident(limitIndent) + "limit = {");
                            foreach (var cl in condLines)
                            {
                                // print condition children one level deeper than their original depth so they sit inside limit
                                sw.WriteLine(Ident(cl.depth + 1) + cl.text);
                            }
                            sw.WriteLine(Ident(limitIndent) + "}");
                        }

                        // Skip a terminating 'then' token if present
                        if (j < end && list[j].depth > depth && Normalize(list[j].text) == "then") j++;

                        // Process the body of the if/else (all following items with depth > current depth)
                        j = ProcessRange(j, end, depth);

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
    }
}
